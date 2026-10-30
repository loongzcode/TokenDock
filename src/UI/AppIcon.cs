using System;
using System.Drawing;

namespace TokenDock
{
    static class AppIcon
    {
        public static Icon Create()
        {
            using (var bitmap = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Theme.Bg);
                    using (var pen = new Pen(Theme.Accent, 4))
                        g.DrawEllipse(pen, 4, 4, 23, 23);
                    using (var font = new Font("Segoe UI", 15, FontStyle.Bold))
                    using (var brush = new SolidBrush(Theme.Text))
                        g.DrawString("T", font, brush, 6, 3);
                }

                IntPtr icon = bitmap.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(icon).Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(icon);
                }
            }
        }
    }
}
