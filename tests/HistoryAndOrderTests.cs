using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TokenDock
{
    static class HistoryAndOrderTests
    {
        public static string Run()
        {
            int count = 0;
            Action<bool, string> check = delegate (bool valid, string message)
            {
                if (!valid)
                    throw new Exception("FAIL: " + message);
                count++;
            };
            var settings = Settings.FromJson("{}");
            check(settings.HistoryEnabled && settings.QuotaRecoveryAlertsEnabled && settings.ItemOrder.SequenceEqual(new[] { "codex", "claude", "deepseek" }), "old configuration gains history, recovery and order defaults");
            settings.ItemOrder = new List<string>
            {
                "deepseek",
                "",
                "codex",
                "deepseek"
            };
            settings.Normalize();
            check(settings.ItemOrder.SequenceEqual(new[] { "deepseek", "codex" }), "saved order removes empty and duplicate keys");
            var ordered = TaskbarOrder.Sort(new[] { "new-keyboard", "codex", "deepseek", "new-mouse" }, settings.ItemOrder, item => item);
            check(ordered.SequenceEqual(new[] { "deepseek", "codex", "new-keyboard", "new-mouse" }), "known keys reorder while new items retain their relative order");
            var moved = new List<string>
            {
                "a",
                "b",
                "c"
            };
            check(TaskbarOrder.Move(moved, 0, 2) && moved.SequenceEqual(new[] { "b", "c", "a" }), "drag move preserves every item");
            check(!TaskbarOrder.Move(moved, 0, -1) && !TaskbarOrder.Move(moved, 0, 0) && moved.Count == 3, "invalid movement does not delete items");
            DateTime now = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
            double unix = HistoryCapture.Unix(now);
            settings = new Settings();
            var tracker = new QuotaRecoveryTracker();
            var initial = Snapshot(now, 95, unix + 60);
            check(tracker.Evaluate(settings, new[] { initial }, now).Count == 0, "first reading never claims a recovery");
            check(tracker.Evaluate(settings, new[] { initial }, now.AddSeconds(10)).Count == 0, "same reading is not a new observation");
            var reset = Snapshot(now.AddSeconds(61), 0, unix + 3600);
            check(tracker.Evaluate(settings, new[] { reset }, now.AddSeconds(61)).Single().Text.Contains("周期已更新"), "elapsed quota cycle plus returned increase produces a reset alert");
            check(tracker.Evaluate(settings, new[] { reset }, now.AddSeconds(80)).Count == 0, "reset alert is not repeated");
            tracker = new QuotaRecoveryTracker();
            tracker.Evaluate(settings, new[] { initial }, now);
            var early = Snapshot(now.AddSeconds(5), 0, unix + 3600);
            settings.QuotaAlertPercent = 0;
            check(tracker.Evaluate(settings, new[] { early }, now.AddSeconds(5)).Count == 0, "future reset timestamp drift alone is not treated as an elapsed cycle");
            settings = new Settings();
            tracker = new QuotaRecoveryTracker();
            tracker.Evaluate(settings, new[] { Snapshot(now, 100, null) }, now);
            var returned = Snapshot(now.AddSeconds(10), 40, null);
            returned.Stale = true;
            check(tracker.Evaluate(settings, new[] { returned }, now.AddSeconds(10)).Count == 0, "stale recovery data never notifies");
            returned.Stale = false;
            check(tracker.Evaluate(settings, new[] { returned }, now.AddSeconds(10)).Count == 1, "real recovery from low quota works without reset timestamp");
            settings.QuotaRecoveryAlertsEnabled = false;
            tracker.Evaluate(settings, new[] { Snapshot(now.AddSeconds(20), 100, null) }, now.AddSeconds(20));
            settings.QuotaRecoveryAlertsEnabled = true;
            check(tracker.Evaluate(settings, new[] { Snapshot(now.AddSeconds(30), 0, null) }, now.AddSeconds(30)).Count == 0, "reenabling recovery starts a baseline without catchup alerts");
            var capture = new HistoryCapture();
            var quota = Snapshot(now, 100, unix + 300);
            var balance = new UsageSnapshot
            {
                Name = "DeepSeek",
                Updated = now,
                Balances = new List<BalanceInfo>
                {
                    new BalanceInfo
                    {
                        Currency = "CNY",
                        Amount = 0
                    }
                }
            };
            var devices = new[]
            {
                new BluetoothDevice
                {
                    id = "keyboard",
                    name = "Keyboard",
                    connected = true,
                    battery = 0
                }
            };
            var points = capture.Capture(new[] { quota, balance }, devices, now, now);
            check(points.Count == 3 && points.All(point => point.Value == 0), "history keeps actual zero quota, balance and battery");
            check(capture.Capture(new[] { quota, balance }, devices, now, now).Count == 0, "UI change events do not duplicate the same source observation");
            quota.Updated = now.AddSeconds(120);
            check(capture.Capture(new[] { quota }, null, null, now.AddSeconds(120)).Count == 0, "unchanged values are throttled to five minute samples");
            quota.Updated = now.AddSeconds(180);
            quota.Quotas[0].Used = 99;
            check(capture.Capture(new[] { quota }, null, null, now.AddSeconds(180)).Single().Value == 1, "value changes are sampled immediately");
            quota.Updated = now.AddSeconds(181);
            quota.Stale = true;
            check(capture.Capture(new[] { quota }, null, null, now.AddSeconds(181)).Count == 0, "failed provider readings are not history samples");
            devices[0].connected = false;
            check(capture.Capture(new UsageSnapshot[0], devices, now.AddSeconds(200), now.AddSeconds(200)).Count == 0, "disconnected battery is not recorded as live");
            var expired = Snapshot(now, 0, unix - 1);
            check(new HistoryCapture().Capture(new[] { expired }, null, null, now).Count == 0, "expired quota does not fabricate a historical value");
            check(!new HistoryPoint { Time = unix, Series = "a", Label = "A", Unit = "%", Value = 101 }.Valid, "invalid percentage is rejected");
            string serialized = Json.Write(points[0]);
            check(!serialized.Contains("DeepSeekKey") && !serialized.Contains("Detail") && !serialized.Contains("CodexPath"), "history serializes the metric whitelist only");
            var samples = new[]
            {
                Point(unix, 20, unix + 100),
                Point(unix + 60, 10, unix + 100),
                Point(unix + 120, 100, unix + 500),
                Point(unix + 4000, 80, unix + 500)
            };
            check(HistorySeries.Segments(samples).Count == 3, "charts break lines at quota resets and long sampling gaps");
            check(HistorySeries.Select(samples, "quota:test", unix + 60, unix + 120).Count == 2, "chart range filtering includes both boundary samples");
            string directory = Path.Combine(Path.GetTempPath(), "TokenDock-history-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new HistoryStore(directory);
                store.Append(points, now);
                check(store.Read(now.AddMinutes(-1), now.AddMinutes(1)).Count == 3, "daily history round trips actual source samples");
                string day = Path.Combine(directory, now.ToString("yyyy-MM-dd") + ".jsonl");
                File.AppendAllText(day, "{broken\n");
                check(store.Read(now.AddMinutes(-1), now.AddMinutes(1)).Count == 3, "one corrupt record does not hide valid history");
                File.AppendAllText(day, "{incomplete");
                store.Append(new[] { Point(unix + 20, 50, null) }, now);
                check(store.Read(now.AddMinutes(-1), now.AddMinutes(1)).Count == 4, "append isolates an incomplete final line rather than swallowing a new sample");
                store.Append(points, now);
                check(store.Read(now.AddMinutes(-1), now.AddMinutes(1)).Count == 4, "repeated source samples across runs do not duplicate the chart");
                string old = Path.Combine(directory, now.AddDays(-31).ToString("yyyy-MM-dd") + ".jsonl");
                string marker = Path.Combine(directory, "unrelated.txt");
                File.WriteAllText(old, "old");
                File.WriteAllText(marker, "keep");
                new HistoryStore(directory).Append(new HistoryPoint[0], now);
                check(!File.Exists(old) && File.Exists(marker) && File.Exists(day), "retention removes only recognized expired daily files");
                var previousContext = SynchronizationContext.Current;
                try
                {
                    var source = new FixedProvider(Snapshot(DateTime.UtcNow, 20, null));
                    var monitor = new UsageMonitor(new Settings { HistoryEnabled = false }, new[] { source }, new EmptyBluetooth());
                    using (var recorder = new HistoryRecorder(monitor, new HistoryStore(Path.Combine(directory, "recorder"))))
                    {
                        SynchronizationContext.SetSynchronizationContext(null);
                        monitor.Refresh(true).GetAwaiter().GetResult();
                        recorder.FlushAsync().GetAwaiter().GetResult();
                        check(!Directory.Exists(Path.Combine(directory, "recorder")), "disabled recording creates no history files");
                        monitor.Config.HistoryEnabled = true;
                        monitor.Emit();
                        recorder.FlushAsync().GetAwaiter().GetResult();
                        check(new HistoryStore(Path.Combine(directory, "recorder")).Read(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1)).Count == 1, "enabling history persists the current valid snapshot");
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            finally
            {
                foreach (string subdirectory in Directory.GetDirectories(directory))
                {
                    foreach (string file in Directory.GetFiles(subdirectory))
                        File.Delete(file);
                    Directory.Delete(subdirectory);
                }

                foreach (string file in Directory.GetFiles(directory))
                    File.Delete(file);
                Directory.Delete(directory);
            }

            return "PASS " + count + " history and order checks";
        }

        static UsageSnapshot Snapshot(DateTime time, double used, double? reset)
        {
            return new UsageSnapshot
            {
                Name = "Codex",
                Updated = time,
                Quotas = new List<QuotaWindow>
                {
                    new QuotaWindow
                    {
                        Label = "5小时",
                        DurationMinutes = 300,
                        Used = used,
                        Resets = reset
                    }
                }
            };
        }

        static HistoryPoint Point(double time, decimal value, double? reset)
        {
            return new HistoryPoint
            {
                Time = time,
                Series = "quota:test",
                Label = "Test",
                Value = value,
                Unit = "%",
                Reset = reset
            };
        }

        class FixedProvider : IUsageProvider
        {
            readonly UsageSnapshot snapshot;
            public FixedProvider(UsageSnapshot value)
            {
                snapshot = value;
            }

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

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(snapshot);
            }
        }

        class EmptyBluetooth : IBluetoothSource
        {
            public Task<BluetoothSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(new BluetoothSnapshot());
            }
        }
    }
}
