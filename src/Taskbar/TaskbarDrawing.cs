using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace TokenDock
{
    static class TaskbarDrawing
    {
        static float TextWidth(Graphics g, Font font, string value)
        {
            return (float)Math.Ceiling(g.MeasureString(value, font, int.MaxValue).Width);
        }

        static string ItemLabel(TaskbarItem item, bool compact)
        {
            return item.Label;
        }

        static float LabelWidth(Graphics g, Font font, TaskbarItem item, bool compact)
        {
            return compact ? 18 : TextWidth(g, font, ItemLabel(item, false)) + 24;
        }

        static float ItemWidth(Graphics g, Font font, TaskbarItem item, bool compact)
        {
            return LabelWidth(g, font, item, compact) + 6 + TextWidth(g, font, item.Value);
        }

        public static int Measure(IList<TaskbarItem> items, bool compact = false, bool showGrip = false)
        {
            if (items.Count == 0)
                return 0;
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            using (var font = Theme.PixelFont(12, false))
                return (int)Math.Ceiling((showGrip ? 32 : 16) + items.Sum(item => ItemWidth(g, font, item, compact)) + 14 * (items.Count - 1));
        }

        public static string HitTest(IList<TaskbarItem> items, int contentWidth, float logicalX, bool preferCompact)
        {
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = Theme.PixelFont(12, false))
            {
                bool compact = preferCompact || Measure(items) > contentWidth;
                float x = 8;
                foreach (var item in items)
                {
                    float width = ItemWidth(graphics, font, item, compact);
                    if (x + width > contentWidth - 8)
                        break;
                    if (logicalX >= x && logicalX < x + width)
                        return item.Key;
                    x += width + 14;
                }
            }

            return null;
        }

        public static void Paint(Graphics g, float height, int contentWidth, IList<TaskbarItem> items, bool hover, bool showDropTarget, bool validDrop, bool preferCompact = false, bool showGrip = false)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (showDropTarget)
            {
                bool valid = validDrop;
                Theme.Round(g, new RectangleF(1, 1, contentWidth - 2, height - 2), 6, Color.FromArgb(valid ? 45 : 20, 0, 120, 212));
                using (var path = Theme.Path(new RectangleF(1, 1, contentWidth - 2, height - 2), 6))
                using (var pen = new Pen(Color.FromArgb(180, 0, 120, 212), 1))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, path);
                }

                Theme.TextAt(g, valid ? "松手固定到此处" : "拖回任务栏以固定", 12, false, Theme.Text, new RectangleF(8, 0, contentWidth - 16, height), StringAlignment.Center);
                return;
            }

            if (hover)
                Theme.Round(g, new RectangleF(0, 2, contentWidth, height - 4), 6, Theme.Light ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(38, 255, 255, 255));
            if (showGrip)
                using (var brush = new SolidBrush(Theme.Muted))
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 2; col++)
                            g.FillEllipse(brush, 6 + col * 4, height / 2 - 5 + row * 4, 2, 2);
            float x = showGrip ? 24 : 8;
            using (var font = Theme.PixelFont(12, false))
            {
                bool compact = preferCompact || Measure(items, false, showGrip) > contentWidth;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    float width = ItemWidth(g, font, item, compact), labelWidth = LabelWidth(g, font, item, compact);
                    if (x + width > contentWidth - 8)
                    {
                        Theme.TextAt(g, "…", 12, false, Theme.Muted, x, 0, Math.Max(0, contentWidth - x - 4), height);
                        break;
                    }

                    ItemIcons.Draw(g, item, x, (height - 18) / 2, 18);
                    if (!compact)
                        Theme.TextAt(g, ItemLabel(item, false), 12, false, Theme.Text, x + 24, 0, labelWidth - 22, height);
                    Theme.TextAt(g, item.Value, 12, false, Theme.Text, x + labelWidth + 6, 0, width - labelWidth - 4, height);
                    x += width + 14;
                }
            }
        }
    }
}
