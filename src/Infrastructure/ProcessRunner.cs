using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace TokenDock
{
    static class ProcessRunner
    {
        public static async Task<string> ReadAll(Process p, int timeoutMs)
        {
            Task<string> output = p.StandardOutput.ReadToEndAsync();
            Task<string> errors = p.StandardError.ReadToEndAsync();
            if (await Task.WhenAny(output, Task.Delay(timeoutMs)) != output)
            {
                try
                {
                    p.Kill();
                }
                catch
                {
                }

                throw new TimeoutException("读取超时，请稍后重试。");
            }

            string s = await output;
            if (!p.WaitForExit(1000))
            {
                try
                {
                    p.Kill();
                }
                catch
                {
                }
            }

            await errors;
            if (p.HasExited && p.ExitCode != 0)
                throw new InvalidOperationException("辅助进程未能完成读取。");
            return s;
        }

        public static ProcessStartInfo Info(string file, string args)
        {
            return new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
    }
}
