using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace TokenDock
{
    static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                int code;
                if (CommandLine.TryRun(args, out code))
                    return code;
                using (var mutex = new Mutex(false, "Local\\TokenDockApp"))
                {
                    bool owned;
                    try
                    {
                        owned = mutex.WaitOne(0);
                    }
                    catch (AbandonedMutexException)
                    {
                        owned = true;
                    }

                    if (!owned)
                    {
                        MessageBox.Show("TokenDock 已在运行，可通过系统托盘打开。", "TokenDock");
                        return 0;
                    }

                    try
                    {
                        AppPaths.MigrateLegacy(CommandLine.Value(args, "--migrate-from") ?? Path.Combine(AppPaths.BinaryDirectory, "data"));
                        StartupRegistration.UpdatePathIfEnabled();
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        using (var controller = new ApplicationController(Settings.Load(), !args.Contains("--details")))
                            Application.Run(controller);
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }

                return 0;
            }
            catch (Exception error)
            {
                try
                {
                    Json.AtomicWrite(Path.Combine(AppPaths.DataDirectory, "startup-error.json"), Json.Write(new { error = error.Message, type = error.GetType().FullName, stack = error.StackTrace }));
                }
                catch
                {
                }

                MessageBox.Show("TokenDock 启动失败：" + error.Message, "TokenDock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
        }
    }
}
