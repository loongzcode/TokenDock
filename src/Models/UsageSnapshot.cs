using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    class UsageSnapshot
    {
        public string Name, Status, Detail;
        public DateTime? Updated;
        public List<QuotaWindow> Quotas = new List<QuotaWindow>();
        public bool Stale;
        public long? LifetimeTokens, InputTokens, OutputTokens;
        public List<BalanceInfo> Balances = new List<BalanceInfo>();
        public static UsageSnapshot Failed(UsageSnapshot previous, string message)
        {
            return new UsageSnapshot
            {
                Name = previous.Name,
                Status = "读取失败",
                Detail = message + (previous.Updated.HasValue ? "\n上次成功：" + previous.Updated.Value.ToString("MM-dd HH:mm:ss") : ""),
                Quotas = previous.Quotas,
                Updated = previous.Updated,
                Stale = true,
                LifetimeTokens = previous.LifetimeTokens,
                InputTokens = previous.InputTokens,
                OutputTokens = previous.OutputTokens,
                Balances = previous.Balances
            };
        }

        public string Compact
        {
            get
            {
                var q = Quotas.FirstOrDefault(x => !x.Expired);
                if (q != null)
                    return (Stale ? "旧 " : "") + q.Remaining.ToString("0") + "%";
                if (Quotas.Count > 0)
                    return "待刷新";
                return string.IsNullOrEmpty(Status) ? "--" : Status;
            }
        }

        public double? LowRemaining
        {
            get
            {
                var a = Quotas.Where(q => !q.Expired).ToArray();
                return !Stale && a.Length > 0 ? (double? )a.Min(q => q.Remaining) : null;
            }
        }
    }
}
