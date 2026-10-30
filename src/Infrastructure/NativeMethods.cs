using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TokenDock
{
    static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
            public Rectangle Rectangle
            {
                get
                {
                    return Rectangle.FromLTRB(Left, Top, Right, Bottom);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Pt
        {
            public int X, Y;
            public Pt(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Sz
        {
            public int W, H;
            public Sz(int w, int h)
            {
                W = w;
                H = h;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Blend
        {
            public byte Op, Flags, Alpha, Format;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr h, out Rect r);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr h, out Rect r);
        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr h, ref Pt p);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Pt point);
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetLong32(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetLong32(IntPtr h, int index, int value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetLong64(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetLong64(IntPtr h, int index, IntPtr value);
        public static long GetStyle(IntPtr h, int index)
        {
            return IntPtr.Size == 8 ? GetLong64(h, index).ToInt64() : GetLong32(h, index);
        }

        public static void SetStyle(IntPtr h, int index, long value)
        {
            if (IntPtr.Size == 8)
                SetLong64(h, index, new IntPtr(value));
            else
                SetLong32(h, index, (int)value);
        }

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dest, IntPtr position, ref Sz size, IntPtr source, ref Pt origin, int key, ref Blend blend, int flags);
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);
        public static float Dpi(IntPtr h)
        {
            try
            {
                uint d = GetDpiForWindow(h);
                return d > 0 ? d / 96f : 1;
            }
            catch
            {
                return 1;
            }
        }

        public static Rectangle Bounds(IntPtr h)
        {
            Rect r;
            return h != IntPtr.Zero && GetWindowRect(h, out r) ? r.Rectangle : Rectangle.Empty;
        }

        public static void PopupFrame(Form f)
        {
            try
            {
                int round = 2;
                DwmSetWindowAttribute(f.Handle, 33, ref round, 4);
                int dark = Theme.Light ? 0 : 1;
                DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);
            }
            catch
            {
            }
        }
    }
}
