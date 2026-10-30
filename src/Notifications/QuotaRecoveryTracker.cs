using System;
using System.Collections.Generic;
using System.Globalization;

namespace TokenDock
{
    sealed class QuotaRecoveryTracker
    {
        sealed class Observation
        {
            public double Remaining;
            public double? Reset;
            public DateTime Updated;
        }

        readonly Dictionary<string, Observation> previous = new Dictionary<string, Observation>();
        bool? wasEnabled;
        public List<AlertMessage> Evaluate(Settings settings, IEnumerable<UsageSnapshot> providers, DateTime now)
        {
            var messages = new List<AlertMessage>();
            bool enabled = settings.AlertsEnabled && settings.QuotaRecoveryAlertsEnabled;
            if (wasEnabled.HasValue && wasEnabled != enabled)
                previous.Clear();
            wasEnabled = enabled;
            double unixNow = (now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            foreach (var provider in providers)
            {
                if (provider == null || provider.Stale || !provider.Updated.HasValue)
                    continue;
                foreach (var quota in provider.Quotas)
                {
                    if (double.IsNaN(quota.Used) || double.IsInfinity(quota.Used) || quota.Used < 0 || quota.Used > 100 || (quota.Resets.HasValue && quota.Resets <= unixNow))
                        continue;
                    string key = provider.Name + ":" + quota.Label + ":" + (quota.DurationMinutes.HasValue ? quota.DurationMinutes.Value.ToString(CultureInfo.InvariantCulture) : "");
                    Observation old;
                    if (previous.TryGetValue(key, out old))
                    {
                        if (provider.Updated.Value <= old.Updated)
                            continue;
                        bool increased = quota.Remaining > old.Remaining + 0.01;
                        bool reset = old.Reset.HasValue && quota.Resets.HasValue && old.Reset <= unixNow && quota.Resets > old.Reset;
                        bool recovered = old.Remaining <= settings.QuotaAlertPercent && quota.Remaining > settings.QuotaAlertPercent;
                        if (enabled && increased && (reset || recovered))
                            messages.Add(new AlertMessage { Key = "recovery:" + key, Title = provider.Name + " 额度恢复", Text = quota.Label + (reset ? "周期已更新" : "已恢复可用额度") + "，剩余 " + quota.Remaining.ToString("0.#") + "%。" });
                    }

                    previous[key] = new Observation
                    {
                        Remaining = quota.Remaining,
                        Reset = quota.Resets,
                        Updated = provider.Updated.Value
                    };
                }
            }

            return messages;
        }
    }
}
