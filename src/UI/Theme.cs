using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TokenDock
{
    static class Theme
    {
        static DateTime nextThemeCheck;
        static bool lightTheme = true;
        public static bool Light
        {
            get
            {
                if (DateTime.UtcNow < nextThemeCheck)
                    return lightTheme;
                nextThemeCheck = DateTime.UtcNow.AddSeconds(1);
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        lightTheme = Convert.ToInt32(k == null ? 1 : k.GetValue("SystemUsesLightTheme", 1)) != 0;
                }
                catch
                {
                    lightTheme = true;
                }

                return lightTheme;
            }
        }

        public static Color Bg
        {
            get
            {
                return Light ? Color.FromArgb(250, 250, 250) : Color.FromArgb(31, 31, 31);
            }
        }

        public static Color Card
        {
            get
            {
                return Light ? Color.White : Color.FromArgb(39, 39, 39);
            }
        }

        public static Color Text
        {
            get
            {
                return Light ? Color.FromArgb(30, 30, 30) : Color.FromArgb(245, 245, 245);
            }
        }

        public static Color Muted
        {
            get
            {
                return Light ? Color.FromArgb(112, 112, 112) : Color.FromArgb(164, 164, 164);
            }
        }

        public static Color Line
        {
            get
            {
                return Light ? Color.FromArgb(226, 226, 226) : Color.FromArgb(63, 63, 63);
            }
        }

        public static Color Accent
        {
            get
            {
                return Light ? Color.FromArgb(20, 118, 94) : Color.FromArgb(110, 211, 170);
            }
        }

        public static Color SettingsBg
        {
            get
            {
                return Light ? Color.FromArgb(252, 252, 252) : Color.FromArgb(28, 28, 28);
            }
        }

        public static Color Hover
        {
            get
            {
                return Light ? Color.FromArgb(237, 237, 237) : Color.FromArgb(54, 54, 54);
            }
        }

        public static Color AccentSoft
        {
            get
            {
                return Light ? Color.FromArgb(228, 228, 228) : Color.FromArgb(57, 57, 57);
            }
        }

        public static Color AccentHover
        {
            get
            {
                return Light ? Color.FromArgb(15, 104, 82) : Color.FromArgb(93, 185, 149);
            }
        }

        public static Color ToggleTrack
        {
            get
            {
                return Light ? Color.FromArgb(179, 188, 190) : Color.FromArgb(92, 103, 105);
            }
        }

        public static Color ToggleHover
        {
            get
            {
                return Light ? Color.FromArgb(157, 170, 172) : Color.FromArgb(112, 123, 126);
            }
        }

        public static Color FieldBorder
        {
            get
            {
                return Light ? Color.FromArgb(211, 211, 211) : Color.FromArgb(78, 78, 78);
            }
        }

        public static Color Sidebar
        {
            get
            {
                return Light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(35, 35, 35);
            }
        }

        public static Color ControlAccent
        {
            get
            {
                return Light ? Color.FromArgb(56, 56, 56) : Color.FromArgb(212, 212, 212);
            }
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style);
        }

        public static Font PixelFont(float size, bool bold)
        {
            return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static void TextAt(Graphics g, string text, float size, bool bold, Color color, RectangleF r, StringAlignment align)
        {
            using (var font = PixelFont(size, bold))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            }

            )
                g.DrawString(text, font, brush, r, format);
        }

        public static void TextAt(Graphics g, string text, float size, bool bold, Color color, float x, float y, float w, float h)
        {
            TextAt(g, text, size, bold, color, new RectangleF(x, y, w, h), StringAlignment.Near);
        }

        public static Button Button(string text, int width)
        {
            var b = new ModernButton
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Card,
                ForeColor = Text,
                Font = Font(9.5f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = Light ? Color.FromArgb(238, 238, 238) : Color.FromArgb(52, 52, 52);
            return b;
        }

        public static void Round(Graphics g, RectangleF r, float radius, Color color)
        {
            using (var p = Path(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static GraphicsPath Path(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
