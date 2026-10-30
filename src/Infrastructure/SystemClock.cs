using System;

namespace TokenDock
{
    static class SystemClock
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static double UnixNow()
        {
            return (DateTime.UtcNow - Epoch).TotalSeconds;
        }

        public static DateTime FromUnix(double value)
        {
            try
            {
                return Epoch.AddSeconds(value);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }
}
