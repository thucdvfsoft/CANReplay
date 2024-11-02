using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using vxlapi_NET;
using static vxlapi_NET.XLClass;

namespace CANReplay.Apps
{
    public class Message
    {
        public uint id;
        public ushort dlc;
        public byte[] data;

        public Message(uint _id, string _data)
        {
            id = _id;
            // Tách dữ liệu byte
            string[] dataBytes = _data.Trim().Split(' ');
            dlc = (ushort)dataBytes.Length;

            //ushort dlc = (ushort)dataBytes.Length;
            data = new byte[dlc];
            for (int i = 0; i < dlc; i++)
            {
                data[i] = Convert.ToByte(dataBytes[i], 16);
            }
        }
    }
}
