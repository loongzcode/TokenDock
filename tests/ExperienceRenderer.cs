using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TokenDock
{
    static class ExperienceRenderer
    {
        public static void Render(string directory)
        {
            Directory.CreateDirectory(directory);
            var items = new List<TaskbarItem>
            {
                new TaskbarItem
                {
                    Key = "codex",
                    Label = "Codex",
                    Value = "90%"
                },
                new TaskbarItem
                {
                    Key = "deepseek",
                    Label = "DS",
                    Value = "¥12.34"
                },
                new TaskbarItem
                {
                    Key = "device:keyboard",
                    Label = "RK R98 5.0",
                    Value = "100%",
                    Battery = 100
                }
            };
            foreach (bool compact in new[]
            {
                false,
                true
            }

            )
                using (var bitmap = new Bitmap(TaskbarDrawing.Measure(items, compact), 28))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Theme.Bg);
                    TaskbarDrawing.Paint(graphics, 28, bitmap.Width, items, false, false, true, compact);
                    bitmap.Save(Path.Combine(directory, compact ? "taskbar-compact.png" : "taskbar-full.png"), ImageFormat.Png);
                }

            var content = new HoverContent
            {
                Title = "Codex",
                Footer = "更新于 14:30:00 · 单击查看详情"
            };
            content.Lines.AddRange(new[] { "5小时 · 剩余 90%", "距重置 2小时 18分钟", "7天 · 剩余 68%", "距重置 4天 2小时", "账户累计 Token 9,400,000,000" });
            foreach (float scale in new[]
            {
                1f,
                1.5f,
                2f
            }

            )
                using (var preview = new HoverPreview())
                {
                    preview.Prepare(content, scale, 1200);
                    using (var bitmap = new Bitmap(preview.Width, preview.Height))
                    {
                        preview.DrawToBitmap(bitmap, preview.ClientRectangle);
                        bitmap.Save(Path.Combine(directory, "hover-" + (int)(scale * 100) + ".png"), ImageFormat.Png);
                    }
                }
        }
    }
}
