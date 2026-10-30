using System;

namespace TokenDock
{
    class QuotaWindow
    {
        public string Label;
        public double Used;
        public double? Resets;
        public double? DurationMinutes;
        public double Remaining
        {
            get
            {
                return Math.Max(0, Math.Min(100, 100 - Used));
            }
        }

        public bool Expired
        {
            get
            {
                return Resets.HasValue && Resets.Value <= SystemClock.UnixNow();
            }
        }

        public string Detail()
        {
            if (Expired)
                return Label + "：等待新额度";
            string reset = Resets.HasValue ? " · " + SystemClock.FromUnix(Resets.Value).ToLocalTime().ToString("MM-dd HH:mm") + " 重置" : "";
            return Label + "：剩余 " + Remaining.ToString("0.#") + "%" + reset;
        }
    }
}
