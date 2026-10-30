using System;
using System.Collections.Generic;
using System.Globalization;

namespace TokenDock
{
    sealed class HistoryCapture
    {
        readonly Dictionary<string, HistoryPoint> last = new Dictionary<string, HistoryPoint>();
        public List<HistoryPoint> Capture(IEnumerable<UsageSnapshot> providers, IEnumerable<BluetoothDevice> devices, DateTime? bluetoothUpdated, DateTime now)
        {
            var points = new List<HistoryPoint>();
            double unixNow = Unix(now);
            foreach (var provider in providers)
            {
                if (provider == null || provider.Stale || !provider.Updated.HasValue)
                    continue;
                double time = Unix(provider.Updated.Value);
                foreach (var quota in provider.Quotas)
                {
                    if (double.IsNaN(quota.Used) || double.IsInfinity(quota.Used) || quota.Used < 0 || quota.Used > 100 || (quota.Resets.HasValue && quota.Resets <= unixNow))
                        continue;
                    string series = "quota:" + provider.Name + ":" + quota.Label + ":" + (quota.DurationMinutes.HasValue ? quota.DurationMinutes.Value.ToString(CultureInfo.InvariantCulture) : "");
                    Add(points, new HistoryPoint { Time = time, Series = series, Label = provider.Name + " · " + quota.Label + "剩余额度", Unit = "%", Value = (decimal)quota.Remaining, Reset = quota.Resets });
                }

                foreach (var balance in provider.Balances)
                    if (balance.Currency == "CNY" || balance.Currency == "USD")
                        Add(points, new HistoryPoint { Time = time, Series = "balance:" + provider.Name + ":" + balance.Currency, Label = provider.Name + " · " + balance.Currency + "余额", Unit = balance.Currency, Value = balance.Amount });
            }

            if (bluetoothUpdated.HasValue && devices != null)
                foreach (var device in devices)
                    if (device.connected == true && device.charging != true && device.battery.HasValue && !double.IsNaN(device.battery.Value) && device.battery >= 0 && device.battery <= 100)
                        Add(points, new HistoryPoint { Time = Unix(bluetoothUpdated.Value), Series = "battery:" + device.Identity, Label = (device.name ?? "蓝牙设备").Trim() + " · 电量", Unit = "%", Value = (decimal)device.battery.Value });
            return points;
        }

        void Add(List<HistoryPoint> points, HistoryPoint point)
        {
            if (!point.Valid)
                return;
            HistoryPoint previous;
            if (last.TryGetValue(point.Series, out previous))
            {
                if (point.Time <= previous.Time)
                    return;
                if (point.Value == previous.Value && point.Reset == previous.Reset && point.Time - previous.Time < 300)
                    return;
            }

            last[point.Series] = point;
            points.Add(point);
        }

        public static double Unix(DateTime value)
        {
            return (value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
