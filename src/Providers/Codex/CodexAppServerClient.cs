using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    static class CodexAppServerClient
    {
        public static string Locate(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    string file = Path.Combine(directory.Trim('"'), "codex.exe");
                    if (File.Exists(file))
                        return file;
                }
                catch
                {
                }
            }

            string bin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (Directory.Exists(bin))
                foreach (var directory in Directory.GetDirectories(bin).OrderByDescending(Directory.GetLastWriteTimeUtc))
                {
                    string file = Path.Combine(directory, "codex.exe");
                    if (File.Exists(file))
                        return file;
                }

            return null;
        }

        public static async Task<object> Read(string executable)
        {
            var info = ProcessRunner.Info(executable, "app-server");
            info.RedirectStandardInput = true;
            using (var process = new Process
            {
                StartInfo = info
            }

            )
            {
                process.Start();
                var errors = process.StandardError.ReadToEndAsync();
                try
                {
                    Send(process, new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "token_dock", title = "TokenDock", version = AppIdentity.Version } } });
                    await Response(process, 1);
                    Send(process, new { method = "initialized", @params = new { } });
                    Send(process, new { id = 2, method = "account/rateLimits/read" });
                    object result = await Response(process, 2);
                    try
                    {
                        Send(process, new { id = 3, method = "account/usage/read" });
                        var usage = await Response(process, 3);
                        var map = result as IDictionary<string, object>;
                        if (map != null)
                            map["tokenActivity"] = usage;
                    }
                    catch
                    {
                    }

                    return result;
                }
                finally
                {
                    try
                    {
                        process.StandardInput.Close();
                        if (!process.WaitForExit(500))
                            process.Kill();
                        errors.Wait(1000);
                    }
                    catch
                    {
                    }
                }
            }
        }

        static void Send(Process process, object request)
        {
            process.StandardInput.WriteLine(Json.Write(request));
            process.StandardInput.Flush();
        }

        static async Task<object> Response(Process process, int id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                var line = process.StandardOutput.ReadLineAsync();
                int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                if (await Task.WhenAny(line, Task.Delay(remaining)) != line)
                    throw new TimeoutException("Codex 读取超时。");
                string value = await line;
                if (value == null)
                    throw new InvalidDataException("Codex 连接已关闭。");
                object message;
                try
                {
                    message = Json.Read(value);
                }
                catch
                {
                    continue;
                }

                if (Json.Text(Json.Get(message, "id")) != id.ToString())
                    continue;
                if (Json.Get(message, "error") != null)
                    throw new InvalidOperationException("Codex 未返回数据，请检查登录状态和 CLI 版本。");
                return Json.Get(message, "result");
            }

            throw new TimeoutException("Codex 读取超时。");
        }
    }
}
