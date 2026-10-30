using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TokenDock
{
    class LayeredSurface
    {
        public bool IsLayered { get; private set; }
        public bool Rendered { get; private set; }
        public int Error { get; private set; }
        public int OpaquePixels { get; private set; }
        public NativeMethods.Pt OpaquePoint { get; private set; }

        public LayeredSurface()
        {
            IsLayered = true;
        }

        public void MarkGdiRendered()
        {
            Rendered = true;
        }

        public void Update(Control target, Bitmap bitmap)
        {
            InspectAlpha(bitmap);
            if (!IsLayered)
            {
                target.Invalidate();
                return;
            }

            IntPtr screen = NativeMethods.GetDC(IntPtr.Zero), dc = NativeMethods.CreateCompatibleDC(screen), image = bitmap.GetHbitmap(Color.FromArgb(0)), previous = NativeMethods.SelectObject(dc, image);
            try
            {
                var size = new NativeMethods.Sz(bitmap.Width, bitmap.Height);
                var point = new NativeMethods.Pt(0, 0);
                var blend = new NativeMethods.Blend
                {
                    Alpha = 255,
                    Format = 1
                };
                if (NativeMethods.UpdateLayeredWindow(target.Handle, screen, IntPtr.Zero, ref size, dc, ref point, 0, ref blend, 2))
                {
                    Rendered = OpaquePixels > 0;
                    Error = 0;
                }
                else
                {
                    Error = Marshal.GetLastWin32Error();
                    IsLayered = false;
                    NativeMethods.SetStyle(target.Handle, -20, NativeMethods.GetStyle(target.Handle, -20) & ~0x80000L);
                    target.Invalidate();
                }
            }
            finally
            {
                NativeMethods.SelectObject(dc, previous);
                NativeMethods.DeleteObject(image);
                NativeMethods.DeleteDC(dc);
                NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }
        }

        void InspectAlpha(Bitmap bitmap)
        {
            OpaquePixels = 0;
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                        if (pixels[y * stride + x * 4 + 3] > 200)
                        {
                            if (OpaquePixels == 0)
                                OpaquePoint = new NativeMethods.Pt(x, y);
                            OpaquePixels++;
                        }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
