using System;
using System.Drawing;
using System.Windows.Forms;

namespace TokenDock
{
    class SettingsWindow : Form
    {
        readonly UsageMonitor monitor;
        readonly TaskbarControl bar;
        readonly ConnectionPage connection;
        readonly DisplayPage display;
        readonly AlertsPage alerts;
        readonly Label feedback, heading;
        readonly ModernButton[] navigation;
        readonly ModernButton save;
        readonly Timer savedTimer = new Timer
        {
            Interval = 1800
        };
        public Control CurrentPage { get; private set; }

        public SettingsWindow(UsageMonitor source, TaskbarControl taskbar)
        {
            monitor = source;
            bar = taskbar;
            Text = "TokenDock · 设置";
            Font = Theme.Font(9.5f, FontStyle.Regular);
            ForeColor = Theme.Text;
            BackColor = Theme.SettingsBg;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, 620);
            MinimumSize = new Size(680, 460);
            MaximizeBox = false;
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            var sidebar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Sidebar,
                Padding = new Padding(10, 14, 10, 10),
                ColumnCount = 1,
                RowCount = 5
            };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (int i = 0; i < 3; i++)
                sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar.Controls.Add(new Label { Text = "设置", Font = Theme.Font(10, FontStyle.Bold), ForeColor = Theme.Text, Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0) }, 0, 0);
            navigation = new[]
            {
                new ModernButton
                {
                    Text = "账户"
                },
                new ModernButton
                {
                    Text = "显示"
                },
                new ModernButton
                {
                    Text = "提醒"
                }
            };
            for (int i = 0; i < navigation.Length; i++)
            {
                int section = i;
                navigation[i].Dock = DockStyle.Fill;
                navigation[i].Subtle = true;
                navigation[i].Navigation = true;
                navigation[i].Margin = new Padding(0, 0, 0, 4);
                navigation[i].Click += delegate
                {
                    SelectSection(section);
                };
                sidebar.Controls.Add(navigation[i], 0, i + 1);
            }

            sidebar.Controls.Add(new Label { Text = "v" + AppIdentity.Version, ForeColor = Theme.Muted, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10, 0, 0, 0) }, 0, 4);
            shell.Controls.Add(sidebar, 0, 0);
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20, 14, 16, 12),
                BackColor = Theme.SettingsBg
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            shell.Controls.Add(main, 1, 0);
            heading = new Label
            {
                Font = Theme.Font(12, FontStyle.Regular),
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text,
                Margin = new Padding(0, 0, 0, 0)
            };
            main.Controls.Add(heading, 0, 0);
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = Theme.SettingsBg
            };
            main.Controls.Add(content, 0, 1);
            connection = new ConnectionPage(source.Config, SetFeedback);
            display = new DisplayPage(source);
            alerts = new AlertsPage(source.Config);
            content.Controls.Add(connection);
            content.Controls.Add(display);
            content.Controls.Add(alerts);
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0, 10, 8, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            feedback = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var close = new ModernButton
            {
                Text = "关闭",
                Dock = DockStyle.Fill,
                Subtle = false,
                Margin = new Padding(0, 0, 8, 0)
            };
            save = new ModernButton
            {
                Text = "保存",
                Dock = DockStyle.Fill,
                Primary = true,
                Margin = new Padding(0),
                Name = "SaveSettings"
            };
            close.Click += delegate
            {
                Close();
            };
            save.Click += async delegate
            {
                await SaveAsync();
            };
            footer.Controls.Add(feedback, 0, 0);
            footer.Controls.Add(close, 1, 0);
            footer.Controls.Add(save, 2, 0);
            main.Controls.Add(footer, 0, 2);
            savedTimer.Tick += delegate
            {
                savedTimer.Stop();
                save.Text = "保存";
                save.Glyph = null;
                save.Invalidate();
            };
            CancelButton = close;
            SelectSection(0);
            Shown += delegate
            {
                var work = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width - 32), Math.Min(MinimumSize.Height, work.Height - 32));
                Size = new Size(Math.Min(Width, work.Width - 32), Math.Min(Height, work.Height - 32));
                Left = Math.Max(work.Left + 16, Math.Min(work.Right - Width - 16, Left));
                Top = Math.Max(work.Top + 16, Math.Min(work.Bottom - Height - 16, Top));
                NativeMethods.PopupFrame(this);
            };
        }

        async System.Threading.Tasks.Task SaveAsync()
        {
            if (save.Busy)
                return;
            save.Busy = true;
            save.Text = "保存中…";
            savedTimer.Stop();
            try
            {
                var staged = Settings.FromJson(Json.Write(monitor.Config));
                connection.Apply(staged);
                display.Apply(staged);
                alerts.Apply(staged);
                staged.Normalize();
                staged.Save();
                monitor.Config = staged;
                StartupRegistration.SetEnabled(connection.StartupEnabled);
                monitor.Emit();
                SetFeedback("设置已保存，正在刷新数据…");
                await monitor.Refresh(true);
                if (IsDisposed)
                    return;
                SetFeedback("设置已保存");
                save.Text = "已保存";
                save.Glyph = "check";
                savedTimer.Start();
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    SetFeedback("保存失败：" + error.Message);
                    save.Text = "重试保存";
                }
            }
            finally
            {
                if (!IsDisposed)
                    save.Busy = false;
            }
        }

        void SetFeedback(string message)
        {
            if (feedback != null)
                feedback.Text = message;
        }

        public void SelectSection(int index)
        {
            var pages = new Control[]
            {
                connection,
                display,
                alerts
            };
            string[] titles =
            {
                "账户",
                "显示",
                "提醒"
            };
            CurrentPage = pages[index];
            heading.Text = titles[index];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
                navigation[i].Selected = i == index;
            }

            CurrentPage.BringToFront();
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                savedTimer.Stop();
                savedTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
