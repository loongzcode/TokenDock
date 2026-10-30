using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TokenDock
{
    class ModernToggle : CheckBox
    {
        bool over;
        public ModernToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Height = 28;
            MinimumSize = new Size(80, 28);
            Cursor = Cursors.Default;
            FlatStyle = FlatStyle.Flat;
            Appearance = Appearance.Button;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            float scale = DeviceDpi / 96f;
            return new Size(TextRenderer.MeasureText(Text, Font).Width + (int)(26 * scale), (int)(28 * scale));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            over = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            over = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.SettingsBg : Parent.BackColor);
            var g = e.Graphics;
            float scale = g.DpiX / 96f;
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float width = Width / scale, height = Height / scale, y = (height - 14) / 2;
            var bounds = new RectangleF(1, y, 14, 14);
            Theme.Round(g, bounds, 2, Checked ? Theme.ControlAccent : Theme.Card);
            using (var path = Theme.Path(bounds, 2))
            using (var pen = new Pen(over || Checked ? Theme.ControlAccent : Theme.FieldBorder))
                g.DrawPath(pen, path);
            if (Checked)
                using (var pen = new Pen(Theme.Light ? Color.White : Color.FromArgb(28, 28, 28), 1.6f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new[] { new PointF(4, y + 7), new PointF(7, y + 10), new PointF(12, y + 4) });
                }

            Theme.TextAt(g, Text, 12.5f, false, Enabled ? Theme.Text : Theme.Muted, 24, 0, Math.Max(0, width - 26), height);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(SystemColors.Highlight))
                    g.DrawRectangle(pen, 0, y - 2, 18, 18);
        }
    }
}
