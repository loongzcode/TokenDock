using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class SettingsCard : TableLayoutPanel
    {
        public SettingsCard()
        {
            DoubleBuffered = true;
            BackColor = Theme.SettingsBg;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = 1;
            Padding = new Padding(0, 8, 0, 8);
            Margin = new Padding(0, 0, 0, 6);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.SettingsBg);
            using (var pen = new Pen(Theme.Line))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
