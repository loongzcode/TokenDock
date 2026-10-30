using System;
using System.Collections.Generic;
using System.Globalization;

namespace TokenDock
{
    class AlertEngine
    {
        readonly Dictionary<string, bool> active = new Dictionary<string, bool>();
        readonly QuotaRecoveryTracker recovery = new QuotaRecoveryTracker();
        public List<AlertMessage> Evaluate(Settings settings, IEnumerable<UsageSnapshot> providers, IEnumerable<BluetoothDevice> devices, bool batteryFresh)
        {
            var messages = recovery.Evaluate(settings, providers, DateTime.UtcNow);
            if (!settings.AlertsEnabled)
                return messages;
            foreach (var provider in providers)
            {
                if (provider == null || provider.Stale)
                    continue;
                if (settings.QuotaAlertsEnabled)
                    foreach (var quota in provider.Quotas)
                    {
                        if (quota.Expired)
                            continue;
                        string key = "quota:" + provider.Name + ":" + quota.Label + ":" + (quota.Resets.HasValue ? quota.Resets.Value.ToString(CultureInfo.InvariantCulture) : "unknown");
                        Add(messages, key, quota.Remaining <= settings.QuotaAlertPercent, provider.Name + " 额度提醒", quota.Label + "剩余 " + quota.Remaining.ToString("0.#") + "%。");
                    }

                if (settings.BalanceAlertsEnabled && provider.Updated.HasValue)
                    foreach (var balance in provider.Balances)
                    {
                        if (balance.Currency != "CNY" && balance.Currency != "USD")
                            continue;
                        decimal threshold = balance.Currency == "CNY" ? settings.CnyBalanceAlert : settings.UsdBalanceAlert;
                        Add(messages, "balance:" + provider.Name + ":" + balance.Currency, balance.Amount <= threshold, provider.Name + " 余额提醒", "当前余额 " + balance.Amount.ToString("0.00") + " " + balance.Currency + "。");
                    }
            }

            if (settings.BatteryAlertsEnabled && batteryFresh && devices != null)
                foreach (var device in devices)
                {
                    if (device.connected != true || device.charging == true || !device.battery.HasValue || device.battery < 0 || device.battery > 100)
                        continue;
                    Add(messages, "battery:" + device.Identity, device.battery <= settings.BatteryAlertPercent, "设备低电量", (device.name ?? "蓝牙设备").Trim() + " 剩余 " + device.battery.Value.ToString("0") + "%。");
                }

            return messages;
        }

        void Add(List<AlertMessage> messages, string key, bool low, string title, string text)
        {
            bool wasLow;
            active.TryGetValue(key, out wasLow);
            active[key] = low;
            if (low && !wasLow)
                messages.Add(new AlertMessage { Key = key, Title = title, Text = text });
        }
    }
}
