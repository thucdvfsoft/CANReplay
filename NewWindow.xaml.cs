using CANReplay.Apps;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using vxlapi_NET;

namespace CANReplay
{
    /// <summary>
    /// Interaction logic for NewWindow.xaml
    /// </summary>
    public partial class NewWindow : Window
    {
        private Replay canReplay;
        private int delay = 1;
        public NewWindow()
        {
            InitializeComponent();
            delay2_tb.Text = ConfigurationManager.AppSettings["Default_Delay_2"] ?? "1";
        }

        public void SetReplay(Replay replay)
        {
            canReplay = replay;
            setLabel();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(delay2_tb.Text, out delay);
            asyncStart();
        }

        private void setLabel()
        {
            if (canReplay != null)
            {
                if (canReplay.Connected)
                {
                    connected2_lb.Content = "Connected";
                }
                else
                {
                    connected2_lb.Content = "Disconnected";
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (canReplay != null)
            {
                canReplay.Connect();
                setLabel();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (canReplay != null)
            {
                canReplay.ClosePort();
                setLabel();
            }
        }

        private List<Message> getCommands(string txt)
        {
            List<Message> commands = new List<Message>();
            string[] lines = txt.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, 2);
                if (parts.Length > 1)
                {
                    string idStr = parts[0];
                    string data = parts.Length > 1 ? parts[1] : "";
                    uint id = Convert.ToUInt32(idStr, 16);
                    commands.Add(new Message(id, data));
                }
            }

            return commands;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

        }

        private async void asyncStart()
        {
            string txt = command_tb.Text;
            status2_lb.Content = "running";
            await Task.Run(async () =>
            {
                var commands = getCommands(txt);
                for (int i = 0; i < commands.Count;i++)
                {
                    XLDefine.XL_Status status = canReplay.SendCommands(commands[i]);
                    if (i < (commands.Count - 1))
                    {
                        await Task.Delay(delay * 1000);
                    }
                }
            });
            status2_lb.Content = "Stopped";
            canReplay.ClosePort();
            setLabel();
        }
    }
}
