using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace CANReplay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string logFilePath = @"Error.log";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Bắt các lỗi không được xử lý ở UI thread (WPF thread)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Bắt các lỗi không được xử lý ở non-UI threads (background threads)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        // Xử lý lỗi không được xử lý ở UI thread
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception, "UI Thread Exception");
            MessageBox.Show("An unexpected error occurred", "Error", MessageBoxButton.OK);
            e.Handled = true;  // Để tránh ứng dụng tự động bị đóng, nhưng bạn có thể set thành false để crash
        }

        // Xử lý lỗi không được xử lý ở non-UI threads
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogError(ex, "Non-UI Thread Exception");
            }
        }

        // Ghi log lỗi
        private void LogError(Exception ex, string errorType)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine("==========================================");
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {errorType}");
                    writer.WriteLine(ex.Message);
                    writer.WriteLine(ex.StackTrace);
                    writer.WriteLine("==========================================");
                }
            }
            catch (Exception logEx)
            {
                // Nếu không thể ghi log, bạn có thể hiển thị cảnh báo hoặc bỏ qua
                MessageBox.Show("Failed to write to log file: " + logEx.Message);
            }
        }
    }
}
