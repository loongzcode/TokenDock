using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace TokenDock
{
    class DeepSeekProvider : IUsageProvider
    {
        public string Id
        {
            get
            {
                return "deepseek";
            }
        }

        public string Name
        {
            get
            {
                return "DeepSeek";
            }
        }

        public async Task<UsageSnapshot> ReadAsync(Settings settings)
        {
            string key;
            try
            {
                key = settings.RevealKey();
            }
            catch
            {
                throw new InvalidOperationException("DeepSeek Key 无法解密，请重新填写。");
            }

            if (string.IsNullOrEmpty(key))
                return new UsageSnapshot
                {
                    Name = Name,
                    Status = "未配置",
                    Detail = "在连接设置中填写 DeepSeek API Key，即可查询余额。"
                };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                using (var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15)
                }

                )
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    using (var response = await client.GetAsync("https://api.deepseek.com/user/balance"))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException("DeepSeek 余额读取失败，HTTP " + (int)response.StatusCode + "。");
                        return Parse(Json.Read(await response.Content.ReadAsStringAsync()));
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                throw new InvalidOperationException("DeepSeek 暂时不可用，请检查网络或 Key。");
            }
        }

        public static UsageSnapshot Parse(object root)
        {
            var lines = new List<string>();
            var balances = new List<BalanceInfo>();
            foreach (var row in Json.Items(Json.Get(root, "balance_infos")))
            {
                decimal total;
                string s = Json.Text(Json.Get(row, "total_balance"));
                if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out total))
                    continue;
                string cur = Json.Text(Json.Get(row, "currency"));
                balances.Add(new BalanceInfo { Currency = cur, Amount = total });
                string symbol = cur == "CNY" ? "¥" : cur == "USD" ? "$" : cur + " ";
                lines.Add(symbol + total.ToString("0.00", CultureInfo.InvariantCulture));
            }

            if (lines.Count == 0)
                throw new InvalidDataException("余额响应中没有有效金额。");
            return new UsageSnapshot
            {
                Name = "DeepSeek",
                Status = string.Join(" / ", lines),
                Updated = DateTime.Now,
                Balances = balances,
                Detail = "可用余额：" + string.Join(" / ", lines) + "\n余额来自官方 API。按模型和缓存分别计费，不换算成固定剩余 Token。\n全账户历史 Token 未接入；本版仅显示真实余额。"
            };
        }
    }
}
