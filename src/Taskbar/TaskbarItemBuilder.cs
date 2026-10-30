using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    static class TaskbarItemBuilder
    {
        public static List<TaskbarItem> Build(UsageSnapshot codex, UsageSnapshot claude, UsageSnapshot deepseek, IEnumerable<BluetoothDevice> devices, bool batteryFresh, Settings settings = null)
        {
            settings = settings ?? new Settings();
            var items = new List<TaskbarItem>();
            if (settings.ShowCodex)
                AddUsage(items, codex, "codex", "Codex", settings.CodexMetric);
            if (settings.ShowClaude)
                AddUsage(items, claude, "claude", "Claude", settings.ClaudeMetric);
            if (settings.ShowDeepSeek && deepseek != null && deepseek.Updated.HasValue && !deepseek.Stale && !string.IsNullOrEmpty(deepseek.Status) && (deepseek.Status.StartsWith("¥") || deepseek.Status.StartsWith("$")))
                items.Add(new TaskbarItem { Key = "deepseek", Label = "DS", Value = deepseek.Status, Tooltip = "DeepSeek · 可用余额" });
            if (settings.ShowDevices && batteryFresh && devices != null)
            {
                foreach (var device in devices.Where(d => d.connected == true && (d.charging == true || (d.battery.HasValue && d.battery.Value >= 0 && d.battery.Value <= 100))).OrderBy(d => d.name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if (settings.HiddenDeviceIds.Contains(device.Identity))
                        continue;
                    string name = string.IsNullOrWhiteSpace(device.name) ? "设备" : device.name.Trim();
                    items.Add(new TaskbarItem { Key = "device:" + device.Identity, Label = name.Length > 12 ? name.Substring(0, 11) + "…" : name, Value = device.charging == true ? "充电中" : device.battery.Value.ToString("0") + "%", Battery = device.battery, Tooltip = name, IconKind = DeviceIconResolver.ResolveDevice(device) });
                }
            }

            return TaskbarOrder.Sort(items, settings.ItemOrder, item => item.Key);
        }

        static void AddUsage(List<TaskbarItem> items, UsageSnapshot snapshot, string key, string label, string metric)
        {
            if (snapshot == null || snapshot.Stale)
                return;
            if (metric == "tokens")
            {
                long? tokens = key == "codex" ? snapshot.LifetimeTokens : MetricFormatter.SessionTokens(snapshot);
                if (tokens.HasValue)
                    items.Add(new TaskbarItem { Key = key, Label = label, Value = MetricFormatter.CompactTokens(tokens.Value), Tooltip = label + (key == "codex" ? " · 账户累计 Token" : " · 最近会话 Token") });
                return;
            }

            var quota = MetricFormatter.SelectQuota(snapshot, metric);
            if (quota != null)
                items.Add(new TaskbarItem { Key = key, Label = label, Value = quota.Remaining.ToString("0") + "%", Tooltip = label + " · " + quota.Label });
        }
    }
}
