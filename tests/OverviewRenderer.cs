using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TokenDock
{
    static class OverviewRenderer
    {
        public static void Render(string destination)
        {
            var previous = SynchronizationContext.Current;
            try
            {
                var monitor = new UsageMonitor(new Settings(), new IUsageProvider[] { new Provider("codex"), new Provider("deepseek") }, new Devices());
                using (var view = new OverviewControl(monitor, null)
                {
                    Size = new Size(420, 600)
                }

                )
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    monitor.Refresh(true).GetAwaiter().GetResult();
                    using (var image = new Bitmap(420, 600))
                    {
                        view.DrawToBitmap(image, view.ClientRectangle);
                        image.Save(destination, ImageFormat.Png);
                    }
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        sealed class Provider : IUsageProvider
        {
            readonly string id;
            public Provider(string value)
            {
                id = value;
            }

            public string Id
            {
                get
                {
                    return id;
                }
            }

            public string Name
            {
                get
                {
                    return id == "codex" ? "Codex" : "DeepSeek";
                }
            }

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                var snapshot = new UsageSnapshot
                {
                    Name = Name,
                    Updated = DateTime.Now,
                    Status = id == "codex" ? "87%" : "¥10.21"
                };
                if (id == "codex")
                    snapshot.Quotas.Add(new QuotaWindow { Label = "7天", Used = 13, Resets = SystemClock.UnixNow() + 86400 * 4 });
                return Task.FromResult(snapshot);
            }
        }

        sealed class Devices : IBluetoothSource
        {
            public Task<BluetoothSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(new BluetoothSnapshot { Status = "成功", Updated = DateTime.Now, Devices = new List<BluetoothDevice> { new BluetoothDevice { id = "keyboard", name = "RK R98 5.0", connected = true, battery = 100 } } });
            }
        }
    }
}
