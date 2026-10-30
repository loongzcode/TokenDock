using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TokenDock
{
    class DragPreview : Form
    {
        readonly float scale;
        readonly Action<Graphics> paint;
        public DragPreview(int width, int height, float dpiScale, Action<Graphics> draw)
        {
            scale = dpiScale;
            paint = draw;
            Text = "TokenDock · 拖动预览";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(width + (int)(8 * scale), height + (int)(8 * scale));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= 0x08000000 | 0x00000080 | 0x20;
                p.ClassStyle |= 0x20000;
                return p;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {
                m.Result = new IntPtr(-1);
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Path(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8 * scale))
            using (var pen = new Pen(Theme.Line))
                e.Graphics.DrawPath(pen, p);
            e.Graphics.TranslateTransform(4 * scale, 4 * scale);
            e.Graphics.ScaleTransform(scale, scale);
            paint(e.Graphics);
        }
    }
}
