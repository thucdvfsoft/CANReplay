using System;
using System.IO;
using System.Text.RegularExpressions;
using vxlapi_NET; // Đảm bảo rằng bạn đã đúng namespace từ vxlapi_NET
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using CanReproduce.Apps;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows;
using System.Configuration;
using System.Linq;

namespace CANReplay.Apps
{
    public class Replay
    {
        public enum ReplayState
        {
            Running,
            Paused,
            Stopped
        }

        private CancellationTokenSource cancellationTokenSource;
        private Task replayTask;
        private ReplayState currentState = ReplayState.Stopped;

        private static Regex regex = new Regex(@"(\d+\.\d+)\s+(\d+)\s+(0x)?([0-9A-Fa-f]+)\s+(Tx|Rx)\s+d\s+(\d+)\s+((?:[0-9A-Fa-f]{2}(?:\s+)?)+)");
        private static Regex regexDate = new Regex(@"date\s+(\d{1,2}/\d{1,2}/\d{4})\s+(\d{1,2}):(\d{1,2}):(\d{1,2}\.\d{1,3})");

        private string sentLogFilePath = "sent_can_log.asc"; // Đường dẫn file để ghi log các bản tin đã gửi

        private int portHandle;
        private XLDriver xlDriver;
        private ulong permissionMask = 1; // Chuyển kiểu thành 'ulong'
        private uint accessMask = 1;

        public uint AccessMask
        {
            get
            {
                return accessMask;
            }
            set
            {
                accessMask = value;
                permissionMask = value;
            }
        }
        public int InfoChannel { get; set; } = -1;
        public uint Bitrate { get; set; }
        public uint BufferSize { get; set; }
        public Boolean IsAutoDetectInfo { get; set; }
        public uint Delay { get; set; }
        public Label StatusLabel { get; set; }
        public Label CurrentTimeLable { get; set; }
        public Label CurrentTimeMHULable { get; set; }

        public bool Connected { get; set; }

        private bool useWhiteListID = true;
        private HashSet<uint> blackListCANIds = new HashSet<uint>();
        private HashSet<uint> whiteListCANIds = new HashSet<uint>();
        private DateTime? startTime;

        public Replay()
        {
            // Khởi tạo XL Driver
            xlDriver = new XLDriver();
            useWhiteListID = Boolean.Parse(ConfigurationManager.AppSettings["Use_White_List_ID"] ?? System.Boolean.TrueString);
            LOG.WriteLine("useWhiteListID: " + useWhiteListID);
        }

