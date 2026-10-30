using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TokenDock
{
    sealed class HistoryStore
    {
        readonly string directory;
        readonly object gate = new object ();
        DateTime pruned = DateTime.MinValue;
        public HistoryStore(string path)
        {
            directory = Path.GetFullPath(path);
        }

        public void Append(IEnumerable<HistoryPoint> points, DateTime now)
        {
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                foreach (var group in points.Where(point => point.Valid).GroupBy(point => SystemClock.FromUnix(point.Time).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                {
                    string path = Path.Combine(directory, group.Key + ".jsonl");
                    using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        if (stream.Length > 0)
                        {
                            stream.Seek(-1, SeekOrigin.End);
                            bool needsNewline = stream.ReadByte() != 10;
                            stream.Seek(0, SeekOrigin.End);
                            if (needsNewline)
                                stream.WriteByte(10);
                        }

                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                            foreach (var point in group)
                                writer.WriteLine(Json.Write(point));
                    }
                }

                if (pruned.Date != now.ToUniversalTime().Date)
                {
                    foreach (string file in Directory.GetFiles(directory, "????-??-??.jsonl"))
                    {
                        DateTime day;
                        if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) && day.Date < now.ToUniversalTime().Date.AddDays(-29))
                            File.Delete(file);
                    }

                    pruned = now.ToUniversalTime();
                }
            }
        }

        public List<HistoryPoint> Read(DateTime from, DateTime to)
        {
            var result = new List<HistoryPoint>();
            double start = HistoryCapture.Unix(from), end = HistoryCapture.Unix(to);
            lock (gate)
            {
                if (!Directory.Exists(directory))
                    return result;
                foreach (string path in Directory.GetFiles(directory, "????-??-??.jsonl").OrderByDescending(path => path, StringComparer.Ordinal))
                {
                    DateTime day;
                    if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day) || day.Date < from.ToUniversalTime().Date || day.Date > to.ToUniversalTime().Date)
                        continue;
                    foreach (string line in File.ReadLines(path))
                    {
                        if (line.Length > 8192)
                            continue;
                        try
                        {
                            var point = Json.Serializer().Deserialize<HistoryPoint>(line);
                            if (point != null && point.Valid && point.Time >= start && point.Time <= end)
                                result.Add(point);
                        }
                        catch
                        { /* One incomplete record must not hide the rest of a day. */
                        }
                    }

                    if (result.Count >= 100000)
                        break;
                }
            }

            return result.GroupBy(point => new { point.Series, point.Time, point.Value, point.Reset }).Select(group => group.First()).OrderBy(point => point.Time).ToList();
        }
    }
}
