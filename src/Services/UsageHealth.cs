using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    sealed class UsageHealth
    {
        public string Label, Hint;
        public bool IsIssue;

        public static UsageHealth Read(UsageSnapshot snapshot, DateTime now, int refreshSeconds)
        {
            if (snapshot == null)
                return State("暂无数据", "等待首次读取", false);
            if (snapshot.Status == "读取失败")
                return State("读取失败", "检查登录、网络或连接设置后刷新", true);
            if (snapshot.Stale || (snapshot.Updated.HasValue && (now - snapshot.Updated.Value).TotalSeconds > Math.Max(600, refreshSeconds * 3)))
                return State("数据过期", snapshot.Name == "Claude Code" ? "上次结果仅供参考，继续使用 Claude Code 后刷新" : "上次结果仅供参考，请刷新", true);
            if (snapshot.Quotas.Count > 0 && snapshot.Quotas.All(q => q.Expired))
                return State("等待更新", "额度周期已结束，等待平台返回新额度", true);
            if (snapshot.Status == "未配置" || snapshot.Status == "未连接")
                return State(snapshot.Status, "在连接设置中接入后显示", false);
            if (!snapshot.Updated.HasValue)
                return State("读取中", "等待首次成功读取", false);
            if (snapshot.Quotas.Count == 0 && snapshot.Balances.Count == 0 && !snapshot.LifetimeTokens.HasValue && !MetricFormatter.SessionTokens(snapshot).HasValue)
                return State("暂无指标", "已读取，但来源尚未提供可显示的指标", false);
            return State("已更新", "", false);
        }

        static UsageHealth State(string label, string hint, bool issue)
        {
            return new UsageHealth { Label = label, Hint = hint, IsIssue = issue };
        }

        public static List<string> Issues(UsageMonitor monitor, string placement, DateTime now)
        {
            var result = new List<string>();
            var settings = monitor.Config;
            var snapshots = new[] { monitor.Codex, monitor.Claude, monitor.DeepSeek };
            var enabled = new[] { settings.ShowCodex, settings.ShowClaude, settings.ShowDeepSeek };
            for (int i = 0; i < snapshots.Length; i++)
            {
                var health = Read(snapshots[i], now, settings.RefreshSeconds);
                if (enabled[i] && health.IsIssue)
                    result.Add(snapshots[i].Name + "：" + health.Label);
            }
            if (settings.ShowDevices && (monitor.BluetoothStatus ?? "").StartsWith("设备扫描失败"))
                result.Add("设备：读取失败，上次电量仅供参考");
            if (settings.ShowBar && !string.IsNullOrEmpty(placement) && (placement.Contains("空间不足") || placement.Contains("失败") || placement.Contains("被遮挡") || placement.Contains("不支持") || placement.Contains("未找到")))
                result.Add(placement);
            return result;
        }
    }
}