        // Hàm để bắt đầu phát lại log CAN
        public void StartReplay(string logFilePath)
        {
            try
            {
                LOG.WriteLine("Started");
                cancellationTokenSource = new CancellationTokenSource();
                currentState = ReplayState.Running;
                if (replayTask != null)
                {
                    replayTask.Dispose();
                }

                replayTask = Task.Factory.StartNew(() =>
                {
                    ReplayLog(logFilePath, cancellationTokenSource);
                    ClosePort();
                }
                , cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                LOG.WriteLine("Exception: " + e.Message);
                ClosePort();
            }
            finally
            {
                //ClosePort();
            }
        }

        private void ReplayLog(string logFilePath, CancellationTokenSource token)
        {
            XLDefine.XL_Status status;

            if (!Connected)
            {
                if (!Connect())
                {
                    return;
                }
            }

            sentLogFilePath = $"CAN_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.asc";

            if (IsAutoDetectInfo)
            {
                // Xác định kênh INFO trước khi đọc file
                IdentifyInfoChannel(logFilePath);
            }
            else
            {
                if (InfoChannel <= 0)
                {
                    LOG.WriteLine($"!!! Error InfoChannel = {InfoChannel} < 0");
                    return;
                }
            }

            LOG.WriteLine($"InfoChannel = {InfoChannel}, " +
                $"AccessMask = {AccessMask}, " +
                $"Bitrate = {Bitrate}, " +
                $"BufferSize = {BufferSize}, " +
                $"Delay = {Delay}, " +
                $"IsAutoDetectInfo = {IsAutoDetectInfo}");

            float previousTimestamp = 0f;

            LoadBlackListId();
            if (useWhiteListID)
            {
                LoadWhiteListId();
            }

            // Đọc file log và phát lại thông điệp CAN
            using (StreamReader reader = new StreamReader(logFilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (token.IsCancellationRequested)
                    {
                        LOG.WriteLine("Replay stopped.");
                        break;
                    }

                    // Kiểm tra nếu đang ở trạng thái paused
                    while (currentState == ReplayState.Paused)
                    {
                        if (token.IsCancellationRequested)
                        {
                            LOG.WriteLine("Replay stopped.");
                            return;
                        }
                        Thread.Sleep(100); // Đợi một khoảng thời gian nhỏ trước khi tiếp tục kiểm tra
                    }

                    if (currentState == ReplayState.Stopped)
                    {
                        LOG.WriteLine("Replay stopped.");
                        break;
                    }

                    // Bỏ qua các dòng trống hoặc không hợp lệ
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    Match findDate = regexDate.Match(line);
                    if (findDate.Success)
                    {
                        string[] dateFmt = {"d/M/yyyy H:m:s.fff", "d/M/yyyy H:m:s.f","d/M/yyyy H:m:s.ff",
                                            "d/M/yyyy H:m:s.ffff","d/M/yyyy H:m:s.fffff","d/M/yyyy H:m:s.ffffff",
                                            };

                        string dateTimeString = findDate.Groups[1].Value
                            + " " + findDate.Groups[2].Value
                            + ":" + findDate.Groups[3].Value
                            + ":" + findDate.Groups[4].Value;

                        startTime = dateTimeString.ToDate(dateFmt);
                        continue;
                    }

                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        string timestampStr = match.Groups[1].Value;
                        float currentTimestamp = float.Parse(timestampStr, CultureInfo.InvariantCulture);
                        uint canId = Convert.ToUInt32(match.Groups[4].Value, 16);
                        string dataStr = match.Groups[7].Value;
                        ushort dlc = ushort.Parse(match.Groups[6].Value);
                        int channel = int.Parse(match.Groups[2].Value); // Channel của message

                        // Chỉ gửi các tin nhắn từ kênh INFO
                        if (channel != InfoChannel)
                        {
                            //LOG.WriteLine($"Skipping message from CAN ID: 0x{canId:X} (Channel {channel}), not from INFO channel.");
                            continue; // Bỏ qua các tin nhắn không thuộc kênh INFO
                        }

                        // Kiểm tra xem CAN ID có nằm trong BlackList không
                        if (blackListCANIds.Contains(canId))
                        {
                            //LOG.WriteLine($"Skipping CAN ID: 0x{canId:X}");
                            continue;  // Bỏ qua CAN ID này
                        }

                        // Kiểm tra xem CAN ID có nằm trong WhiteList không
                        if (useWhiteListID)
                        {
                            if (!whiteListCANIds.Contains(canId))
                            {
                                //LOG.WriteLine($"Skipping CAN ID: 0x{canId:X}");
                                continue;  // Bỏ qua CAN ID này
                            }
                        }
                        
                        // Tính toán delay
                        if (previousTimestamp == 0)
                        {
                            previousTimestamp = currentTimestamp;
                        }
                        float delay = currentTimestamp * 1000 - previousTimestamp * 1000;
                        if (delay > 0)
                        {
                            Thread.Sleep((int)(delay + Delay));
                        }

                        previousTimestamp = currentTimestamp;
                        status = SendCommands(canId, dataStr);
                        if (status != XLDefine.XL_Status.XL_SUCCESS)
                        {
                            LOG.WriteLine($"Error sending message with CAN ID 0x{canId:X}: " + status);
                            Connected = false;
                            break;
                        }
                        else
                        {
                            // Ghi log thông điệp đã gửi
                            //LogSentMessage(currentTimestamp, channel, canId, dlc, data);

                            // Calculate the real timestamp for this log line
                            string actualTimeStr = "0";
                            if (startTime != null)
                            {
                                DateTime actualTime = startTime.Value.AddSeconds(currentTimestamp);
                                actualTimeStr = actualTime.ToString("dd/MM/yyyy HH:mm:ss.fff");
                            }

                            RunOnUIThread(() =>
                            {
                                this.CurrentTimeLable.Content = currentTimestamp.ToString();
                                this.CurrentTimeMHULable.Content = actualTimeStr;
                            });
                        }
                    }
                }
            }

            RunOnUIThread(() =>
            {
                this.StatusLabel.Content = "Stoped";
            });
        }

