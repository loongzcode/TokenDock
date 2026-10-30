using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenDock
{
    static class HistorySeries
    {
        public static List<HistoryPoint> Select(IEnumerable<HistoryPoint> points, string series, double start, double end)
        {
            return points.Where(point => point.Valid && point.Series == series && point.Time >= start && point.Time <= end).OrderBy(point => point.Time).ToList();
        }

        public static List<List<HistoryPoint>> Segments(IEnumerable<HistoryPoint> points)
        {
            var segments = new List<List<HistoryPoint>>();
            HistoryPoint previous = null;
            foreach (var point in points.OrderBy(point => point.Time))
            {
                if (previous == null || point.Time - previous.Time > 1800 || point.Reset != previous.Reset || point.Series != previous.Series)
                    segments.Add(new List<HistoryPoint>());
                segments[segments.Count - 1].Add(point);
                previous = point;
            }

            return segments;
        }
    }
}
