using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    sealed class OrderChoice
    {
        public string Key, Label;
        public override string ToString()
        {
            return Label;
        }
    }

    static class TaskbarOrder
    {
        public static List<string> Normalize(IEnumerable<string> keys)
        {
            return (keys ?? new[]
            {
                "codex",
                "claude",
                "deepseek"
            }

            ).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).Take(512).ToList();
        }

        public static List<T> Sort<T>(IEnumerable<T> items, IEnumerable<string> keys, Func<T, string> identity)
        {
            var order = Normalize(keys);
            return items.Select((item, index) => new { Item = item, Index = index, Position = order.IndexOf(identity(item)) }).OrderBy(row => row.Position < 0 ? int.MaxValue : row.Position).ThenBy(row => row.Index).Select(row => row.Item).ToList();
        }

        public static List<OrderChoice> Choices(UsageMonitor monitor, IEnumerable<string> order)
        {
            var choices = new List<OrderChoice>
            {
                new OrderChoice
                {
                    Key = "codex",
                    Label = "Codex"
                },
                new OrderChoice
                {
                    Key = "claude",
                    Label = "Claude Code"
                },
                new OrderChoice
                {
                    Key = "deepseek",
                    Label = "DeepSeek"
                }
            };
            foreach (var device in monitor.Devices.OrderBy(d => d.name))
                choices.Add(new OrderChoice { Key = "device:" + device.Identity, Label = (device.name ?? "蓝牙设备").Trim() + (device.connected == false ? "（未连接）" : "") });
            foreach (string key in Normalize(order))
                if (!choices.Any(choice => choice.Key == key))
                    choices.Add(new OrderChoice { Key = key, Label = "暂未发现的设备（保留位置）" });
            return Sort(choices, order, choice => choice.Key);
        }

        public static bool Move<T>(IList<T> items, int from, int to)
        {
            if (from < 0 || from >= items.Count || to < 0 || to >= items.Count || from == to)
                return false;
            T item = items[from];
            items.RemoveAt(from);
            items.Insert(to, item);
            return true;
        }
    }
}
