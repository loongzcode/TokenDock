using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace TokenDock
{
    static class ItemIcons
    {
        static readonly Dictionary<string, Bitmap> brands = LoadBrands();
        static Dictionary<string, Bitmap> LoadBrands()
        {
            var result = new Dictionary<string, Bitmap>();
            foreach (string name in new[]
            {
                "codex",
                "claude",
                "deepseek"
            }

            )
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TokenDock.Brands." + name + ".png"))
                {
                    if (stream == null)
                        continue;
                    using (var image = Image.FromStream(stream))
                        result[name] = new Bitmap(image);
                }
            }

            return result;
        }

        public static bool HasBrand(string name)
        {
            return brands.ContainsKey(name);
        }

        public static string Kind(TaskbarItem item)
        {
            if (brands.ContainsKey(item.Key ?? ""))
                return item.Key;
            return !string.IsNullOrEmpty(item.IconKind) ? item.IconKind : DeviceIconResolver.Resolve(item.Tooltip ?? item.Label);
        }

        public static void Draw(Graphics graphics, TaskbarItem item, float x, float y, float size)
        {
            string kind = Kind(item);
            Bitmap image;
            if (brands.TryGetValue(kind, out image))
            {
                var state = graphics.Save();
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float ratio = Math.Min(size / image.Width, size / image.Height);
                float width = image.Width * ratio, height = image.Height * ratio;
                graphics.DrawImage(image, new RectangleF(x + (size - width) / 2, y + (size - height) / 2, width, height));
                graphics.Restore(state);
                return;
            }

            DrawDevice(graphics, kind, x, y, size, Theme.Text);
        }

        public static void DrawDevice(Graphics graphics, string kind, float x, float y, float size, Color color)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(x, y);
            graphics.ScaleTransform(size / 20, size / 20);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.35f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (kind == "keyboard")
                {
                    using (var path = Theme.Path(new RectangleF(1, 4, 18, 12), 2))
                        graphics.DrawPath(pen, path);
                    for (int row = 0; row < 2; row++)
                        for (int column = 0; column < 4; column++)
                            graphics.DrawLine(pen, 4 + column * 4, 7 + row * 3, 4.6f + column * 4, 7 + row * 3);
                    graphics.DrawLine(pen, 5, 13, 15, 13);
                }
                else if (kind == "mouse")
                {
                    using (var path = Theme.Path(new RectangleF(4.5f, 1, 11, 18), 5))
                        graphics.DrawPath(pen, path);
                    graphics.DrawLine(pen, 10, 1.5f, 10, 8);
                    graphics.DrawLine(pen, 5, 8, 15, 8);
                    graphics.DrawLine(pen, 10, 4, 10, 5.5f);
                }
                else if (kind == "headphones")
                {
                    graphics.DrawArc(pen, 2, 2, 16, 16, 180, 180);
                    using (var left = Theme.Path(new RectangleF(2, 10, 4, 7), 1.5f))
                        graphics.DrawPath(pen, left);
                    using (var right = Theme.Path(new RectangleF(14, 10, 4, 7), 1.5f))
                        graphics.DrawPath(pen, right);
                }
                else
                {
                    graphics.DrawLines(pen, new[] { new PointF(10, 1), new PointF(15, 6), new PointF(5, 14), new PointF(10, 19), new PointF(10, 1), new PointF(10, 19), new PointF(15, 14), new PointF(5, 6) });
                }
            }

            graphics.Restore(state);
        }
    }
}
