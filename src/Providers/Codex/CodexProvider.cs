using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    class CodexProvider : IUsageProvider
    {
        public string Id
        {
            get
            {
                return "codex";
            }
        }

        public string Name
        {
            get
            {
                return "Codex";
            }
        }

        public async Task<UsageSnapshot> ReadAsync(Settings settings)
        {
            string executable = CodexAppServerClient.Locate(settings.CodexPath);
            if (executable == null)
                throw new InvalidOperationException("找不到 codex.exe，请检查 Codex CLI 路径并登录。");
            try
            {
                return Parse(await CodexAppServerClient.Read(executable));
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                throw new InvalidOperationException("无法读取 Codex 额度，请检查本机登录和网络。");
            }
        }

        public static UsageSnapshot Parse(object root)
        {
            var p = new UsageSnapshot
            {
                Name = "Codex",
                Updated = DateTime.Now,
                Status = "无额度数据"
            };
            var buckets = Json.Get(root, "rateLimitsByLimitId") as IDictionary<string, object>;
            var list = new List<KeyValuePair<string, object>>();
            if (buckets != null && buckets.Count > 0)
            {
                if (buckets.ContainsKey("codex"))
                    list.Add(new KeyValuePair<string, object>("codex", buckets["codex"]));
                list.AddRange(buckets.Where(kv => kv.Key != "codex"));
            }
            else if (Json.Get(root, "rateLimits") != null)
                list.Add(new KeyValuePair<string, object>("codex", Json.Get(root, "rateLimits")));
            foreach (var b in list)
                foreach (string k in new[]
                {
                    "primary",
                    "secondary"
                }

                )
                {
                    object w = Json.Get(b.Value, k);
                    double? minutes = Json.Number(Json.Get(w, "windowDurationMins"));
                    string label = minutes.HasValue ? (minutes.Value >= 1440 ? (minutes.Value / 1440).ToString("0.#") + "天" : (minutes.Value / 60).ToString("0.#") + "小时") : (k == "primary" ? "主要额度" : "次要额度");
                    if (list.Count > 1)
                        label = b.Key + " · " + label;
                    QuotaWindow q = QuotaParser.Read(w, label, "usedPercent", "resetsAt");
                    if (q != null)
                    {
                        q.DurationMinutes = minutes;
                        p.Quotas.Add(q);
                    }
                }

            p.Detail = p.Quotas.Count == 0 ? "账户没有返回可显示的订阅额度。请确认 Codex 已使用 ChatGPT 账户登录。" : string.Join("\n", p.Quotas.Select(q => q.Detail()));
            double? tokens = Json.Number(Json.At(root, "tokenActivity", "summary", "lifetimeTokens"));
            p.LifetimeTokens = Json.Integer(Json.At(root, "tokenActivity", "summary", "lifetimeTokens"));
            if (tokens.HasValue)
                p.Detail += "\n账户累计 Token：" + tokens.Value.ToString("N0");
            if (p.Quotas.Count > 0)
                p.Status = "已连接";
            return p;
        }
    }
}
