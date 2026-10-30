using System;
using System.IO;
using System.Text;
using System.Threading;

namespace TokenDock
{
    static class CommandLine
    {
        public const string QuitEvent = "Local\\TokenDock.Quit";
        public static bool TryRun(string[] args, out int exitCode)
        {
            exitCode = 0;
            int bindIndex = Array.IndexOf(args, "--link-flx1");
            if (bindIndex >= 0)
            {
                if (args.Length <= bindIndex + 2)
                    throw new ArgumentException("需要接收器指纹与蓝牙设备 ID。");
                string fingerprint = args[bindIndex + 1].ToUpperInvariant();
                Guid peer;
                if (!System.Text.RegularExpressions.Regex.IsMatch(fingerprint, "^[A-F0-9]{24}$") || !Guid.TryParse(args[bindIndex + 2], out peer))
                    throw new ArgumentException("无效的接收器绑定。");
                if (!Flx1ReceiverSource.ValidateBinding(fingerprint))
                    throw new InvalidOperationException("当前接收器未通过已验证的 FLX1 型号检查。");
                var settings = Settings.Load();
                settings.Flx1Receivers.RemoveAll(binding => binding.Fingerprint == fingerprint);
                settings.Flx1Receivers.Add(new ReceiverBinding { Fingerprint = fingerprint, BluetoothId = peer.ToString("D") });
                settings.Save();
                return true;
            }

            if (Array.IndexOf(args, "--quit") >= 0)
            {
                try
                {
                    using (var signal = EventWaitHandle.OpenExisting(QuitEvent))
                        signal.Set();
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                }

                return true;
            }

            if (Array.IndexOf(args, "--claude-statusline") >= 0)
            {
                object snapshot = ClaudeProvider.SanitizeSnapshot(Json.Read(Console.In.ReadToEnd()));
                bool taken = false;
                using (var mutex = new Mutex(false, "Local\\TokenDockClaudeSnapshot"))
                {
                    try
                    {
                        try
                        {
                            taken = mutex.WaitOne(2000);
                        }
                        catch (AbandonedMutexException)
                        {
                            taken = true;
                        }

                        if (taken)
                            Json.AtomicWrite(AppPaths.ClaudeSnapshot, Json.Write(snapshot));
                    }
                    finally
                    {
                        if (taken)
                            mutex.ReleaseMutex();
                    }
                }

                Console.Out.WriteLine("Claude " + ClaudeProvider.Parse(snapshot, DateTime.Now).Compact);
                return true;
            }

            if (Array.IndexOf(args, "--probe") >= 0 && args.Length > 1)
            {
                var monitor = new UsageMonitor(Settings.Load());
                monitor.Refresh(true).GetAwaiter().GetResult();
                var output = new
                {
                    version = AppIdentity.Version,
                    codex = monitor.Codex.Status,
                    claude = monitor.Claude.Status,
                    deepseek = monitor.DeepSeek.Status,
                    bluetooth = new
                    {
                        status = monitor.BluetoothStatus,
                        count = monitor.Devices.Count
                    }
                };
                File.WriteAllText(args[1], Json.Write(output), new UTF8Encoding(false));
                return true;
            }

            return false;
        }

        public static string Value(string[] args, string option)
        {
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
