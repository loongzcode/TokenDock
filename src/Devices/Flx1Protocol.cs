using System;
using System.Text;

namespace TokenDock
{
    sealed class ReceiverPower
    {
        public double? Battery;
        public bool Charging;
    }

    static class Flx1Protocol
    {
        public static byte[] Query(byte command)
        {
            if (command != 3 && command != 0x0E && command != 0x30)
                throw new ArgumentOutOfRangeException("command", "仅支持已验证的读取命令。");
            var packet = new byte[65];
            packet[1] = 0x55;
            packet[2] = command;
            if (command != 3)
            {
                byte[] header =
                {
                    0xA5,
                    0x0B,
                    0x2E,
                    1,
                    1,
                    1,
                    0,
                    0
                };
                Array.Copy(header, 0, packet, 3, header.Length);
            }

            return packet;
        }

        public static bool Matches(byte[] identity)
        {
            return identity != null && identity.Length == 65 && identity[0] == 0 && identity[1] == 0xAA && identity[2] == 3 && Encoding.ASCII.GetString(identity, 9, 56).StartsWith("YJX.co.mouse263213", StringComparison.Ordinal);
        }

        static bool Envelope(byte[] frame, byte command, byte type)
        {
            return frame != null && frame.Length == 65 && frame[0] == 0 && frame[1] == 0xAA && frame[2] == command && frame[3] == 0xA5 && frame[5] == type && frame[6] == 1 && frame[7] == 1 && frame[8] == 1;
        }

        public static bool Online(byte[] status)
        {
            if (!Envelope(status, 0x0E, 0x2F))
                return false;
            int count = status[12], stage = status[13];
            if (count < 1 || count > 6 || stage < 1 || stage > count)
                return false;
            for (int index = 0; index < count; index++)
            {
                int dpi = status[14 + index * 2] | status[15 + index * 2] << 8;
                if (dpi < 50 || dpi > 50000)
                    return false;
            }

            return true;
        }

        public static ReceiverPower Read(byte[] status, byte[] battery)
        {
            if (!Online(status) || !Envelope(battery, 0x30, 0x0A) || battery[9] > 100 || battery[10] > 1)
                return null;
            bool charging = battery[10] == 1;
            return new ReceiverPower
            {
                Charging = charging,
                Battery = charging ? null : (double? )battery[9]
            };
        }
    }
}
