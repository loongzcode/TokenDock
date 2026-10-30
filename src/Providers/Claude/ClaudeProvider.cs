using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    class ClaudeProvider : IUsageProvider
    {
        public string Id
        {
            get
            {
                return "claude";
            }
        }

        public string Name
        {
            get
            {
                return "Claude Code";
            }
        }

        public Task<UsageSnapshot> ReadAsync(Settings settings)
        {
            if (!File.Exists(AppPaths.ClaudeSnapshot))
                return Task.FromResult(new UsageSnapshot { Name = Name, Status = "未连接", Detail = "在连接设置中配置 Claude Code 状态栏桥接。" });
            try
            {
                return Task.FromResult(Parse(Json.Read(File.ReadAllText(AppPaths.ClaudeSnapshot)), File.GetLastWriteTime(AppPaths.ClaudeSnapshot)));
            }
            catch
            {
                throw new InvalidOperationException("Claude 快照暂时无法读取，下次刷新会重试。");
            }
        }

        public static string Configuration(string executable)
        {
            return Json.Write(new { statusLine = new { type = "command", command = "\"" + executable.Replace('\\', '/') + "\" --claude-statusline" } });
        }

        public static UsageSnapshot Parse(object root, DateTime modified)
        {
            var p = new UsageSnapshot
            {
                Name = "Claude Code",
                Updated = modified,
                Status = "待更新",
                Stale = (DateTime.Now - modified).TotalMinutes > 10
            };
            object r = Json.Get(root, "rate_limits");
            foreach (string k in new[]
            {
                "five_hour",
                "seven_day",
                "spend_limit"
            }

            )
            {
                var q = QuotaParser.Read(Json.Get(r, k), k == "five_hour" ? "5小时" : k == "seven_day" ? "7天" : "消费限额", "used_percentage", "resets_at");
                if (q != null)
                {
                    q.DurationMinutes = k == "five_hour" ? (double? )300 : k == "seven_day" ? (double? )10080 : null;
                    p.Quotas.Add(q);
                }
            }

            p.Detail = p.Quotas.Count > 0 ? string.Join("\n", p.Quotas.Select(q => q.Detail())) : "Claude Code 尚未提供套餐额度。需支持 rate_limits 的版本，并在会话中收到过响应。";
            var i = Json.Number(Json.At(root, "context_window", "total_input_tokens"));
            var o = Json.Number(Json.At(root, "context_window", "total_output_tokens"));
            p.InputTokens = Json.Integer(Json.At(root, "context_window", "total_input_tokens"));
            p.OutputTokens = Json.Integer(Json.At(root, "context_window", "total_output_tokens"));
            if (i.HasValue || o.HasValue)
                p.Detail += "\n最近会话 Token：输入 " + (i.HasValue ? i.Value.ToString("N0") : "--") + " / 输出 " + (o.HasValue ? o.Value.ToString("N0") : "--");
            if (p.Stale)
                p.Detail += "\n数据超过 10 分钟未更新，仅供参考；继续使用 Claude Code 后刷新。";
            if (p.Quotas.Count > 0)
                p.Status = "已连接";
            return p;
        }

        public static object SanitizeSnapshot(object root)
        {
            var result = new Dictionary<string, object>();
            var rates = new Dictionary<string, object>();
            foreach (string key in new[]
            {
                "five_hour",
                "seven_day",
                "spend_limit"
            }

            )
            {
                object source = Json.At(root, "rate_limits", key);
                var fields = new Dictionary<string, object>();
                foreach (string f in new[]
                {
                    "used_percentage",
                    "resets_at"
                }

                )
                {
                    var n = Json.Number(Json.Get(source, f));
                    if (n.HasValue)
                        fields[f] = n.Value;
                }

                if (fields.Count > 0)
                    rates[key] = fields;
            }

            result["rate_limits"] = rates;
            var tokens = new Dictionary<string, object>();
            foreach (string k in new[]
            {
                "total_input_tokens",
                "total_output_tokens"
            }

            )
            {
                var n = Json.Number(Json.At(root, "context_window", k));
                if (n.HasValue && n.Value >= 0)
                    tokens[k] = n.Value;
            }

            result["context_window"] = tokens;
            return result;
        }
    }
}
