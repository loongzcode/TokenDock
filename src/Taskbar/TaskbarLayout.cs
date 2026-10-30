using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TokenDock
{
    static class TaskbarLayout
    {
        public static bool CanDrop(Rectangle taskbarBounds, Point point, int margin)
        {
            return point.X >= taskbarBounds.Left && point.X < taskbarBounds.Right && point.Y >= taskbarBounds.Top - margin && point.Y < taskbarBounds.Bottom + margin;
        }

        public static Rectangle FindSlot(Rectangle taskbarBounds, IEnumerable<Rectangle> blocked, int desired, int minimum, int padding, int height)
        {
            return PositionedSlot(taskbarBounds, blocked, desired, minimum, padding, height, "auto", taskbarBounds.Left + padding);
        }

        public static Rectangle PositionedSlot(Rectangle taskbarBounds, IEnumerable<Rectangle> blocked, int desired, int minimum, int padding, int height, string mode, int requestedX)
        {
            return PositionedSlot(taskbarBounds, blocked, desired, minimum, padding, height, mode, requestedX, false);
        }

        public static Rectangle PositionedSlot(Rectangle taskbarBounds, IEnumerable<Rectangle> blocked, int desired, int minimum, int padding, int height, string mode, int requestedX, bool preserveRegion)
        {
            if (blocked == null)
                return Rectangle.Empty;
            var intervals = blocked.Where(r => r.IntersectsWith(taskbarBounds)).OrderBy(r => r.Left).ToList();
            var gaps = new List<Rectangle>();
            int cursor = taskbarBounds.Left + padding;
            foreach (var r in intervals)
            {
                int end = Math.Min(taskbarBounds.Right - padding, r.Left - padding);
                if (end > cursor)
                    gaps.Add(new Rectangle(cursor, taskbarBounds.Top, end - cursor, height));
                cursor = Math.Max(cursor, r.Right + padding);
            }

            if (cursor < taskbarBounds.Right - padding)
                gaps.Add(new Rectangle(cursor, taskbarBounds.Top, taskbarBounds.Right - padding - cursor, height));
            var eligible = gaps.Where(r => r.Width >= minimum).ToList();
            if (eligible.Count == 0)
                return Rectangle.Empty;
            Rectangle candidate;
            if (mode == "custom")
            {
                if (preserveRegion)
                {
                    // Resizing the taskbar may clamp the anchor; ordinary relayout must
                    // not move a saved position across occupied taskbar buttons.
                    requestedX = Math.Max(taskbarBounds.Left + padding, Math.Min(requestedX, taskbarBounds.Right - padding - minimum));
                    eligible = eligible.Where(r => r.Left < requestedX + desired && r.Right > requestedX).ToList();
                    if (eligible.Count == 0)
                        return Rectangle.Empty;
                }
                candidate = eligible.OrderBy(r => Math.Abs(Math.Max(r.Left, Math.Min(requestedX, r.Right - Math.Min(desired, r.Width))) - requestedX)).First();
                int width = Math.Min(desired, candidate.Width);
                int x = Math.Max(candidate.Left, Math.Min(requestedX, candidate.Right - width));
                return new Rectangle(x, taskbarBounds.Top + (taskbarBounds.Height - height) / 2, width, height);
            }

            var full = eligible.Where(r => r.Width >= desired).ToList();
            if (mode == "right")
                candidate = full.Count > 0 ? full.Last() : eligible.Last();
            else if (mode == "left")
                candidate = full.Count > 0 ? full.First() : eligible.First();
            else
            {
                candidate = full.Count > 0 ? full.First() : eligible.OrderByDescending(r => r.Width).First();
            }

            if (candidate.Width < minimum)
                return Rectangle.Empty;
            int actualWidth = Math.Min(desired, candidate.Width);
            return new Rectangle(mode == "right" ? candidate.Right - actualWidth : candidate.Left, taskbarBounds.Top + (taskbarBounds.Height - height) / 2, actualWidth, height);
        }
    }
}
