using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    class OverviewControl : Control
    {
        readonly UsageMonitor monitor;
        readonly TaskbarControl bar;
        float scale = 1;
        int scroll, contentHeight;
        readonly ModernButton refreshButton, historyButton, settingsButton, closeButton;
        readonly ModernButton allButton;
        public string SelectedKey { get; private set; }
        public void SelectItem(string key)
        {
            SelectedKey = DetailSelection.Normalize(key);
            scroll = 0;
            allButton.Visible = SelectedKey != null;
            Changed();
        }
        readonly Timer resultTimer = new Timer
        {
            Interval = 2000
        };
        readonly ToolTip feedback = new ToolTip();
        Task refreshTask;
        public event Action SettingsRequested;
        public event Action HistoryRequested;
        public event Action CloseRequested;
        public event Action AllRequested;
        public OverviewControl(UsageMonitor m, TaskbarControl b)
        {
            monitor = m;
            bar = b;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            TabStop = true;
            AccessibleName = "AI 剩余额度和蓝牙电量";
            AccessibleRole = AccessibleRole.Pane;
            SetStyle(ControlStyles.ResizeRedraw, true);
            refreshButton = new ModernButton
            {
                Text = "刷新",
                Glyph = "refresh",
                Name = "RefreshOverview",
                AccessibleName = "刷新用量与电量",
                TabIndex = 0
            };
            historyButton = new ModernButton
            {
                Text = "历史趋势",
                Glyph = "history",
                Name = "OpenHistory",
                AccessibleName = "打开历史趋势",
                TabIndex = 1
            };
            settingsButton = new ModernButton
            {
                Text = "连接设置",
                Glyph = "settings",
                Name = "OpenSettings",
                AccessibleName = "打开连接设置",
                TabIndex = 2
            };
            closeButton = new ModernButton
            {
                Text = "",
                Glyph = "close",
                Subtle = true,
                Name = "CloseOverview",
                AccessibleName = "关闭详情",
                TabStop = false
            };
            Controls.AddRange(new Control[] { refreshButton, historyButton, settingsButton, closeButton });
            allButton = new ModernButton { Text = "全部", Name = "ShowAllDetails", AccessibleName = "返回全部用量与电量", Subtle = true, Visible = false };
            Controls.Add(allButton);
            allButton.Click += delegate { SelectItem(null); if (AllRequested != null) AllRequested(); };
            refreshButton.Click += async delegate
            {
                await RefreshAsync();
            };
            historyButton.Click += delegate
            {
                if (HistoryRequested != null)
                    HistoryRequested();
            };
            settingsButton.Click += delegate
            {
                if (SettingsRequested != null)
                    SettingsRequested();
            };
            closeButton.Click += delegate
            {
                if (CloseRequested != null)
                    CloseRequested();
            };
            resultTimer.Tick += delegate
            {
                resultTimer.Stop();
                refreshButton.Text = "刷新";
                refreshButton.Glyph = "refresh";
                refreshButton.Selected = false;
            };
            LayoutButtons();
            monitor.Changed += Changed;
            MouseWheel += delegate (object s, MouseEventArgs e)
            {
                scroll = Math.Max(0, Math.Min(Math.Max(0, contentHeight - (int)(Height / scale) + 76), scroll - e.Delta / 3));
                Invalidate();
            };
        }

        void Changed()
        {
            AccessibleDescription = string.Join("；", new[] {
                DetailSelection.Includes(SelectedKey, "codex") ? monitor.Codex.Detail : null,
                DetailSelection.Includes(SelectedKey, "claude") ? monitor.Claude.Detail : null,
                DetailSelection.Includes(SelectedKey, "deepseek") ? monitor.DeepSeek.Detail : null
            }.Where(value => !string.IsNullOrEmpty(value)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            scale = e.Graphics.DpiX / 96f;
            LayoutButtons();
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.ScaleTransform(scale, scale);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Width / scale;
            int y = 72 - scroll;
            var clip = g.Clip;
            g.SetClip(new RectangleF(0, 64, w, Math.Max(1, Height / scale - 132)));
            if (SelectedKey == null && bar != null && monitor.Config.ShowBar && bar.Placement != "任务栏控件已显示")
            {
                Theme.TextAt(g, bar.Placement, 11, false, Theme.Muted, 24, y, w - 48, 32);
                y += 36;
            }
            if (DetailSelection.Includes(SelectedKey, "codex")) UsageSnapshot(g, monitor.Codex, 24, ref y, w - 48);
            if (DetailSelection.Includes(SelectedKey, "claude")) UsageSnapshot(g, monitor.Claude, 24, ref y, w - 48);
            if (DetailSelection.Includes(SelectedKey, "deepseek")) UsageSnapshot(g, monitor.DeepSeek, 24, ref y, w - 48);
            if (SelectedKey == null || SelectedKey.StartsWith("device:"))
            {
            y += 8;
            Theme.TextAt(g, "设备电量", 12, true, Theme.Muted, 24, y, w - 48, 24);
            y += 32;
            var devices = monitor.Devices.Where(d => DetailSelection.Includes(SelectedKey, "device:" + d.Identity)).OrderByDescending(d => d.connected == true).ToArray();
            if (!monitor.BluetoothUpdated.HasValue)
            {
                Theme.TextAt(g, (monitor.BluetoothStatus ?? "等待设备读取"), 11, false, Theme.Muted, 24, y, w - 48, 32);
                y += 36;
            }
            if (devices.Length == 0)
            {
                Theme.TextAt(g, SelectedKey == null ? "连接蓝牙设备后会自动出现" : "该设备当前不可用，刷新后重试", 12, false, Theme.Muted, 24, y, w - 48, 28);
                y += 40;
            }

            foreach (var d in devices)
            {
                string value = d.connected == false ? "未连接" : d.charging == true ? "充电中" : d.battery.HasValue ? d.battery.Value.ToString("0") + "%" : "电量未知";
                if (!monitor.BluetoothUpdated.HasValue && d.battery.HasValue) value = "旧 " + value;
                Color c = d.connected == true && monitor.BluetoothUpdated.HasValue ? Theme.Text : Theme.Muted;
                ItemIcons.DrawDevice(g, DeviceIconResolver.ResolveDevice(d), 26, y + 9, 16, c);
                Theme.TextAt(g, (d.name ?? "设备").Trim(), 13, false, c, 52, y, w - 196, 36);
                Theme.TextAt(g, value, 12, false, c, new RectangleF(w - 148, y, 124, 36), StringAlignment.Far);
                y += 44;
                if (SelectedKey != null)
                {
                    Theme.TextAt(g, (d.source ?? "") + (monitor.BluetoothUpdated.HasValue ? " · 更新于 " + monitor.BluetoothUpdated.Value.ToString("HH:mm:ss") : " · 数据未更新"), 11, false, Theme.Muted, 24, y, w - 48, 32);
                    y += 36;
                }
            }
            }

            contentHeight = y + scroll;
            g.Clip = clip;
            using (var p = new Pen(Theme.Line))
            {
                g.DrawLine(p, 24, Height / scale - 68, w - 24, Height / scale - 68);
            }

            Theme.TextAt(g, "用量与电量", 16, true, Theme.Text, 24, 16, w - 80, 32);
            using (var pen = new Pen(Theme.Line))
                g.DrawRectangle(pen, 0, 0, w - 1, Height / scale - 1);
        }

        void UsageSnapshot(Graphics g, UsageSnapshot p, int x, ref int y, float width)
        {
            string value = p.Name == "DeepSeek" ? (p.Stale ? "--" : p.Status ?? "暂无数据") : p.Compact;
            var health = UsageHealth.Read(p, DateTime.Now, monitor.Config.RefreshSeconds);
            if (p.Quotas.Count == 0 && p.Name != "DeepSeek")
                value = "未连接";
            if (p.Name == "DeepSeek" && !value.StartsWith("¥") && !value.StartsWith("$"))
                value = "未连接";
            if (health.IsIssue || !p.Updated.HasValue)
                value = health.Label;
            else if (p.Quotas.Count == 0 && p.Name != "DeepSeek" && (p.LifetimeTokens.HasValue || MetricFormatter.SessionTokens(p).HasValue))
                value = "已连接";
            Theme.TextAt(g, p.Name, 14, true, Theme.Text, x, y, width - 120, 28);
            Theme.TextAt(g, value, 16, true, Theme.Text, new RectangleF(x + width - 120, y, 120, 28), StringAlignment.Far);
            y += 30;
            if (health.IsIssue)
            {
                Theme.TextAt(g, health.Hint, 11, false, Theme.Muted, x, y, width, 32);
                y += 36;
                Theme.TextAt(g, p.Updated.HasValue ? "上次成功：" + p.Updated.Value.ToString("MM-dd HH:mm:ss") : "尚未成功读取", 11, false, Theme.Muted, x, y, width, 22);
                y += 26;
            }
            if (p.Quotas.Count > 0)
            {
                foreach (var q in p.Quotas.Take(3))
                {
                    string remaining = q.Expired ? "等待更新" : (health.IsIssue ? "旧 " : "") + q.Remaining.ToString("0.#") + "%";
                    Theme.TextAt(g, q.Label, 12, false, Theme.Muted, x, y, width - 90, 24);
                    Theme.TextAt(g, remaining, 12, false, Theme.Muted, new RectangleF(x + width - 90, y, 90, 24), StringAlignment.Far);
                    y += 27;
                    Theme.Round(g, new RectangleF(x, y, width, 4), 2, Theme.Line);
                    if (!q.Expired && q.Remaining > 0)
                        Theme.Round(g, new RectangleF(x, y, Math.Max(4, width * (float)q.Remaining / 100), 4), 2, health.IsIssue ? Theme.Muted : q.Remaining <= 15 ? Color.FromArgb(202, 113, 61) : Theme.Accent);
                    y += 12;
                    if (q.Resets.HasValue)
                    {
                        Theme.TextAt(g, SystemClock.FromUnix(q.Resets.Value).ToLocalTime().ToString("MM-dd HH:mm") + " 重置" + (p.Stale ? " · 旧数据" : ""), 11, false, Theme.Muted, x, y, width, 20);
                        y += 22;
                    }
                }
            }
            else if (!health.IsIssue)
            {
                string hint = p.Name == "Claude Code" ? "连接 Claude Code 查看订阅额度" : p.Name == "DeepSeek" ? "输入 API Key 查看余额" : "正在读取本机 Codex 登录状态";
                if (value.StartsWith("¥") || value.StartsWith("$"))
                    hint = "官方 API 余额";
                if (p.Status == "读取失败")
                    hint = "读取失败，请检查登录或网络后刷新";
                if (health.IsIssue) hint = "可刷新重试，或检查连接设置";
                Theme.TextAt(g, hint, 12, false, Theme.Muted, x, y, width, 24);
                y += 27;
            }

            string tokenLine = (p.Detail ?? "").Split('\n').FirstOrDefault(line => line.Contains("Token"));
            if (!string.IsNullOrEmpty(tokenLine) && p.Name != "DeepSeek")
            {
                Theme.TextAt(g, tokenLine, 11, false, Theme.Muted, x, y, width, 22);
                y += 22;
            }
            if (!health.IsIssue && p.Updated.HasValue)
            {
                Theme.TextAt(g, "更新于 " + p.Updated.Value.ToString("HH:mm:ss"), 11, false, Theme.Muted, x, y, width, 22);
                y += 22;
            }

            y += 16;
            using (var pen = new Pen(Theme.Line))
                g.DrawLine(pen, x, y, x + width, y);
            y += 20;
        }

        void LayoutButtons()
        {
            if (refreshButton == null || Width < 100 || Height < 100)
                return;
            var bounds = OverviewFooterLayout.Calculate(Size, scale);
            refreshButton.Bounds = bounds[0];
            historyButton.Bounds = bounds[1];
            settingsButton.Bounds = bounds[2];
            closeButton.Bounds = bounds[3];
            if (allButton != null) allButton.Bounds = new Rectangle((int)(Width - 116 * scale), (int)(16 * scale), (int)(54 * scale), (int)(32 * scale));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutButtons();
        }

        public Task RefreshAsync()
        {
            if (refreshTask != null && !refreshTask.IsCompleted)
                return refreshTask;
            refreshTask = RefreshCore();
            return refreshTask;
        }

        async Task RefreshCore()
        {
            resultTimer.Stop();
            refreshButton.Busy = true;
            refreshButton.Text = "刷新中…";
            refreshButton.Selected = false;
            refreshButton.AccessibleDescription = "正在刷新，请稍候";
            try
            {
                await monitor.Refresh(true);
                if (IsDisposed)
                    return;
                bool failed = UsageHealth.Issues(monitor, null, DateTime.Now).Count > 0;
                refreshButton.Text = failed ? "部分失败" : "已刷新";
                refreshButton.Glyph = failed ? "refresh" : "check";
                refreshButton.Selected = !failed;
                refreshButton.AccessibleDescription = refreshButton.Text;
                feedback.SetToolTip(refreshButton, failed ? "部分数据未更新，可再次刷新重试。" : "已完成本次刷新。");
            }
            catch (Exception)
            {
                if (!IsDisposed)
                {
                    refreshButton.Text = "重试";
                    refreshButton.Glyph = "refresh";
                    feedback.SetToolTip(refreshButton, "刷新失败，请稍后重试。");
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    refreshButton.Busy = false;
                    resultTimer.Start();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape && CloseRequested != null)
                CloseRequested();
            if (e.KeyCode == Keys.F5)
            {
                var ignored = RefreshAsync();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                monitor.Changed -= Changed;
                resultTimer.Stop();
                resultTimer.Dispose();
                feedback.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
