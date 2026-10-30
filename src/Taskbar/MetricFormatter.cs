using System;
using System.Globalization;
using System.Linq;

namespace TokenDock
{
    static class MetricFormatter
    {
        public static QuotaWindow SelectQuota(UsageSnapshot snapshot, string mode)
        {
            var quotas = snapshot.Quotas.Where(q => !q.Expired);
            if (mode == "short")
                return quotas.Where(q => q.DurationMinutes.HasValue && q.DurationMinutes.Value < 10080).OrderBy(q => q.DurationMinutes).FirstOrDefault();
            if (mode == "weekly")
                return quotas.Where(q => q.DurationMinutes.HasValue && q.DurationMinutes.Value >= 10080).OrderBy(q => q.DurationMinutes).FirstOrDefault();
            if (mode == "minimum")
                return quotas.OrderBy(q => q.Remaining).FirstOrDefault();
            return quotas.FirstOrDefault();
        }

        public static string CompactTokens(long value)
        {
            if (value >= 1000000000)
                return (value / 1000000000d).ToString("0.#", CultureInfo.InvariantCulture) + "B";
            if (value >= 1000000)
                return (value / 1000000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000)
                return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static long? SessionTokens(UsageSnapshot snapshot)
        {
            if (!snapshot.InputTokens.HasValue && !snapshot.OutputTokens.HasValue)
                return null;
            long input = snapshot.InputTokens ?? 0, output = snapshot.OutputTokens ?? 0;
            return input > long.MaxValue - output ? long.MaxValue : input + output;
        }
    }
}
