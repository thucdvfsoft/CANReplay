using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CanReproduce.Apps
{
    class LOG
    {
        private static string LOG_NAME = "LOG_CANReplay.txt";
        public static void WriteLine(string data)
        {
            try
            {
                using (StreamWriter logWriter = new StreamWriter(LOG_NAME, true))
                {
                    logWriter.WriteLine(data);
                }
            }
            catch (Exception)
            {
                
            }
        }
    }
}
