using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Automation;

namespace TokenDock
{
    static class TaskbarOccupancyReader
    {
        public static List<Rectangle> Read(IntPtr handle)
        {
            var result = new List<Rectangle>();
            bool incomplete = false;
            try
            {
                var root = AutomationElement.FromHandle(handle);
                var elements = root.FindAll(TreeScope.Descendants, new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)));
                Rectangle bounds = NativeMethods.Bounds(handle);
                foreach (AutomationElement element in elements)
                {
                    try
                    {
                        var current = element.Current;
                        var r = current.BoundingRectangle;
                        if (!current.IsOffscreen && r.Width > 0 && r.Width < bounds.Width * 0.8 && r.Height > 0)
                            result.Add(Rectangle.FromLTRB((int)r.Left, (int)r.Top, (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom)));
                    }
                    catch
                    {
                        incomplete = true;
                    }
                }
            }
            catch
            {
                return null;
            }

            return !incomplete && result.Count > 0 ? result : null;
        }
    }
}
