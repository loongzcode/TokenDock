using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TokenDock
{
    static class TaskbarExperienceTests
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
            DateTime now = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
            double unix = (now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var quota = new UsageSnapshot
            {
                Name = "Codex",
                Updated = now,
                Quotas = new List<QuotaWindow>
                {
                    new QuotaWindow
                    {
                        Label = "5小时",
                        Used = 100,
                        Resets = unix + 3661
                    },
                    new QuotaWindow
                    {
                        Label = "7天",
                        Used = 20,
                        Resets = unix + 172800
                    }
                }
            };
            var hover = HoverContent.Build("codex", quota, null, null, null, null, now);
            check(hover.Lines.Contains("5小时 · 剩余 0%") && hover.Lines.Contains("7天 · 剩余 80%"), "hover includes all quota windows and real zero");
            check(hover.Lines.Contains("距重置 1小时 1分钟"), "hover countdown uses reset time");
            check(HoverContent.Countdown(TimeSpan.FromSeconds(40)) == "40秒", "subminute countdown");
            check(HoverContent.Countdown(TimeSpan.FromMinutes(3)) == "3分钟 0秒", "minute countdown");
            check(HoverContent.Countdown(TimeSpan.FromDays(2)) == "2天 0小时", "day countdown");
            check(HoverContent.Countdown(TimeSpan.Zero) == "等待更新", "expired countdown never becomes negative");
            hover = HoverContent.Build("codex", quota, null, null, null, null, now.AddHours(2));
            check(hover.Lines.Contains("5小时 · 等待额度更新"), "expired window is shown as waiting rather than remaining quota");
            quota.Stale = true;
            check(HoverContent.Build("codex", quota, null, null, null, null, now).Footer.StartsWith("旧数据"), "stale hover data is explicit");
            var devices = new[]
            {
                new BluetoothDevice
                {
                    id = "keyboard",
                    name = "Keyboard",
                    connected = true,
                    battery = 0,
                    source = "Windows 上报（可能有缓存）"
                }
            };
            hover = HoverContent.Build("device:keyboard", null, null, null, devices, now, now);
            check(hover.Title == "Keyboard" && hover.Lines.Contains("电量 0%") && hover.Lines.Contains("已连接"), "device hover keeps real zero and connection state");
            check(hover.Lines.Contains(devices[0].source) && hover.Footer.Contains("更新于"), "device hover exposes reading source and update time");
            check(HoverContent.Build("device:missing", null, null, null, devices, now, now) == null, "removed device does not leave a hover preview");
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
            check(HoverContent.Build("deepseek", null, null, balance, null, null, now).Lines.Single() == "CNY 可用余额 0", "balance hover uses actual currency and no invented token totals");
            var settings = Settings.FromJson("{\"ShowBar\":true}");
            check(settings.TaskbarStyle == "full" && settings.HoverPreviewEnabled, "old settings gain compatible display defaults");
            settings.TaskbarStyle = "invalid";
            settings.Normalize();
            check(settings.TaskbarStyle == "full", "unknown style normalizes safely");
            var items = new List<TaskbarItem>
            {
                new TaskbarItem
                {
                    Key = "codex",
                    Label = "Codex",
                    Value = "90%"
                },
                new TaskbarItem
                {
                    Key = "device:keyboard",
                    Label = "Long Keyboard Name",
                    Value = "100%",
                    Battery = 100
                }
            };
            int full = TaskbarDrawing.Measure(items), compact = TaskbarDrawing.Measure(items, true);
            check(compact < full, "compact style reduces actual measured taskbar width");
            check(TaskbarDrawing.HitTest(items, compact, 25, true) == "codex", "compact hit starts on the provider icon");
            check(TaskbarDrawing.HitTest(items, compact, compact - 10, true) == "device:keyboard", "compact last item remains visible and interactive");
            check(TaskbarDrawing.HitTest(items, compact, 2, true) == null, "outer padding is not a data hover target");
            check(TaskbarDrawing.HitTest(items, 40, 30, true) == null, "clipped ellipsis is not mistaken for a visible item");
            var schedule = new DeviceRefreshSchedule();
            schedule.Signal(now);
            schedule.Signal(now.AddMilliseconds(100));
            check(!schedule.TakeDue(now.AddMilliseconds(500)) && schedule.TakeDue(now.AddSeconds(1)), "connection burst coalesces into one prompt refresh");
            check(!schedule.TakeDue(now.AddSeconds(2)), "connection burst does not trigger duplicate scans");
            check(schedule.TakeDue(now.AddSeconds(11)) && !schedule.TakeDue(now.AddSeconds(30)), "battery property settling triggers exactly one delayed retry");
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                var provider = new CountingProvider();
                var source = new GatedBluetooth();
                var monitor = new UsageMonitor(new Settings(), new[] { provider }, source);
                var first = monitor.RefreshBluetooth();
                var followup = monitor.RefreshBluetooth();
                source.Release();
                followup.GetAwaiter().GetResult();
                check(first.IsCompleted && source.Count == 2 && source.MaximumConcurrent == 1, "connection during scan queues one followup without concurrent scans");
                check(provider.Count == 0 && monitor.Devices.Single().battery == 100, "Bluetooth-only refresh updates devices without API calls");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            return "PASS " + count + " taskbar experience checks";
        }

        class CountingProvider : IUsageProvider
        {
            public int Count;
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
                Count++;
                return Task.FromResult(new UsageSnapshot());
            }
        }

        class GatedBluetooth : IBluetoothSource
        {
            readonly TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();
            public int Count, MaximumConcurrent;
            int concurrent;
            public async Task<BluetoothSnapshot> ReadAsync(Settings settings)
            {
                int active = Interlocked.Increment(ref concurrent);
                MaximumConcurrent = Math.Max(active, MaximumConcurrent);
                int request = Interlocked.Increment(ref Count);
                if (request == 1)
                    await gate.Task;
                Interlocked.Decrement(ref concurrent);
                return new BluetoothSnapshot
                {
                    Updated = DateTime.Now,
                    Devices = new List<BluetoothDevice>
                    {
                        new BluetoothDevice
                        {
                            id = "key",
                            connected = true,
                            battery = 100
                        }
                    }
                };
            }

            public void Release()
            {
                gate.SetResult(true);
            }
        }
    }
}
