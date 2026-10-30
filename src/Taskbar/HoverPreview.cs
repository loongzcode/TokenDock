using System;
using System.Drawing;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class HoverPreview : Form
    {
        HoverContent content;
        float scale = 1;
        public HoverPreview()
        {
            Text = "TokenDock · 速览";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
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
                var value = base.CreateParams;
                value.ExStyle |= 0x08000000 | 0x80 | 0x20;
                return value;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x84)
            {
                message.Result = new IntPtr(-1);
                return;
            }

            base.WndProc(ref message);
        }

        public void Present(HoverContent value, Rectangle anchor, float dpiScale)
        {
            if (value == null)
            {
                Hide();
                return;
            }

            var area = Screen.FromRectangle(anchor).WorkingArea;
            Prepare(value, dpiScale, area.Width - 16);
            Left = Math.Max(area.Left + 8, Math.Min(area.Right - Width - 8, anchor.Left));
            int above = anchor.Top - Height - (int)(8 * scale);
            Top = above >= area.Top ? above : Math.Min(area.Bottom - Height - 8, anchor.Bottom + (int)(8 * scale));
            if (!Visible)
            {
                Show();
                NativeMethods.PopupFrame(this);
            }

            Invalidate();
        }

        internal void Prepare(HoverContent value, float dpiScale, int maximumWidth)
        {
            content = value;
            scale = dpiScale;
            int logicalWidth = 320;
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = Theme.PixelFont(12, false))
            {
                foreach (var line in content.Lines)
                    logicalWidth = Math.Max(logicalWidth, Math.Min(430, (int)Math.Ceiling(graphics.MeasureString(line, font).Width) + 36));
            }

            Size = new Size(Math.Min(maximumWidth, (int)(logicalWidth * scale)), (int)((78 + content.Lines.Count * 24) * scale));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            e.Graphics.ScaleTransform(scale, scale);
            float width = Width / scale, height = Height / scale;
            if (content != null)
            {
                Theme.TextAt(e.Graphics, content.Title, 14, true, Theme.Text, 16, 10, width - 32, 28);
                int y = 44;
                foreach (var line in content.Lines)
                {
                    Theme.TextAt(e.Graphics, line, 12, false, Theme.Text, 16, y, width - 32, 24);
                    y += 24;
                }

                Theme.TextAt(e.Graphics, content.Footer, 11, false, Theme.Muted, 16, height - 29, width - 32, 22);
            }

            using (var pen = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(pen, 0, 0, width - 1, height - 1);
        }
    }
}
