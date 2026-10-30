using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    static class DetailsAndUpdateTests
    {
        static readonly byte[] Package = Encoding.ASCII.GetBytes("PK-test-update-package");
        static readonly string PackageHash = Hash(Package);
        public static string Run()
        {
            int count = 0;
            Action<bool, string> check = delegate(bool valid, string message) { if (!valid) throw new Exception("FAIL: " + message); count++; };
            var now = DateTime.Now;
            check(!UsageHealth.Read(new UsageSnapshot { Status = "未配置" }, now, 120).IsIssue, "unconfigured accounts are not read errors");
            var good = new UsageSnapshot { Name = "Codex", Updated = now, LifetimeTokens = 0 };
            check(!UsageHealth.Read(good, now, 120).IsIssue, "successful real zero is healthy");
            var failed = UsageSnapshot.Failed(good, "private fixture text");
            check(UsageHealth.Read(failed, now, 120).Label == "读取失败", "failed cached data is distinguished from an unconnected account");
            check(!UsageHealth.Read(failed, now, 120).Hint.Contains("private"), "status hints do not echo source responses");
            check(UsageHealth.Read(new UsageSnapshot { Updated = now.AddMinutes(-11), InputTokens = 10 }, now, 120).Label == "数据过期", "old results are clearly marked stale");
            var expired = new UsageSnapshot { Updated = now, Quotas = new List<QuotaWindow> { new QuotaWindow { Resets = SystemClock.UnixNow() - 60 } } };
            check(UsageHealth.Read(expired, now, 120).Label == "等待更新", "expired quota cycles are distinguished from connection failures");
            var monitor = DemoMonitor();
            check(UsageHealth.Issues(monitor, "任务栏空间不足，可从托盘查看详情", now).Count == 2, "placement failures and provider failures are both reachable from tray status");
            monitor.Config.ShowDeepSeek = false;
            check(UsageHealth.Issues(monitor, "任务栏控件已显示", now).Count == 0, "disabled providers do not inflate tray issue counts");
            check(DetailSelection.Normalize("random") == null && DetailSelection.Normalize("device:") == null, "unknown click targets return to all details");
            check(DetailSelection.Normalize("device:mouse") == "device:mouse" && !DetailSelection.Includes("device:mouse", "device:keyboard"), "device routing uses identity rather than device name");
            check(DetailSelection.Height("device:mouse", monitor) < DetailSelection.Height(null, monitor), "single-device details use a compact popup");
            using (var view = new OverviewControl(monitor, null) { Size = new Size(420, 600) })
            {
                view.SelectItem("deepseek");
                check(view.SelectedKey == "deepseek" && !view.AccessibleDescription.Contains("demo-codex"), "selected details do not expose unrelated account sections");
                var all = (Button)view.Controls.Find("ShowAllDetails", true).Single();
                typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(all, new object[] { EventArgs.Empty });
                check(view.SelectedKey == null && view.AccessibleDescription.Contains("demo-codex"), "all-details button restores the complete view");
            }
            using (var bar = new TaskbarControl(monitor))
            {
                string target = null;
                bar.OpenItemDetails += delegate(string key) { target = key; };
                typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 12, 12, 0) });
                check(target == "codex", "actual taskbar click emits the item key");
            }
            check(Settings.FromJson("{}").CheckUpdatesEnabled && !Settings.FromJson("{\"CheckUpdatesEnabled\":false}").CheckUpdatesEnabled, "automatic update checks retain an explicit opt-out");
            var context = SynchronizationContext.Current;
            try { SynchronizationContext.SetSynchronizationContext(null); RunNetworkChecks(check).GetAwaiter().GetResult(); }
            finally { SynchronizationContext.SetSynchronizationContext(context); }
            return "PASS " + count + " details and update checks";
        }

        static async Task RunNetworkChecks(Action<bool, string> check)
        {
            var release = UpdateRelease.Parse(Json.Read(Metadata()));
            check(release.IsNewerThan(new Version(1, 1, 0, 0)) && !release.IsNewerThan(new Version(1, 2, 3, 0)) && !release.IsNewerThan(new Version(2, 0, 0)), "update comparison never offers equal versions or downgrades");
            check(release.ReadChecksum(PackageHash + "  " + release.FileName + "\r\n") == PackageHash, "SHA256 entry must match the exact runtime filename");
            check(Rejects(delegate { UpdateRelease.Parse(Json.Read(Metadata().Replace("\"prerelease\":false", "\"prerelease\":true"))); }), "prereleases are not offered as stable updates");
            check(Rejects(delegate { UpdateRelease.Parse(Json.Read(Metadata().Replace("https://github.com/loongzcode/TokenDock/releases/download", "https://example.invalid/releases/download"))); }), "foreign asset destinations are rejected");
            check(Rejects(delegate { UpdateRelease.Parse(Json.Read(Metadata().Replace("SHA256SUMS.txt", "missing.txt"))); }), "updates without checksums cannot be downloaded");
            check(Rejects(delegate { release.ReadChecksum(PackageHash + "  " + release.FileName + "\n" + PackageHash + "  " + release.FileName); }), "ambiguous duplicate checksum entries are rejected");
            check(Rejects(delegate { release.ReadChecksum(new string('0', 64) + "  " + release.FileName); }), "release digest and checksum must agree");
            check(Rejects(delegate { release.ReadChecksum(PackageHash + "  ../" + release.FileName); }), "checksum path traversal does not match the package");
            using (var client = new UpdateClient(new MemoryHandler()))
            {
                var latest = await client.CheckAsync();
                check(latest.Tag == "v1.2.3", "check reads a stable release through the HTTP boundary");
            }
            var pending = new MemoryHandler { Gate = new TaskCompletionSource<bool>() };
            using (var client = new UpdateClient(pending))
            {
                var first = client.CheckAsync(); var second = client.CheckAsync();
                check(object.ReferenceEquals(first, second), "concurrent update checks share the in-flight request");
                pending.Gate.SetResult(true); await first;
                check(pending.Requests == 1 && !pending.SawAuthorization, "public update checks send no account authorization");
            }
            using (var client = new UpdateClient(new MemoryHandler { Failure = HttpStatusCode.Forbidden }))
            {
                bool limited = false;
                try { await client.CheckAsync(); } catch (InvalidOperationException error) { limited = error.Message.Contains("频率"); }
                check(limited, "rate limiting is a retryable status rather than a false latest-version result");
            }
            var fallback = new MemoryHandler { ApiLimited = true };
            using (var client = new UpdateClient(fallback))
            {
                var latest = await client.CheckAsync();
                check(latest.Tag == "v1.2.3" && latest.Size == Package.Length, "API rate limiting falls back to the same repository's public stable release");
                check(!fallback.SawAuthorization && fallback.Requests == 4, "public fallback verifies package metadata and checksums without credentials");
            }
            string directory = Path.Combine(Path.GetTempPath(), "TokenDock-update-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var client = new UpdateClient(new MemoryHandler()))
                {
                    string file = await client.DownloadAsync(release, directory, null, CancellationToken.None);
                    check(File.Exists(file) && Hash(File.ReadAllBytes(file)) == PackageHash, "download becomes a ready ZIP only after hash and size validation");
                }
                foreach (string mode in new[] { "corrupt", "truncated", "oversized", "cancelled" })
                {
                    string attempt = Path.Combine(directory, mode);
                    using (var client = new UpdateClient(new MemoryHandler { Mode = mode }))
                    {
                        bool rejected = false;
                        try { await client.DownloadAsync(release, attempt, null, CancellationToken.None); } catch (InvalidDataException) { rejected = true; } catch (OperationCanceledException) { rejected = mode == "cancelled"; }
                        check(rejected, mode + " package is rejected");
                        check(!Directory.Exists(attempt) || Directory.GetFiles(attempt, "*", SearchOption.AllDirectories).Length == 0, mode + " package leaves no ready or partial file");
                    }
                }
                using (var cancellation = new CancellationTokenSource())
                using (var client = new UpdateClient(new MemoryHandler()))
                {
                    cancellation.Cancel(); bool cancelled = false;
                    try { await client.DownloadAsync(release, Path.Combine(directory, "cancel"), null, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
                    check(cancelled && !Directory.Exists(Path.Combine(directory, "cancel")), "cancelled downloads do not create an installable file");
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        static bool Rejects(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static string Metadata()
        {
            return Json.Write(new { tag_name = "v1.2.3", draft = false, prerelease = false, assets = new[] {
                new { name = "TokenDock-1.2.3.zip", browser_download_url = UpdateRelease.RepositoryUrl + "/releases/download/v1.2.3/TokenDock-1.2.3.zip", size = Package.Length, digest = "sha256:" + PackageHash },
                new { name = "SHA256SUMS.txt", browser_download_url = UpdateRelease.RepositoryUrl + "/releases/download/v1.2.3/SHA256SUMS.txt", size = 100, digest = "" }
            } });
        }

        internal static UsageMonitor DemoMonitor()
        {
            var monitor = new UsageMonitor(new Settings(), new[] { new DemoProvider("codex"), new DemoProvider("deepseek") }, new DemoDevices());
            var context = SynchronizationContext.Current;
            try { SynchronizationContext.SetSynchronizationContext(null); monitor.Refresh(true).GetAwaiter().GetResult(); }
            finally { SynchronizationContext.SetSynchronizationContext(context); }
            return monitor;
        }

        public static void Render(string directory)
        {
            Directory.CreateDirectory(directory);
            var monitor = DemoMonitor();
            foreach (string key in new[] { "codex", "deepseek", "device:mouse", "all" })
                using (var view = new OverviewControl(monitor, null) { Size = new Size(420, DetailSelection.Height(key == "all" ? null : key, monitor)) })
                using (var image = new Bitmap(view.Width, view.Height))
                {
                    view.SelectItem(key == "all" ? null : key);
                    view.DrawToBitmap(image, view.ClientRectangle);
                    image.Save(Path.Combine(directory, "details-" + key.Replace(':', '-') + ".png"));
                }
            using (var client = new UpdateClient(new MemoryHandler()))
            using (var window = new UpdateWindow(client))
            {
                var context = SynchronizationContext.Current;
                try { SynchronizationContext.SetSynchronizationContext(null); window.CheckAsync().GetAwaiter().GetResult(); }
                finally { SynchronizationContext.SetSynchronizationContext(context); }
                SettingsRenderer.RenderWindow(window, Path.Combine(directory, "update.png"));
            }
        }

        sealed class MemoryHandler : HttpMessageHandler
        {
            public string Mode;
            public HttpStatusCode? Failure;
            public TaskCompletionSource<bool> Gate;
            public int Requests;
            public bool SawAuthorization;
            public bool ApiLimited;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                Requests++; SawAuthorization |= request.Headers.Authorization != null;
                cancellation.ThrowIfCancellationRequested();
                if (Gate != null) await Gate.Task;
                if (ApiLimited && request.RequestUri.AbsoluteUri == UpdateClient.LatestUrl) return new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = request };
                if (ApiLimited && request.RequestUri.AbsoluteUri == UpdateRelease.RepositoryUrl + "/releases/latest")
                    return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(HttpMethod.Head, UpdateRelease.RepositoryUrl + "/releases/tag/v1.2.3"), Content = new ByteArrayContent(new byte[0]) };
                if (Failure.HasValue) return new HttpResponseMessage(Failure.Value) { RequestMessage = request };
                byte[] body = request.RequestUri.AbsoluteUri == UpdateClient.LatestUrl ? Encoding.UTF8.GetBytes(Metadata()) : request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt") ? Encoding.UTF8.GetBytes(PackageHash + "  TokenDock-1.2.3.zip\n") : Package;
                if (request.RequestUri.AbsolutePath.EndsWith(".zip"))
                {
                    if (Mode == "corrupt") body = Enumerable.Repeat((byte)0, Package.Length).ToArray();
                    if (Mode == "truncated") body = Package.Take(5).ToArray();
                    if (Mode == "oversized") body = Package.Concat(Package).ToArray();
                }
                HttpContent content = Mode == "cancelled" && request.RequestUri.AbsolutePath.EndsWith(".zip") ? (HttpContent)new StreamContent(new InterruptedStream(body)) : new ByteArrayContent(body);
                if (Mode == "truncated" || Mode == "oversized") content.Headers.ContentLength = Package.Length;
                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content };
            }
        }

        sealed class InterruptedStream : MemoryStream
        {
            bool started;
            public InterruptedStream(byte[] bytes) : base(bytes) { }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
            {
                if (started) throw new OperationCanceledException();
                started = true;
                return base.ReadAsync(buffer, offset, Math.Min(5, count), cancellation);
            }
        }

        sealed class DemoProvider : IUsageProvider
        {
            public string Id { get; private set; }
            public string Name { get { return Id == "codex" ? "Codex" : "DeepSeek"; } }
            public DemoProvider(string id) { Id = id; }
            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(Id == "codex" ? new UsageSnapshot { Name = Name, Status = "已连接", Detail = "demo-codex", Updated = DateTime.Now, LifetimeTokens = 100000, Quotas = new List<QuotaWindow> { new QuotaWindow { Label = "7天", Used = 20, Resets = SystemClock.UnixNow() + 86400 } } } : UsageSnapshot.Failed(new UsageSnapshot { Name = Name, Updated = DateTime.Now.AddMinutes(-5) }, "演示读取失败"));
            }
        }
        sealed class DemoDevices : IBluetoothSource
        {
            public Task<BluetoothSnapshot> ReadAsync(Settings settings) { return Task.FromResult(new BluetoothSnapshot { Status = "成功", Updated = DateTime.Now, Devices = new List<BluetoothDevice> { new BluetoothDevice { id = "mouse", name = "无线鼠标", kind = "mouse", connected = true, battery = 65, source = "Windows 电量" } } }); }
        }
    }
}
