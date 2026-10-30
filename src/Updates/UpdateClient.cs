using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TokenDock
{
    sealed class UpdateHttpException : InvalidOperationException
    {
        public int StatusCode { get; private set; }
        public UpdateHttpException(int code, string message) : base(message) { StatusCode = code; }
    }
    sealed class UpdateClient : IDisposable
    {
        public const string LatestUrl = "https://api.github.com/repos/loongzcode/TokenDock/releases/latest";
        readonly HttpClient client;
        Task<UpdateRelease> checking;

        public UpdateClient() : this(new HttpClientHandler()) { }
        internal UpdateClient(HttpMessageHandler handler)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TokenDock/" + AppIdentity.Version);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public Task<UpdateRelease> CheckAsync()
        {
            if (checking == null || checking.IsCompleted) checking = CheckCore();
            return checking;
        }

        async Task<UpdateRelease> CheckCore()
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
            {
                try
                {
                    string metadata = null;
                    bool fallback = false;
                    try { metadata = await ReadText(LatestUrl, 2 * 1024 * 1024, timeout.Token); }
                    catch (UpdateHttpException error)
                    {
                        if (error.StatusCode != 403 && error.StatusCode != 429) throw;
                        fallback = true;
                    }
                    return fallback ? await CheckPublicPage(timeout.Token) : UpdateRelease.Parse(Json.Read(metadata));
                }
                catch (HttpRequestException) { throw new InvalidOperationException("检查更新失败，请检查网络后重试。"); }
                catch (OperationCanceledException) { throw new InvalidOperationException("检查更新超时，请稍后重试。"); }
            }
        }

        async Task<UpdateRelease> CheckPublicPage(CancellationToken cancellation)
        {
            UpdateRelease release;
            using (var request = new HttpRequestMessage(HttpMethod.Head, UpdateRelease.RepositoryUrl + "/releases/latest"))
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                EnsureResponse(response);
                release = UpdateRelease.FromPublicPage(response.RequestMessage.RequestUri, 0);
            }
            using (var request = new HttpRequestMessage(HttpMethod.Head, release.PackageUrl))
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                EnsureResponse(response);
                release = UpdateRelease.FromPublicPage(new Uri(release.PageUrl), response.Content.Headers.ContentLength ?? -1);
                if (release.Size == 0) throw new InvalidDataException("无法确认更新包大小，请稍后重试。");
            }
            release.ReadChecksum(await ReadText(release.ChecksumsUrl, 65536, cancellation));
            return release;
        }

        async Task<string> ReadText(string url, int maximum, CancellationToken cancellation)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                EnsureResponse(response);
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
                    {
                        if (output.Length + count > maximum) throw new InvalidDataException("更新响应超过允许的大小。");
                        output.Write(buffer, 0, count);
                    }
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }

        static void EnsureResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                throw new UpdateHttpException(code, code == 403 || code == 429 ? "GitHub 暂时限制查询频率，请稍后重试。" : code == 404 ? "尚未找到正式版本或更新附件。" : "暂时无法访问 GitHub（HTTP " + code + "），请稍后重试。");
            }
            var uri = response.RequestMessage == null ? null : response.RequestMessage.RequestUri;
            if (uri == null || uri.Scheme != "https" || !(uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("更新下载跳转到了未支持的来源。");
        }

        public async Task<string> DownloadAsync(UpdateRelease release, string directory, IProgress<int> progress, CancellationToken cancellation)
        {
            if (release == null) throw new ArgumentNullException("release");
            string expectedHash = release.ReadChecksum(await ReadText(release.ChecksumsUrl, 65536, cancellation));
            string destination = Path.Combine(Path.GetFullPath(directory), "v" + release.Version.ToString(3), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            string partial = Path.Combine(destination, release.FileName + ".partial");
            try
            {
                using (var response = await client.GetAsync(release.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
                {
                    EnsureResponse(response);
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != release.Size)
                        throw new InvalidDataException("下载文件大小与版本信息不符。");
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    using (var sha = SHA256.Create())
                    {
                        var buffer = new byte[81920];
                        long total = 0;
                        int count;
                        while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
                        {
                            total += count;
                            if (total > release.Size || total > UpdateRelease.MaximumPackageSize) throw new InvalidDataException("下载文件超过预期大小。");
                            await output.WriteAsync(buffer, 0, count, cancellation);
                            sha.TransformBlock(buffer, 0, count, buffer, 0);
                            if (progress != null) progress.Report((int)Math.Min(99, total * 100 / release.Size));
                        }
                        sha.TransformFinalBlock(new byte[0], 0, 0);
                        string actual = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                        if (total != release.Size || actual != expectedHash) throw new InvalidDataException("SHA256 校验失败，下载文件已丢弃，请重试。");
                        cancellation.ThrowIfCancellationRequested();
                    }
                }
                string ready = Path.Combine(destination, release.FileName);
                File.Move(partial, ready);
                if (progress != null) progress.Report(100);
                return ready;
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }

        public void Dispose() { client.Dispose(); }
    }
}
