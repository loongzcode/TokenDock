using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    class NotificationService : IDisposable
    {
        readonly UsageMonitor monitor;
        readonly NotifyIcon tray;
        readonly AlertEngine engine = new AlertEngine();
        readonly List<AlertMessage> pending = new List<AlertMessage>();
        readonly Timer timer = new Timer
        {
            Interval = 3000
        };
        public NotificationService(UsageMonitor source, NotifyIcon notificationIcon)
        {
            monitor = source;
            tray = notificationIcon;
            monitor.Changed += OnChanged;
            timer.Tick += delegate
            {
                Flush();
            };
        }

        void OnChanged()
        {
            pending.AddRange(engine.Evaluate(monitor.Config, new[] { monitor.Codex, monitor.Claude, monitor.DeepSeek }, monitor.Devices, monitor.BluetoothUpdated.HasValue));
            if (pending.Count > 0)
                timer.Start();
        }

        void Flush()
        {
            timer.Stop();
            if (pending.Count == 0)
                return;
            if (!monitor.Config.AlertsEnabled)
            {
                pending.Clear();
                return;
            }

            var deliver = pending.Where(message => !message.Key.StartsWith("recovery:") || monitor.Config.QuotaRecoveryAlertsEnabled).ToList();
            if (deliver.Count == 0)
            {
                pending.Clear();
                return;
            }

            string text = string.Join("\n", deliver.Select(message => message.Title + "：" + message.Text));
            if (text.Length > 250)
                text = text.Substring(0, 247) + "…";
            tray.ShowBalloonTip(8000, "TokenDock 提醒", text, ToolTipIcon.Info);
            pending.Clear();
        }

        public void Dispose()
        {
            monitor.Changed -= OnChanged;
            timer.Stop();
            timer.Dispose();
        }
    }
}
