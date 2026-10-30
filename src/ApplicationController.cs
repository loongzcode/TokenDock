using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TokenDock
{
    class ApplicationController : ApplicationContext
    {
        readonly UsageMonitor monitor;
        readonly TaskbarControl bar;
        readonly DetailsPopup dashboard;
        readonly NotifyIcon tray;
        readonly Icon icon;
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        readonly System.Windows.Forms.Timer quitTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        readonly EventWaitHandle quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, CommandLine.QuitEvent);
        readonly NotificationService notifications;
        readonly HistoryStore historyStore;
        readonly HistoryRecorder historyRecorder;
        HistoryWindow historyWindow;
        readonly UpdateClient updates = new UpdateClient();
        UpdateWindow updateWindow;
        ToolStripMenuItem healthMenu, updateMenu;
        readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        DateTime nextUpdateCheck = DateTime.UtcNow.AddSeconds(10);
        string notifiedUpdate;
        readonly BluetoothConnectionWatcher deviceWatcher = new BluetoothConnectionWatcher();
        readonly DeviceRefreshSchedule deviceRefresh = new DeviceRefreshSchedule();
        readonly SynchronizationContext uiContext;
        readonly System.Windows.Forms.Timer deviceTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        DateTime? lastDeviceEvent, lastDeviceRequest;
        bool stopped;
        public ApplicationController(Settings config, bool background)
        {
            monitor = new UsageMonitor(config);
            historyStore = new HistoryStore(Path.Combine(AppPaths.DataDirectory, "history"));
            historyRecorder = new HistoryRecorder(monitor, historyStore);
            bar = new TaskbarControl(monitor);
            dashboard = new DetailsPopup(monitor, bar);
            bar.OpenHistory += ShowHistory;
            dashboard.HistoryRequested += ShowHistory;
            bar.OpenDetails += delegate
            {
                if (dashboard.Visible)
                    dashboard.Hide();
                else
                    dashboard.ShowPopup();
            };
            bar.OpenItemDetails += delegate(string key)
            {
                if (dashboard.Visible && dashboard.SelectedKey == key) dashboard.Hide();
                else dashboard.ShowPopup(key);
            };
            icon = AppIcon.Create();
            tray = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "TokenDock · AI 用量与设备电量"
            };
            var menu = new ContextMenuStrip();
            healthMenu = new ToolStripMenuItem("状态：正在读取");
            healthMenu.Click += delegate { dashboard.ShowPopup(); };
            menu.Items.Add(healthMenu);
            menu.Items.Add("用量与电量", null, delegate
            {
                dashboard.ShowPopup();
            });
            menu.Items.Add("设置", null, delegate
            {
                using (var settings = new SettingsWindow(monitor, bar))
                    settings.ShowDialog();
            });
            menu.Items.Add("历史趋势", null, delegate
            {
                ShowHistory();
            });
            menu.Items.Add("刷新", null, async delegate
            {
                await monitor.Refresh(true);
            });
            menu.Items.Add(new ToolStripSeparator());
            updateMenu = new ToolStripMenuItem("检查更新…");
            updateMenu.Click += delegate { ShowUpdates(); };
            menu.Items.Add(updateMenu);
            menu.Items.Add("退出", null, delegate
            {
                ExitThread();
            });
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    dashboard.ShowPopup();
            };
            monitor.Changed += UpdateTray;
            bar.PlacementChanged += UpdateTray;
            notifications = new NotificationService(monitor, tray);
            bar.Start();
            uiContext = SynchronizationContext.Current;
            deviceWatcher.Changed += DeviceChanged;
            deviceWatcher.Start();
            WriteDeviceWatchStatus();
            deviceTimer.Tick += async delegate
            {
                if (deviceRefresh.TakeDue(DateTime.UtcNow))
                {
                    lastDeviceRequest = DateTime.Now;
                    WriteDeviceWatchStatus();
                    await monitor.RefreshBluetooth();
                }
            };
            deviceTimer.Start();
            refreshTimer.Tick += async delegate
            {
                await monitor.Refresh(false);
            };
            refreshTimer.Start();
            quitTimer.Tick += delegate
            {
                if (quitSignal.WaitOne(0))
                    ExitThread();
            };
            quitTimer.Start();
            updateTimer.Tick += async delegate
            {
                if (!monitor.Config.CheckUpdatesEnabled || DateTime.UtcNow < nextUpdateCheck) return;
                nextUpdateCheck = DateTime.UtcNow.AddHours(6);
                try
                {
                    var release = await updates.CheckAsync();
                    if (stopped || !monitor.Config.CheckUpdatesEnabled || !release.IsNewerThan(new Version(AppIdentity.Version))) return;
                    updateMenu.Text = "发现新版本 " + release.Version.ToString(3) + "…";
                    if (notifiedUpdate != release.Tag)
                    {
                        notifiedUpdate = release.Tag;
                        tray.ShowBalloonTip(5000, "TokenDock 有新版本", release.Version.ToString(3) + " 已发布。通过托盘“检查更新”查看并选择下载。", ToolTipIcon.Info);
                    }
                }
                catch { /* Automatic checks stay quiet; manual checks show retry information. */ }
            };
            updateTimer.Start();
            var initial = new System.Windows.Forms.Timer
            {
                Interval = 400
            };
            initial.Tick += async delegate
            {
                initial.Stop();
                initial.Dispose();
                if (!background)
                    dashboard.ShowPopup();
                await monitor.Refresh(true);
            };
            initial.Start();
        }

        void UpdateTray()
        {
            var items = TaskbarItemBuilder.Build(monitor.Codex, monitor.Claude, monitor.DeepSeek, monitor.Devices, monitor.BluetoothUpdated.HasValue, monitor.Config);
            string text = items.Count > 0 ? string.Join(" | ", items.Select(item => (item.Tooltip ?? item.Label) + " " + item.Value)) : "TokenDock · 连接账户或设备";
            var issues = UsageHealth.Issues(monitor, bar.Placement, DateTime.Now);
            healthMenu.Text = issues.Count > 0 ? "状态：" + issues.Count + " 项待处理 · 查看详情" : monitor.Config.ShowBar && bar.Placement.StartsWith("正在") ? "状态：正在检测任务栏" : monitor.Codex.Status == "读取中" ? "状态：正在读取" : "状态：正常";
            healthMenu.ToolTipText = string.Join("\n", issues);
            if (issues.Count > 0) text = "TokenDock · " + issues[0] + " | " + text;
            tray.Text = text.Substring(0, Math.Min(63, text.Length));
        }

        void ShowUpdates()
        {
            dashboard.Hide();
            if (updateWindow == null || updateWindow.IsDisposed) updateWindow = new UpdateWindow(updates);
            updateWindow.Show();
            updateWindow.Activate();
        }

        void ShowHistory()
        {
            dashboard.Hide();
            if (historyWindow == null || historyWindow.IsDisposed)
                historyWindow = new HistoryWindow(historyStore, historyRecorder, monitor);
            historyWindow.Show();
            historyWindow.Activate();
        }

        void DeviceChanged()
        {
            if (uiContext == null || stopped)
                return;
            try
            {
                uiContext.Post(delegate
                {
                    if (stopped)
                        return;
                    lastDeviceEvent = DateTime.Now;
                    deviceRefresh.Signal(DateTime.UtcNow);
                    WriteDeviceWatchStatus();
                }, null);
            }
            catch (InvalidOperationException)
            { /* The UI message loop is shutting down. */
            }
        }

        void WriteDeviceWatchStatus()
        {
            try
            {
                Json.AtomicWrite(Path.Combine(AppPaths.DataDirectory, "bluetooth-watch-status.json"), Json.Write(new { active = deviceWatcher.Active, error = deviceWatcher.LastError, changed = lastDeviceEvent, refreshRequested = lastDeviceRequest }));
            }
            catch
            {
            }
        }

        void Cleanup()
        {
            if (stopped)
                return;
            stopped = true;
            deviceWatcher.Changed -= DeviceChanged;
            deviceWatcher.Dispose();
            deviceTimer.Stop();
            deviceTimer.Dispose();
            refreshTimer.Stop();
            refreshTimer.Dispose();
            quitTimer.Stop();
            quitTimer.Dispose();
            quitSignal.Dispose();
            updateTimer.Stop(); updateTimer.Dispose();
            if (updateWindow != null) updateWindow.Dispose();
            updates.Dispose();
            notifications.Dispose();
            if (historyWindow != null)
                historyWindow.Dispose();
            historyRecorder.Dispose();
            monitor.Changed -= UpdateTray;
            bar.PlacementChanged -= UpdateTray;
            tray.Visible = false;
            tray.Dispose();
            icon.Dispose();
            bar.Dispose();
            dashboard.Dispose();
        }

        protected override void ExitThreadCore()
        {
            Cleanup();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Cleanup();
            base.Dispose(disposing);
        }
    }
}
