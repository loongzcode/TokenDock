using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TokenDock
{
    static class FeatureTests
    {
        public static string Run()
        {
            int count = 0;
            Action<bool, string> check = delegate (bool condition, string message)
            {
                if (!condition)
                    throw new Exception("FAIL: " + message);
                count++;
            };
            var settings = new Settings();
            var codex = new UsageSnapshot
            {
                Name = "Codex",
                Updated = DateTime.Now,
                LifetimeTokens = 9400000000,
                Quotas = new List<QuotaWindow>
                {
                    new QuotaWindow
                    {
                        Label = "5小时",
                        DurationMinutes = 300,
                        Used = 20
                    },
                    new QuotaWindow
                    {
                        Label = "7天",
                        DurationMinutes = 10080,
                        Used = 95
                    }
                }
            };
            settings.CodexMetric = "weekly";
            check(TaskbarItemBuilder.Build(codex, null, null, null, false, settings).Single().Value == "5%", "weekly display selects the weekly source window");
            settings.CodexMetric = "short";
            check(TaskbarItemBuilder.Build(codex, null, null, null, false, settings).Single().Value == "80%", "short display selects short quota");
            settings.CodexMetric = "minimum";
            check(TaskbarItemBuilder.Build(codex, null, null, null, false, settings).Single().Value == "5%", "most constrained display selects lower remaining quota");
            settings.CodexMetric = "tokens";
            check(TaskbarItemBuilder.Build(codex, null, null, null, false, settings).Single().Value == "9.4B", "Codex token display uses returned lifetime counter");
            settings.ShowCodex = false;
            check(TaskbarItemBuilder.Build(codex, null, null, null, false, settings).Count == 0, "explicitly hidden provider stays hidden");
            var claude = new UsageSnapshot
            {
                Name = "Claude Code",
                Updated = DateTime.Now,
                InputTokens = 1200,
                OutputTokens = 300
            };
            settings.ClaudeMetric = "tokens";
            check(TaskbarItemBuilder.Build(null, claude, null, null, false, settings).Single().Value == "1.5K", "Claude session token count has its own scope");
            var devices = new[]
            {
                new BluetoothDevice
                {
                    id = "keyboard",
                    name = "Keyboard",
                    connected = true,
                    battery = 50
                },
                new BluetoothDevice
                {
                    id = "mouse",
                    name = "Mouse",
                    connected = true,
                    battery = 20
                },
                new BluetoothDevice
                {
                    id = "headset",
                    name = "Headset",
                    connected = false,
                    battery = 80
                }
            };
            check(TaskbarItemBuilder.Build(null, null, null, devices, true, settings).Count == 2, "multiple connected batteries are individually displayed");
            settings.HiddenDeviceIds.Add("mouse");
            check(TaskbarItemBuilder.Build(null, null, null, devices, true, settings).Single().Key == "device:keyboard", "per-device visibility uses stable identity");
            var extra = devices.Concat(new[] { new BluetoothDevice { id = "new", name = "New device", connected = true, battery = 60 } });
            check(TaskbarItemBuilder.Build(null, null, null, extra, true, settings).Count == 2, "new standard devices are included by default");
            var migrated = Json.Serializer().Deserialize<Settings>("{\"CodexPath\":\"codex.exe\",\"DeepSeekKey\":\"cipher\",\"TaskbarPosition\":\"custom\",\"TaskbarCustomOffset\":1344,\"RefreshSeconds\":120,\"OverlayTaskbar\":true}");
            migrated.Normalize();
            check(migrated.DeepSeekKey == "cipher" && migrated.TaskbarCustomOffset == 1344 && migrated.ShowCodex && migrated.CodexMetric == "auto", "legacy settings keep encrypted key and placement while adding defaults");
            migrated.HiddenDeviceIds = null;
            migrated.RefreshSeconds = 1;
            migrated.Normalize();
            check(migrated.HiddenDeviceIds != null && migrated.RefreshSeconds == 60, "configuration normalization repairs missing collections and invalid intervals");
            var alerts = new AlertEngine();
            settings = new Settings();
            var first = alerts.Evaluate(settings, new[] { codex }, new BluetoothDevice[0], true);
            check(first.Count == 1, "low subscription quota triggers one alert");
            check(alerts.Evaluate(settings, new[] { codex }, new BluetoothDevice[0], true).Count == 0, "repeated refresh does not repeat an active low alert");
            codex.Quotas[1].Used = 10;
            alerts.Evaluate(settings, new[] { codex }, new BluetoothDevice[0], true);
            codex.Quotas[1].Used = 95;
            check(alerts.Evaluate(settings, new[] { codex }, new BluetoothDevice[0], true).Count == 1, "quota recovery rearms the threshold alert");
            codex.Stale = true;
            check(new AlertEngine().Evaluate(settings, new[] { codex }, devices, true).All(message => !message.Key.StartsWith("quota:")), "stale quota never generates an alert");
            var zero = new[]
            {
                new BluetoothDevice
                {
                    id = "zero",
                    name = "Zero battery",
                    connected = true,
                    battery = 0
                },
                new BluetoothDevice
                {
                    id = "unknown",
                    name = "Unknown",
                    connected = true,
                    battery = null
                },
                new BluetoothDevice
                {
                    id = "offline",
                    name = "Offline",
                    connected = false,
                    battery = 1
                }
            };
            check(new AlertEngine().Evaluate(settings, new UsageSnapshot[0], zero, true).Count == 1, "real zero battery alerts while missing and disconnected readings do not");
            check(new AlertEngine().Evaluate(settings, new UsageSnapshot[0], zero, false).Count == 0, "failed battery scan does not use cached values for alerts");
            settings.AlertsEnabled = false;
            check(new AlertEngine().Evaluate(settings, new UsageSnapshot[0], zero, true).Count == 0, "global notification switch is respected");
            settings.AlertsEnabled = true;
            var balance = new UsageSnapshot
            {
                Name = "DeepSeek",
                Updated = DateTime.Now,
                Balances = new List<BalanceInfo>
                {
                    new BalanceInfo
                    {
                        Currency = "CNY",
                        Amount = 0
                    }
                }
            };
            check(new AlertEngine().Evaluate(settings, new[] { balance }, new BluetoothDevice[0], true).Count == 1, "zero API balance is a real low balance");
            var fake = new FakeProvider("codex", codex);
            var monitor = new UsageMonitor(new Settings(), new[] { fake }, new FakeBluetooth());
            codex.Stale = false;
            monitor.Refresh(true).GetAwaiter().GetResult();
            fake.Fail = true;
            monitor.Refresh(true).GetAwaiter().GetResult();
            check(monitor.Codex.Stale && monitor.Codex.Quotas.Count == 2, "provider failure retains prior data but marks it stale");
            bool futureRejected = false;
            try
            {
                Settings.FromJson("{\"SchemaVersion\":2}");
            }
            catch (System.IO.InvalidDataException)
            {
                futureRejected = true;
            }

            check(futureRejected, "older application does not silently downgrade a future configuration");
            bool malformedRejected = false;
            try
            {
                Settings.FromJson("{");
            }
            catch
            {
                malformedRejected = true;
            }

            check(malformedRejected, "invalid configuration is not silently replaced by defaults");
            var previousContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                var gated = new GatedProvider(codex);
                var queuedMonitor = new UsageMonitor(new Settings(), new[] { gated }, new FakeBluetooth());
                Task running = queuedMonitor.Refresh(true);
                Task requested = queuedMonitor.Refresh(true);
                gated.Release();
                requested.GetAwaiter().GetResult();
                check(gated.ReadCount == 2 && running.IsCompleted, "forced refresh during an active request is queued rather than ignored");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }

            return "PASS " + count + " feature checks";
        }

        class FakeProvider : IUsageProvider
        {
            public bool Fail;
            readonly UsageSnapshot snapshot;
            public string Id { get; private set; }

            public string Name
            {
                get
                {
                    return "Fake";
                }
            }

            public FakeProvider(string id, UsageSnapshot value)
            {
                Id = id;
                snapshot = value;
            }

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                if (Fail)
                    throw new InvalidOperationException("offline");
                return Task.FromResult(snapshot);
            }
        }

        class FakeBluetooth : IBluetoothSource
        {
            public Task<BluetoothSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(new BluetoothSnapshot { Updated = DateTime.Now, Status = "test" });
            }
        }

        class GatedProvider : IUsageProvider
        {
            readonly TaskCompletionSource<UsageSnapshot> gate = new TaskCompletionSource<UsageSnapshot>();
            readonly UsageSnapshot snapshot;
            public int ReadCount;
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
                    return "Gate";
                }
            }

            public GatedProvider(UsageSnapshot value)
            {
                snapshot = value;
            }

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                ReadCount++;
                return ReadCount == 1 ? gate.Task : Task.FromResult(snapshot);
            }

            public void Release()
            {
                gate.SetResult(snapshot);
            }
        }
    }
}
