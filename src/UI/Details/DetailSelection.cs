using System;

namespace TokenDock
{
    static class DetailSelection
    {
        public static string Normalize(string key)
        {
            return key == "codex" || key == "claude" || key == "deepseek" || (!string.IsNullOrEmpty(key) && key.StartsWith("device:", StringComparison.Ordinal) && key.Length > 7) ? key : null;
        }

        public static bool Includes(string selection, string item)
        {
            return selection == null || string.Equals(selection, item, StringComparison.Ordinal);
        }

        public static int Height(string selection, UsageMonitor monitor)
        {
            if (selection == null) return 600;
            if (selection.StartsWith("device:")) return 320;
            var snapshot = selection == "codex" ? monitor.Codex : selection == "claude" ? monitor.Claude : monitor.DeepSeek;
            return Math.Min(600, 320 + Math.Min(3, snapshot.Quotas.Count) * 72);
        }
    }
}
