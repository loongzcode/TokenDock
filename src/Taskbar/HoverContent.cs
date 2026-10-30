using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    sealed class HoverContent
    {
        public string Title, Footer;
        public readonly List<string> Lines = new List<string>();
        public static HoverContent Build(string key, UsageMonitor monitor, DateTime now)
        {
            return Build(key, monitor.Codex, monitor.Claude, monitor.DeepSeek, monitor.Devices, monitor.BluetoothUpdated, now);
        }

        public static HoverContent Build(string key, UsageSnapshot codex, UsageSnapshot claude, UsageSnapshot deepseek, IEnumerable<BluetoothDevice> devices, DateTime? bluetoothUpdated, DateTime now)
        {
            var content = new HoverContent();
            if (key.StartsWith("device:"))
            {
                var device = (devices ?? new BluetoothDevice[0]).FirstOrDefault(d => "device:" + d.Identity == key);
                if (device == null)
                    return null;
                content.Title = (device.name ?? "蓝牙设备").Trim();
                content.Lines.Add(device.connected == true ? "已连接" : device.connected == false ? "未连接" : "连接状态未知");
                content.Lines.Add(device.charging == true ? "充电中" : device.battery.HasValue ? "电量 " + device.battery.Value.ToString("0") + "%" : "电量未知");
                if (!string.IsNullOrWhiteSpace(device.source))
                    content.Lines.Add(device.source);
                content.Footer = Updated(bluetoothUpdated, now, !bluetoothUpdated.HasValue);
                return content;
            }

            var snapshot = key == "codex" ? codex : key == "claude" ? claude : key == "deepseek" ? deepseek : null;
            if (snapshot == null)
                return null;
            content.Title = snapshot.Name;
            double unixNow = (now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            foreach (var quota in snapshot.Quotas)
            {
                bool expired = quota.Resets.HasValue && quota.Resets.Value <= unixNow;
                content.Lines.Add(quota.Label + (expired ? " · 等待额度更新" : " · 剩余 " + quota.Remaining.ToString("0.#") + "%"));
                if (quota.Resets.HasValue && !expired)
                    content.Lines.Add("距重置 " + Countdown(TimeSpan.FromSeconds(quota.Resets.Value - unixNow)));
            }

            foreach (var balance in snapshot.Balances)
                content.Lines.Add(balance.Currency + " 可用余额 " + balance.Amount.ToString("0.##"));
            if (snapshot.LifetimeTokens.HasValue)
                content.Lines.Add("账户累计 Token " + snapshot.LifetimeTokens.Value.ToString("N0"));
            var session = MetricFormatter.SessionTokens(snapshot);
            if (key == "claude" && session.HasValue)
                content.Lines.Add("最近会话 Token " + session.Value.ToString("N0"));
            if (content.Lines.Count == 0)
                content.Lines.Add(snapshot.Status ?? "暂无数据");
            content.Footer = Updated(snapshot.Updated, now, snapshot.Stale);
            return content;
        }

        static string Updated(DateTime? updated, DateTime now, bool stale)
        {
            return (stale ? "旧数据 · " : "") + (updated.HasValue ? "更新于 " + updated.Value.ToLocalTime().ToString("HH:mm:ss") : "尚未成功读取") + " · 单击查看详情";
        }

        public static string Countdown(TimeSpan remaining)
        {
            if (remaining.TotalSeconds <= 0)
                return "等待更新";
            if (remaining.TotalDays >= 1)
                return (int)remaining.TotalDays + "天 " + remaining.Hours + "小时";
            if (remaining.TotalHours >= 1)
                return (int)remaining.TotalHours + "小时 " + remaining.Minutes + "分钟";
            if (remaining.TotalMinutes >= 1)
                return (int)remaining.TotalMinutes + "分钟 " + remaining.Seconds + "秒";
            return Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)) + "秒";
        }
    }
}
