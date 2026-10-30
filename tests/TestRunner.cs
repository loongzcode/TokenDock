using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    static class TestRunner
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (args.Length >= 2 && args[0] == "--render-settings")
                {
                    var monitor = new UsageMonitor(new Settings());
                    using (var bar = new TaskbarControl(monitor))
                    using (var ui = new SettingsWindow(monitor, bar))
                    {
                        SettingsRenderer.Render(ui, args[1], args.Length > 2 ? int.Parse(args[2]) : 0);
                    }
                }
                else if (args.Length >= 2 && args[0] == "--render-experience")
                {
                    ExperienceRenderer.Render(args[1]);
                }
                else if (args.Length >= 2 && args[0] == "--render-history")
                {
                    HistoryRenderer.Render(args[1]);
                }
                else if (args.Length >= 2 && args[0] == "--render-overview")
                {
                    OverviewRenderer.Render(args[1]);
                }
                else if (args.Length >= 2 && args[0] == "--render-details-updates")
                {
                    DetailsAndUpdateTests.Render(args[1]);
                }
                else if (args.Length >= 2 && (args[0] == "--check-update" || args[0] == "--download-update"))
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                    using (var client = new UpdateClient())
                    {
                        var release = client.CheckAsync().GetAwaiter().GetResult();
                        if (args[0] == "--download-update")
                            client.DownloadAsync(release, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[1]), "verified-update-download"), null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        System.IO.File.WriteAllText(args[1], Json.Write(new { tag = release.Tag, package = release.FileName, newer = release.IsNewerThan(new Version(AppIdentity.Version)) }));
                    }
                }
                else if (args.Length >= 4 && args[0] == "--probe-flx1")
                {
                    var settings = new Settings();
                    settings.Flx1Receivers.Add(new ReceiverBinding { Fingerprint = args[2], BluetoothId = args[3] });
                    var snapshot = new Flx1ReceiverSource().ReadAsync(settings).GetAwaiter().GetResult();
                    System.IO.File.WriteAllText(args[1], Json.Write(snapshot), new System.Text.UTF8Encoding(false));
                }
                else
                {
                    Console.WriteLine(RegressionTests.Run());
                    Console.WriteLine(FeatureTests.Run());
                    Console.WriteLine(TaskbarExperienceTests.Run());
                    Console.WriteLine(HistoryAndOrderTests.Run());
                    Console.WriteLine(IconAndLockTests.Run());
                    Console.WriteLine(ModernUiTests.Run());
                    Console.WriteLine(ReceiverPowerTests.Run());
                    Console.WriteLine(DetailsAndUpdateTests.Run());
                }

                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
