using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace TokenDock
{
    static class HistoryRenderer
    {
        public static void Render(string directory)
        {
            Directory.CreateDirectory(directory);
            DateTime now = DateTime.UtcNow;
            var points = new List<HistoryPoint>();
            double unix = HistoryCapture.Unix(now);
            for (int i = 0; i < 90; i++)
                points.Add(new HistoryPoint { Time = unix - (89 - i) * 300, Series = "quota:codex", Label = "Codex · 5小时剩余额度", Unit = "%", Value = 100 - i / 2m });
            using (var chart = new HistoryChart())
            {
                chart.Size = new Size(720, 300);
                chart.SetData(points, now.AddDays(-1), now);
                Save(chart, Path.Combine(directory, "history-chart.png"));
                chart.SetData(new List<HistoryPoint>(), now.AddDays(-1), now);
                Save(chart, Path.Combine(directory, "history-empty.png"));
            }

            using (var order = new ItemOrderWindow(new[] { new OrderChoice { Key = "deepseek", Label = "DeepSeek" }, new OrderChoice { Key = "codex", Label = "Codex" }, new OrderChoice { Key = "device:key", Label = "RK R98 5.0" } }))
            {
                order.PerformLayout();
                Save(order, Path.Combine(directory, "item-order.png"));
            }

            var monitor = new UsageMonitor(new Settings());
            using (var recorder = new HistoryRecorder(monitor, new HistoryStore(Path.Combine(directory, "unused"))))
            using (var window = new HistoryWindow(new HistoryStore(Path.Combine(directory, "unused")), recorder, monitor))
            {
                window.PresentData(points);
                window.PerformLayout();
                Save(window, Path.Combine(directory, "history-window.png"));
            }
        }

        static void Save(Control control, string destination)
        {
            if (control is Form)
            {
                SettingsRenderer.RenderWindow((Form)control, destination);
                return;
            }

            using (var bitmap = new Bitmap(control.Width, control.Height))
            {
                control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
                bitmap.Save(destination, ImageFormat.Png);
            }
        }
    }
}
