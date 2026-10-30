using System;
using System.IO;

namespace TokenDock
{
    static class AppPaths
    {
        public static readonly string BinaryDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string DataDirectory
        {
            get
            {
                return File.Exists(Path.Combine(BinaryDirectory, "portable.mode")) ? Path.Combine(BinaryDirectory, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TokenDock");
            }
        }

        public static string SettingsFile
        {
            get
            {
                return Path.Combine(DataDirectory, "settings.json");
            }
        }

        public static string ClaudeSnapshot
        {
            get
            {
                return Path.Combine(DataDirectory, "claude-status.json");
            }
        }

        public static void MigrateLegacy(string legacyData)
        {
            if (string.IsNullOrWhiteSpace(legacyData) || File.Exists(SettingsFile))
                return;
            string source = Path.GetFullPath(legacyData);
            foreach (string name in new[]
            {
                "settings.json",
                "claude-status.json"
            }

            )
            {
                string file = Path.Combine(source, name), target = Path.Combine(DataDirectory, name);
                if (File.Exists(file) && !File.Exists(target))
                {
                    string value = File.ReadAllText(file);
                    if (name == "settings.json")
                        value = Json.Write(Settings.FromJson(value));
                    Json.AtomicWrite(target, value);
                }
            }
        }
    }
}
