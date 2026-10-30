using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class HistoryChart : Control
    {
        List<HistoryPoint> points = new List<HistoryPoint>();
        readonly ToolTip tooltip = new ToolTip();
        readonly List<KeyValuePair<PointF, HistoryPoint>> hits = new List<KeyValuePair<PointF, HistoryPoint>>();
        string lastTip;
        double start, end;
        public HistoryChart()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            AccessibleName = "历史趋势采样图";
            SetStyle(ControlStyles.ResizeRedraw, true);
            MouseMove += Hover;
            MouseLeave += delegate
            {
                tooltip.Hide(this);
                lastTip = null;
            };
        }

        public void SetData(List<HistoryPoint> values, DateTime from, DateTime to)
        {
            points = values;
            start = HistoryCapture.Unix(from);
            end = HistoryCapture.Unix(to);
            AccessibleDescription = points.Count == 0 ? "所选范围暂无采样" : points.Count + " 个真实采样，最近值 " + points.Last().Value + points.Last().Unit;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Card);
            hits.Clear();
            float scale = g.DpiX / 96f;
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float width = Width / scale, height = Height / scale;
            if (points.Count == 0)
            {
                Theme.TextAt(g, "所选范围暂无历史数据", 15, true, Theme.Text, new RectangleF(16, height / 2 - 32, width - 32, 30), StringAlignment.Center);
                Theme.TextAt(g, "开启记录后逐步积累，不补造过去的用量", 12, false, Theme.Muted, new RectangleF(16, height / 2, width - 32, 26), StringAlignment.Center);
                return;
            }

            if (width < 180 || height < 140)
                return;
            var plot = new RectangleF(72, 18, width - 92, height - 68);
            bool percentage = points[0].Unit == "%";
            decimal max = percentage ? 100 : Math.Max(1, points.Max(p => p.Value) * 1.12m);
            using (var grid = new Pen(Theme.Line))
            {
                for (int row = 0; row <= 4; row++)
                {
                    float y = plot.Top + plot.Height * row / 4;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    decimal tick = max * (4 - row) / 4;
                    Theme.TextAt(g, tick.ToString(percentage ? "0" : "0.##") + (percentage ? "%" : ""), 11, false, Theme.Muted, new RectangleF(4, y - 10, 60, 20), StringAlignment.Far);
                }
            }

            double duration = Math.Max(1, end - start);
            Func<HistoryPoint, PointF> coordinate = point => new PointF(plot.Left + (float)((point.Time - start) / duration) * plot.Width, plot.Bottom - (float)(point.Value / max) * plot.Height);
            using (var pen = new Pen(Theme.Accent, 1.7f))
            using (var dot = new SolidBrush(Theme.Accent))
            {
                foreach (var segment in HistorySeries.Segments(points))
                {
                    var positions = segment.Select(coordinate).ToArray();
                    if (positions.Length > 1)
                        g.DrawLines(pen, positions);
                }

                foreach (var point in points)
                {
                    PointF position = coordinate(point);
                    g.FillEllipse(dot, position.X - 2.3f, position.Y - 2.3f, 4.6f, 4.6f);
                    hits.Add(new KeyValuePair<PointF, HistoryPoint>(new PointF(position.X * scale, position.Y * scale), point));
                }
            }

            string format = duration <= 43200 ? "HH:mm" : "MM-dd HH:mm";
            Theme.TextAt(g, SystemClock.FromUnix(start).ToLocalTime().ToString(format), 11, false, Theme.Muted, plot.Left, plot.Bottom + 10, plot.Width / 2, 22);
            Theme.TextAt(g, SystemClock.FromUnix(end).ToLocalTime().ToString(format), 11, false, Theme.Muted, new RectangleF(plot.Left + plot.Width / 2, plot.Bottom + 10, plot.Width / 2, 22), StringAlignment.Far);
        }

        void Hover(object sender, MouseEventArgs e)
        {
            KeyValuePair<PointF, HistoryPoint> hit = default(KeyValuePair<PointF, HistoryPoint>);
            float nearest = float.MaxValue;
            foreach (var candidate in hits)
            {
                float distance = Math.Abs(candidate.Key.X - e.X) + Math.Abs(candidate.Key.Y - e.Y);
                if (distance < nearest)
                {
                    nearest = distance;
                    hit = candidate;
                }
            }

            string text = hit.Value != null && Math.Abs(hit.Key.X - e.X) < 14 && Math.Abs(hit.Key.Y - e.Y) < 14 ? SystemClock.FromUnix(hit.Value.Time).ToLocalTime().ToString("MM-dd HH:mm:ss") + "\n" + hit.Value.Value.ToString("0.##") + " " + hit.Value.Unit : null;
            if (text == lastTip)
                return;
            lastTip = text;
            tooltip.Hide(this);
            if (text != null)
                tooltip.Show(text, this, e.X + 12, e.Y - 40, 4000);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                tooltip.Dispose();
            base.Dispose(disposing);
        }
    }
}
