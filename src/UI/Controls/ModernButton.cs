using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TokenDock
{
    class ModernButton : Button
    {
        readonly Timer animation = new Timer
        {
            Interval = 25
        };
        bool over, down, busy, selected;
        float blend, angle;
        public bool Primary { get; set; }
        public bool Subtle { get; set; }
        public bool Navigation { get; set; }

        public bool Selected
        {
            get
            {
                return selected;
            }

            set
            {
                selected = value;
                Invalidate();
            }
        }

        public string Glyph { get; set; }

        public bool Busy
        {
            get
            {
                return busy;
            }

            set
            {
                busy = value;
                Enabled = !value;
                if (value)
                    animation.Start();
                Invalidate();
            }
        }

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            Size = new Size(120, 38);
            Cursor = Cursors.Default;
            animation.Tick += delegate
            {
                float target = over ? 1 : 0;
                blend += (target - blend) * .35f;
                angle = (angle + 24) % 360;
                if (!busy && Math.Abs(blend - target) < .03f)
                {
                    blend = target;
                    animation.Stop();
                }

                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            over = true;
            animation.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            over = down = false;
            animation.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                down = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                down = true;
                Invalidate();
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnKeyUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            down = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float scale = g.DpiX / 96f;
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Width / scale, h = Height / scale;
            g.Clear(Parent == null ? Theme.Bg : Parent.BackColor);
            Color background = Primary ? Theme.ControlAccent : selected ? Theme.AccentSoft : Subtle ? (Parent == null ? Theme.Bg : Parent.BackColor) : Theme.Card;
            Color hover = Primary ? (Theme.Light ? Color.FromArgb(82, 82, 82) : Color.FromArgb(238, 238, 238)) : Theme.Hover;
            if (Enabled && !selected)
                background = Mix(background, hover, down ? 1 : blend);
            if (down && Primary)
                background = Theme.Light ? Color.FromArgb(32, 32, 32) : Color.FromArgb(182, 182, 182);
            var rect = new RectangleF(.7f, .7f, Math.Max(1, w - 1.4f), Math.Max(1, h - 1.4f));
            Theme.Round(g, rect, 3, background);
            if (!Subtle && !Primary && !selected)
                using (var path = Theme.Path(rect, 3))
                using (var pen = new Pen(Theme.Line))
                    g.DrawPath(pen, path);
            if (Focused && ShowFocusCues)
                using (var path = Theme.Path(new RectangleF(2.5f, 2.5f, w - 5, h - 5), 2))
                using (var pen = new Pen(Primary ? (Theme.Light ? Color.White : Color.Black) : SystemColors.Highlight, 1.1f))
                    g.DrawPath(pen, path);
            Color foreground = Primary ? (Theme.Light ? Color.White : Color.FromArgb(28, 28, 28)) : !Enabled ? Theme.Muted : Theme.Text;
            float shift = down ? 1 : 0;
            if (Navigation)
            {
                if (selected)
                    using (var pen = new Pen(SystemColors.Highlight, 2))
                        g.DrawLine(pen, 2, 8, 2, h - 8);
                Theme.TextAt(g, Text, 12.5f, false, foreground, 12, shift, w - 20, h);
                return;
            }

            bool icon = busy || !string.IsNullOrEmpty(Glyph);
            if (icon)
            {
                float textWidth;
                using (var font = Theme.PixelFont(13, false))
                    textWidth = string.IsNullOrEmpty(Text) ? 0 : g.MeasureString(Text, font).Width;
                float start = string.IsNullOrEmpty(Text) ? (w - 16) / 2 : Math.Max(12, (w - textWidth - 24) / 2);
                if (busy)
                    using (var pen = new Pen(foreground, 1.7f))
                        g.DrawArc(pen, start, (h - 14) / 2 + shift, 14, 14, angle, 265);
                else
                    UiGlyphs.Draw(g, Glyph, new RectangleF(start, (h - 16) / 2 + shift, 16, 16), foreground);
                if (!string.IsNullOrEmpty(Text))
                    Theme.TextAt(g, Text, 12.5f, false, foreground, start + 24, shift, w - start - 30, h);
            }
            else
                Theme.TextAt(g, Text, 12.5f, false, foreground, new RectangleF(8, shift, w - 16, h), StringAlignment.Center);
        }

        static Color Mix(Color a, Color b, float amount)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animation.Stop();
                animation.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    static class UiGlyphs
    {
        public static void Draw(Graphics g, string kind, RectangleF bounds, Color color)
        {
            var state = g.Save();
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 20, bounds.Height / 20);
            using (var p = new Pen(color, 1.7f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                if (kind == "close")
                {
                    g.DrawLine(p, 5, 5, 15, 15);
                    g.DrawLine(p, 15, 5, 5, 15);
                }
                else if (kind == "refresh")
                {
                    g.DrawArc(p, 3, 3, 14, 14, 35, 290);
                    g.DrawLines(p, new[] { new PointF(13, 2), new PointF(17, 3), new PointF(16, 7) });
                }
                else if (kind == "history")
                {
                    g.DrawLines(p, new[] { new PointF(3, 3), new PointF(3, 17), new PointF(18, 17) });
                    g.DrawLines(p, new[] { new PointF(6, 12), new PointF(10, 8), new PointF(13, 10), new PointF(17, 4) });
                }
                else if (kind == "check")
                {
                    g.DrawLines(p, new[] { new PointF(3, 10), new PointF(8, 15), new PointF(17, 5) });
                }
                else if (kind == "settings")
                {
                    g.DrawEllipse(p, 3, 3, 14, 14);
                    g.DrawEllipse(p, 7, 7, 6, 6);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(p, 10 + 7 * (float)Math.Cos(a), 10 + 7 * (float)Math.Sin(a), 10 + 9 * (float)Math.Cos(a), 10 + 9 * (float)Math.Sin(a));
                    }
                }
                else if (kind == "connect")
                {
                    g.DrawArc(p, 1, 6, 11, 8, 60, 280);
                    g.DrawArc(p, 8, 6, 11, 8, 240, 280);
                }
                else if (kind == "display")
                {
                    g.DrawRectangle(p, 2, 3, 16, 11);
                    g.DrawLine(p, 10, 14, 10, 18);
                    g.DrawLine(p, 6, 18, 14, 18);
                }
                else if (kind == "bell")
                {
                    g.DrawArc(p, 5, 3, 10, 12, 180, 180);
                    g.DrawLines(p, new[] { new PointF(5, 9), new PointF(4, 15), new PointF(16, 15), new PointF(15, 9) });
                    g.DrawArc(p, 8, 15, 4, 3, 0, 180);
                }
            }

            g.Restore(state);
        }
    }
}
