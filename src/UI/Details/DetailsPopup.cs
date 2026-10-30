using System;
using System.Drawing;
using System.Windows.Forms;

namespace TokenDock
{
    class DetailsPopup : Form
    {
        readonly UsageMonitor monitor;
        readonly TaskbarControl bar;
        readonly OverviewControl overview;
        public event Action HistoryRequested;
        public DetailsPopup(UsageMonitor m, TaskbarControl b)
        {
            monitor = m;
            bar = b;
            Text = "TokenDock · 用量与电量";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
            overview = new OverviewControl(m, b)
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(overview);
            overview.CloseRequested += delegate
            {
                Hide();
            };
            overview.SettingsRequested += delegate
            {
                Hide();
                using (var s = new SettingsWindow(m, b))
                    s.ShowDialog();
            };
            overview.HistoryRequested += delegate
            {
                Hide();
                if (HistoryRequested != null)
                    HistoryRequested();
            };
            Deactivate += delegate
            {
                Hide();
            };
            KeyPreview = true;
            KeyDown += async delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                    Hide();
                if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    await overview.RefreshAsync();
                }
            };
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        public void ShowPopup()
        {
            Rectangle anchor = bar.ScreenBounds;
            if (anchor.IsEmpty)
                anchor = Screen.PrimaryScreen.WorkingArea;
            Rectangle area = Screen.FromRectangle(anchor).WorkingArea;
            float scale = NativeMethods.Dpi(NativeMethods.FindWindow("Shell_TrayWnd", null));
            Size = new Size((int)(420 * scale), Math.Min((int)(600 * scale), area.Height - 24));
            Left = Math.Max(area.Left + 12, Math.Min(area.Right - Width - 12, anchor.Left));
            Top = Math.Max(area.Top + 12, area.Bottom - Height - 12);
            Show();
            NativeMethods.PopupFrame(this);
            Activate();
            overview.Focus();
            overview.Invalidate();
        }
    }
}