        private void RunOnUIThread(Action action)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                action.Invoke();
            }));
        }

        public void StopReplay()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
            currentState = ReplayState.Stopped;
            ClosePort();
            LOG.WriteLine("stopped.");
        }


        public void PauseReplay()
        {
            currentState = ReplayState.Paused;
            LOG.WriteLine("paused.");
        }

        public void ResumeReplay()
        {
            currentState = ReplayState.Running;
            LOG.WriteLine("resumed.");
        }

        // Hàm để log các bản tin CAN đã gửi
        private void LogSentMessage(float timestamp, int channel, uint canId, int dlc, byte[] data)
        {
            string timestampStr = timestamp.ToString();
            string canIdStr = $"0x{canId:X}";
            string dataStr = BitConverter.ToString(data).Replace("-", " ");

            string logMessage = $"{timestampStr}    {channel}   {canIdStr}      TX      {dataStr}";
            using (StreamWriter logWriter = new StreamWriter(sentLogFilePath, true))
            {
                logWriter.WriteLine(logMessage);
            }
        }

        // Hàm để đóng cổng khi không sử dụng nữa
        public bool ClosePort()
        {
            XLDefine.XL_Status status1 = xlDriver.XL_ClosePort(portHandle);
            if (status1 != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine($"status1 = {status1}");
                //return false;
            }
            XLDefine.XL_Status status2 = xlDriver.XL_CloseDriver();
            if (status2 != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine($"status1 = {status1}, status2 = {status2}");
                return false;
            }
            LOG.WriteLine($"Close port OK: status1 = {status1}, status2 = {status2}");
            Connected = false;
            return true;
        }

        // Hàm để đọc các CAN ID cần bỏ qua từ file cấu hình
        private void LoadBlackListId()
        {
            string black_string = ConfigurationManager.AppSettings["Black_List_ID"] ?? "";
            IEnumerable<string> strs = black_string.Split(',');
            foreach(var str in strs){
                bool ret = uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId);
                if (ret)
                {
                    blackListCANIds.Add(canId);
                }
            }

            if (blackListCANIds.Count > 0)
            {
                string canIds = "";
                foreach (uint id in blackListCANIds)
                {
                    canIds += $" 0x{id:X}";
                }
                LOG.WriteLine($"Black list ID = {canIds}");
            }
        }

        private void LoadWhiteListId()
        {
            string white_string = ConfigurationManager.AppSettings["White_List_ID"] ?? "";
            IEnumerable<string> strs = white_string.Split(',');
            foreach (var str in strs)
            {
                bool ret = uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId);
                if (ret)
                {
                    whiteListCANIds.Add(canId);
                }
            }

            if (whiteListCANIds.Count > 0)
            {
                string canIds = "";
                foreach (uint id in whiteListCANIds)
                {
                    canIds += $" 0x{id:X}";
                }
                LOG.WriteLine($"White list ID = {canIds}");
            }
        }


        // Hàm xác định kênh INFO
        private void IdentifyInfoChannel(string logFilePath)
        {
            using (StreamReader reader = new StreamReader(logFilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        uint canId = Convert.ToUInt32(match.Groups[4].Value, 16);
                        int channel = int.Parse(match.Groups[2].Value);

                        // Kiểm tra xem có phải CAN ID 504 không để xác định kênh INFO
                        if (canId == 0x504)
                        {
                            InfoChannel = channel; // Lưu lại channel của kênh INFO
                            LOG.WriteLine($"INFO channel detected: {InfoChannel}");
                            break; // Đã tìm thấy channel INFO, không cần tiếp tục
                        }
                    }
                }
            }

            if (InfoChannel == -1)
            {
                Console.WriteLine("No INFO channel detected from CAN ID 504.");
            }
        }

        public bool Connect()
        {
            if (Connected)
            {
                return true;
            }

            XLDefine.XL_Status status;

            // Mở driver
            status = xlDriver.XL_OpenDriver();
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine("Error opening driver: " + status);
                return false;
            }

            // Mở cổng giao tiếp CAN với cấu hình phù hợp
            status = xlDriver.XL_OpenPort(ref portHandle, "CAN", AccessMask, ref permissionMask, BufferSize, XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine("Error opening portHandle: " + portHandle + ", write channel: " + AccessMask + ", status: " + status);
                ClosePort();
                return false;
            }

            // Thiết lập bitrate cho CAN (500 kbit/s)
            status = xlDriver.XL_CanSetChannelBitrate(portHandle, AccessMask, Bitrate);
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine("Error setting portHandle: " + portHandle + ", bitrate: " + Bitrate + ", write channel: " + AccessMask + ", status: " + status);
                ClosePort();
                return false;
            }

            // Kích hoạt kênh CAN
            status = xlDriver.XL_ActivateChannel(portHandle, AccessMask, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN, XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                LOG.WriteLine("Error activating portHandle: " + portHandle + ", write channel: " + AccessMask + ", status: " + status);
                ClosePort();
                return false;
            }
            Connected = true;

            LOG.WriteLine("Connect OK to write channel: " + AccessMask + ", portHandle: "+ portHandle);
            return true;
        }

        public XLDefine.XL_Status SendCommands(uint canId, string dataStr)
        {
            Message message = new Message(canId, dataStr);
            // Tạo CAN message để phát lại sử dụng xl_can_msg
            XLClass.xl_event xlEvent = new XLClass.xl_event();
            xlEvent.tagData.can_Msg.id = message.id;
            xlEvent.tagData.can_Msg.dlc = message.dlc;
            if (message.id == 0x3C4)
            {
                message.data[message.dlc - 1] = 0;
            }
            xlEvent.tagData.can_Msg.data = message.data;

            xlEvent.tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG;
            //printCanMessage(xlEvent);

            // Gửi CAN message qua mạng CAN
            XLClass.xl_event_collection events = new XLClass.xl_event_collection(0);
            events.xlEvent.Add(xlEvent);
            events.messageCount = (uint)events.xlEvent.Count;

            return xlDriver.XL_CanTransmit(portHandle, accessMask, events);
        }

        public XLDefine.XL_Status SendCommands(Message message)
        {
            // Tạo CAN message để phát lại sử dụng xl_can_msg
            XLClass.xl_event xlEvent = new XLClass.xl_event();
            xlEvent.tagData.can_Msg.id = message.id;
            xlEvent.tagData.can_Msg.dlc = message.dlc;
            if (message.id == 0x3C4)
            {
                message.data[message.dlc - 1] = 0;
            }
            xlEvent.tagData.can_Msg.data = message.data;

            xlEvent.tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG;
            //printCanMessage(xlEvent);

            // Gửi CAN message qua mạng CAN
            XLClass.xl_event_collection events = new XLClass.xl_event_collection(0);
            events.xlEvent.Add(xlEvent);
            events.messageCount = (uint)events.xlEvent.Count;

            return xlDriver.XL_CanTransmit(portHandle, accessMask, events);
        }
    }
}