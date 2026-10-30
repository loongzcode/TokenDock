using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TokenDock
{
    static class RegressionTests
    {
        public static string Run()
        {
            int n = 0;
            Action<bool, string> check = delegate (bool value, string title)
            {
                if (!value)
                    throw new Exception("FAIL: " + title);
                n++;
            };
            object c = Json.Read("{\"rateLimits\":{\"primary\":{\"usedPercent\":0}},\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":73,\"windowDurationMins\":300}},\"other\":{\"secondary\":{\"usedPercent\":0,\"windowDurationMins\":10080}}}}");
            UsageSnapshot p = CodexProvider.Parse(c);
            check(p.Quotas.Count == 2 && p.Quotas[0].Remaining == 27, "Codex multi-bucket priority and remaining");
            check(p.Quotas[1].Remaining == 100, "zero used is a real value");
            check(CodexProvider.Parse(Json.Read("{\"rateLimits\":{\"primary\":{\"usedPercent\":101}}}")).Quotas.Count == 0, "invalid quota rejected");
            check(CodexProvider.Parse(Json.Read("{}")).Quotas.Count == 0, "missing quota is unavailable");
            UsageSnapshot expired = CodexProvider.Parse(Json.Read("{\"rateLimits\":{\"primary\":{\"usedPercent\":99,\"resetsAt\":1}}}"));
            check(expired.Quotas[0].Expired && expired.Compact == "待刷新" && expired.LowRemaining == null, "expired quota is not fabricated as fresh");
            UsageSnapshot stale = ClaudeProvider.Parse(Json.Read("{\"rate_limits\":{\"five_hour\":{\"used_percentage\":100}}}"), DateTime.Now.AddHours(-1));
            check(stale.Stale && stale.LowRemaining == null && stale.Compact.StartsWith("旧"), "stale data marked");
            UsageSnapshot claude = ClaudeProvider.Parse(Json.Read("{\"rate_limits\":{\"five_hour\":{\"used_percentage\":0},\"seven_day\":{\"used_percentage\":35}},\"context_window\":{\"total_input_tokens\":1200}}"), DateTime.Now);
            check(claude.Quotas.Count == 2 && claude.Quotas[0].Remaining == 100, "Claude optional reset and zero value");
            string clean = Json.Write(ClaudeProvider.SanitizeSnapshot(Json.Read("{\"session_id\":\"secret\",\"transcript_path\":\"private\",\"rate_limits\":{\"five_hour\":{\"used_percentage\":17}},\"context_window\":{\"total_output_tokens\":200},\"prompt\":\"private\"}")));
            check(!clean.Contains("secret") && !clean.Contains("private") && clean.Contains("17"), "Claude snapshot privacy whitelist");
            UsageSnapshot ds = DeepSeekProvider.Parse(Json.Read("{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"0\"},{\"currency\":\"USD\",\"total_balance\":\"12.345\"}]}"));
            check(ds.Status.Contains("¥0.00") && ds.Status.Contains("$12.35") && ds.Quotas.Count == 0, "DeepSeek currencies and no token invention");
            bool invalid = false;
            try
            {
                DeepSeekProvider.Parse(Json.Read("{\"balance_infos\":[]}"));
            }
            catch (InvalidDataException)
            {
                invalid = true;
            }

            check(invalid, "malformed balance fails visibly");
            var cfg = new Settings();
            cfg.SetKey("test-only-key");
            check(!cfg.DeepSeekKey.Contains("test-only-key") && cfg.RevealKey() == "test-only-key", "DPAPI roundtrip");
            cfg.SetKey("");
            check(cfg.RevealKey() == "", "key removal");
            var taskbarRect = new Rectangle(0, 1000, 1920, 48);
            var slot = TaskbarLayout.FindSlot(taskbarRect, new[] { new Rectangle(0, 1000, 160, 48), new Rectangle(700, 1000, 800, 48), new Rectangle(1600, 1000, 320, 48) }, 352, 120, 12, 36);
            check(slot.Left >= 172 && slot.Right <= 688 && slot.Top >= 1000 && slot.Bottom <= 1048, "taskbar slot stays inside a real free area");
            var blocked = TaskbarLayout.FindSlot(taskbarRect, new[] { taskbarRect }, 352, 120, 12, 36);
            check(blocked.IsEmpty, "full taskbar never falls back to floating overlay");
            var compact = TaskbarLayout.FindSlot(new Rectangle(0, 0, 400, 48), new[] { new Rectangle(180, 0, 220, 48) }, 352, 120, 12, 36);
            check(compact.Width >= 120 && compact.Width < 352 && compact.Right < 180, "narrow gap produces compact in-taskbar mode");
            var placementBlocks = new[]
            {
                new Rectangle(0, 1000, 100, 48),
                new Rectangle(600, 1000, 600, 48),
                new Rectangle(1800, 1000, 120, 48)
            };
            var leftSlot = TaskbarLayout.PositionedSlot(taskbarRect, placementBlocks, 352, 120, 12, 28, "left", 0);
            var rightSlot = TaskbarLayout.PositionedSlot(taskbarRect, placementBlocks, 352, 120, 12, 28, "right", 0);
            check(leftSlot.Right <= 588 && rightSlot.Left >= 1212 && rightSlot.Right <= 1788, "left and right alignment avoid taskbar buttons");
            var customSlot = TaskbarLayout.PositionedSlot(taskbarRect, placementBlocks, 352, 120, 12, 28, "custom", 800);
            check(!placementBlocks.Any(r => r.IntersectsWith(customSlot)) && customSlot.Top == 1010, "dragged position snaps into a free taskbar area");
            var exactSlot = TaskbarLayout.PositionedSlot(taskbarRect, placementBlocks, 352, 120, 12, 28, "custom", 1280);
            check(exactSlot.Left == 1280, "free custom position is preserved exactly");
            check(TaskbarLayout.CanDrop(taskbarRect, new Point(600, 990), 48), "drop is accepted near the taskbar");
            check(!TaskbarLayout.CanDrop(taskbarRect, new Point(600, 800), 48) && !TaskbarLayout.CanDrop(taskbarRect, new Point(-10, 1020), 48), "drop outside the taskbar target is cancelled");
            var settingsMonitor = new UsageMonitor(new Settings());
            using (var settingsBar = new TaskbarControl(settingsMonitor))
            using (var ui = new SettingsWindow(settingsMonitor, settingsBar))
            {
                ui.PerformLayout();
                var number = ui.Controls.Find("RefreshSeconds", true).FirstOrDefault();
                check(number != null && number.Width >= 80, "refresh interval input has usable dimensions");
                check(!number.Parent.Controls.Cast<Control>().OfType<Label>().Any(label => label.Bounds.IntersectsWith(number.Bounds)), "settings labels never cover the refresh interval input");
            }

            var liveCodex = CodexProvider.Parse(Json.Read("{\"rateLimits\":{\"primary\":{\"usedPercent\":10}}}"));
            var absent = new UsageSnapshot
            {
                Status = "未连接"
            };
            var onlyConnected = TaskbarItemBuilder.Build(liveCodex, absent, absent, new[] { new BluetoothDevice { connected = true, battery = 100 } }, true);
            check(onlyConnected.Select(item => item.Key).SequenceEqual(new[] { "codex", "device:unknown" }), "unconnected providers are omitted from the taskbar");
            var allConnected = TaskbarItemBuilder.Build(liveCodex, liveCodex, DeepSeekProvider.Parse(Json.Read("{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"1\"}]}")), new[] { new BluetoothDevice { connected = true, battery = 100 } }, true);
            check(TaskbarDrawing.Measure(onlyConnected) < TaskbarDrawing.Measure(allConnected), "hidden providers leave no fixed-width empty cells");
            var zeroQuota = CodexProvider.Parse(Json.Read("{\"rateLimits\":{\"primary\":{\"usedPercent\":100}}}"));
            var zeroBalance = DeepSeekProvider.Parse(Json.Read("{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"0\"}]}"));
            var zeroItems = TaskbarItemBuilder.Build(zeroQuota, null, zeroBalance, new[] { new BluetoothDevice { connected = true, battery = 0 } }, true);
            check(zeroItems.Count == 3 && zeroItems[0].Value == "0%" && zeroItems[1].Value == "¥0.00" && zeroItems[2].Value == "0%", "real zero quota balance and battery stay visible");
            check(TaskbarItemBuilder.Build(expired, null, null, new[] { new BluetoothDevice { connected = false, battery = 80 }, new BluetoothDevice { connected = true, battery = null } }, true).Count == 0, "expired quotas disconnected devices and unknown batteries are omitted");
            check(TaskbarDrawing.Measure(TaskbarItemBuilder.Build(null, null, null, null, false)) == 0, "no available items means no empty taskbar widget");
            return "PASS " + n + " checks";
        }
    }
}
