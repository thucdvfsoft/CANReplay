using CANReplay.Apps;
using CANReplay.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CANReplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string logFilePath = "C:\\Users\\ThucDV\\Downloads\\New folder\\VF6_US_0046_6A.2.8.2V1_PT_CH_BD_IF_hud failed_2024_09_24_09_49_59.ASC";          // Đường dẫn tới file log CAN của bạn

        private Replay canReplay = new Replay();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                readAccessMask();
                readBitrate();
                readBufferSize();
                readIsAutoDetect();
                readReadChannel();
                readDelay();

                write_channel_tb.Text = ConfigurationManager.AppSettings["Default_Write_Channel"] ?? "";
                delay_tb.Text = ConfigurationManager.AppSettings["Default_Delay_1"] ?? "0";

                canReplay.StatusLabel = this.status_lb;
                canReplay.CurrentTimeLable = this.current_time_lb;
                canReplay.CurrentTimeMHULable = this.current_time_mhu_lb;
            }
            catch (Exception)
            {
                MessageBox.Show("An unexpected error occurred", "Error", MessageBoxButton.OK);
            }
            finally
            {
                canReplay.StopReplay();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            readAccessMask();
        }

        private void readAccessMask()
        {
            UInt16 writeChannel;
            UInt16.TryParse(write_channel_tb.Text, out writeChannel);
            canReplay.AccessMask = writeChannel;
        }

        private void bitrate_tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            readBitrate();
        }

        private void readBitrate()
        {
            UInt32 bitrate;
            UInt32.TryParse(bitrate_tb.Text, out bitrate);
            canReplay.Bitrate = bitrate;
        }

        private void buffer_tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            readBufferSize();
        }

        private void readBufferSize()
        {
            UInt16 bufferSize;
            UInt16.TryParse(buffer_tb.Text, out bufferSize);
            canReplay.BufferSize = bufferSize;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            readIsAutoDetect();
        }

        private void readIsAutoDetect()
        {
            canReplay.IsAutoDetectInfo = auto_detect_cb.IsChecked.HasValue ? auto_detect_cb.IsChecked.Value : false;
        }
        private void auto_detect_cb_Unchecked(object sender, RoutedEventArgs e)
        {
            readIsAutoDetect();
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            readReadChannel();
        }

        private void readReadChannel()
        {
            UInt16 readChannel;
            UInt16.TryParse(read_channel_tb.Text, out readChannel);
            if (readChannel != 0)
            {
                if (!auto_detect_cb.IsChecked.HasValue || !auto_detect_cb.IsChecked.Value)
                {
                    canReplay.InfoChannel = readChannel;
                }
            }
        }

        private void select_file_bt_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Chọn file CAN log",
                Filter = "CAN log files (*.asc;*.log)|*.asc;*.log|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                selected_path_tb.Text = selectedFilePath;
            }
        }

        private void start_bt_Click(object sender, RoutedEventArgs e)
        {
            if (selected_path_tb.Text.Any())
            {
                if (!canReplay.Connected)
                {
                    if (!canReplay.Connect())
                    {
                        return;
                    }
                }

                connected_lb.Content = "Connected";
                canReplay.StartReplay(selected_path_tb.Text);
                status_lb.Content = "started";
            }
        }

        private void selected_path_tb_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private bool isPaused = false;
        private void pause_bt_Click(object sender, RoutedEventArgs e)
        {
            if (isPaused)
            {
                canReplay.ResumeReplay();
                status_lb.Content = "Resumed";
                pause_bt.Content = "Pause";
                isPaused = false;
            }
            else
            {
                canReplay.PauseReplay();
                status_lb.Content = "Paused";
                pause_bt.Content = "Resume";
                isPaused = true;
            }
        }

        private void stop_bt_Click(object sender, RoutedEventArgs e)
        {
            status_lb.Content = "Stoped";
            current_time_lb.Content = "0";
            current_time_mhu_lb.Content = "0";
            canReplay.StopReplay();
            connected_lb.Content = "Disconnected";
        }

        private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {

        }

        private void delay_tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            readDelay();
        }

        private void readDelay()
        {
            UInt16 delay;
            UInt16.TryParse(delay_tb.Text, out delay);
            canReplay.Delay = delay;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            NewWindow newWindow = new NewWindow();
            newWindow.SetReplay(canReplay);
            newWindow.Show(); // Use ShowDialog() if you want a modal window
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if(canReplay.Connect())
            {
                connected_lb.Content = "Connected";
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (canReplay.ClosePort())
            {
                connected_lb.Content = "Disconnected";
            }
        }
    }

}
