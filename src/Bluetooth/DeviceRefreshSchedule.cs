using System;

namespace TokenDock
{
    // Coalesces bursts and retries once after Windows has populated battery properties.
    sealed class DeviceRefreshSchedule
    {
        DateTime? due, retry;
        DateTime lastRequest = DateTime.MinValue;
        public void Signal(DateTime now)
        {
            if (!due.HasValue)
                due = now.AddMilliseconds(700);
            retry = now.AddSeconds(10);
        }

        public bool TakeDue(DateTime now)
        {
            if ((now - lastRequest).TotalSeconds < 3)
                return false;
            if (due.HasValue && now >= due.Value)
            {
                due = null;
                lastRequest = now;
                return true;
            }

            if (retry.HasValue && now >= retry.Value)
            {
                retry = null;
                lastRequest = now;
                return true;
            }

            return false;
        }
    }
}
