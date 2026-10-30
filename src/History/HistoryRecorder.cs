using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class HistoryRecorder : IDisposable
    {
        readonly UsageMonitor monitor;
        readonly HistoryCapture capture = new HistoryCapture();
        readonly List<HistoryPoint> pending = new List<HistoryPoint>();
        readonly Timer timer = new Timer
        {
            Interval = 2000
        };
        readonly HistoryStore store;
        Task writing;
        Task flushing;
        bool disposed;
        public string Error { get; private set; }

        public event Action Saved;
        public HistoryRecorder(UsageMonitor source, HistoryStore target)
        {
            monitor = source;
            store = target;
            monitor.Changed += Changed;
            timer.Tick += async delegate
            {
                await FlushAsync();
            };
            timer.Start();
        }

        void Changed()
        {
            if (!monitor.Config.HistoryEnabled)
            {
                pending.Clear();
                return;
            }

            pending.AddRange(capture.Capture(new[] { monitor.Codex, monitor.Claude, monitor.DeepSeek }, monitor.Devices, monitor.BluetoothUpdated, DateTime.UtcNow));
        }

        public Task FlushAsync()
        {
            if (flushing != null && !flushing.IsCompleted)
                return flushing;
            flushing = FlushCore();
            return flushing;
        }

        async Task FlushCore()
        {
            while (!disposed && pending.Count > 0)
            {
                var batch = pending.ToArray();
                pending.Clear();
                writing = Task.Run(delegate
                {
                    try
                    {
                        store.Append(batch, DateTime.UtcNow);
                        Error = null;
                    }
                    catch (Exception error)
                    {
                        Error = "历史保存失败：" + error.Message;
                    }
                });
                await writing;
                writing = null;
                if (!disposed && Saved != null)
                    Saved();
            }
        }

        public void Dispose()
        {
            monitor.Changed -= Changed;
            timer.Stop();
            timer.Dispose();
            if (writing != null)
                writing.GetAwaiter().GetResult();
            if (monitor.Config.HistoryEnabled && pending.Count > 0)
            {
                try
                {
                    store.Append(pending, DateTime.UtcNow);
                }
                catch
                {
                }
            }

            disposed = true;
        }
    }
}
