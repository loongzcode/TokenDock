using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class UpdateWindow : Form
    {
        readonly UpdateClient client;
        readonly Label status, installed;
        readonly ModernButton check, download, releaseNotes;
        readonly ProgressBar progress;
        CancellationTokenSource transfer;
        UpdateRelease latest;
        string ready;
        bool checking;

        public UpdateWindow(UpdateClient source)
        {
            client = source;
            Text = "TokenDock · 检查更新";
            BackColor = Theme.SettingsBg;
            ForeColor = Theme.Text;
            Font = Theme.Font(10, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            AutoScaleMode = AutoScaleMode.None;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(460, 242);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            installed = new Label { Text = "当前版本 " + AppIdentity.Version, Dock = DockStyle.Fill };
            status = new Label { Text = "准备检查正式版本…", Name = "UpdateStatus", Dock = DockStyle.Fill };
            progress = new ProgressBar { Dock = DockStyle.Fill, Visible = false, Height = 8, Margin = new Padding(0, 5, 0, 5) };
            var hint = new Label { Text = "从 GitHub 获取更新；校验后由你解压运行。", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = Theme.Font(9, FontStyle.Regular) };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            download = new ModernButton { Text = "下载并校验", Name = "DownloadUpdate", Enabled = false, Size = new Size(116, 34), Margin = new Padding(6, 0, 0, 0) };
            check = new ModernButton { Text = "重新检查", Name = "CheckUpdate", Size = new Size(92, 34), Margin = new Padding(6, 0, 0, 0) };
            releaseNotes = new ModernButton { Text = "发布说明", Name = "UpdateReleaseNotes", Enabled = false, Size = new Size(92, 34), Margin = new Padding(6, 0, 0, 0) };
            actions.Controls.AddRange(new Control[] { download, check, releaseNotes });
            layout.Controls.Add(installed, 0, 0); layout.Controls.Add(status, 0, 1); layout.Controls.Add(progress, 0, 2); layout.Controls.Add(hint, 0, 3); layout.Controls.Add(actions, 0, 4);
            Controls.Add(layout);
            check.Click += async delegate { await CheckAsync(); };
            download.Click += async delegate { await DownloadAsync(); };
            releaseNotes.Click += delegate { if (latest != null) Open(new ProcessStartInfo(latest.PageUrl) { UseShellExecute = true }); };
            Shown += async delegate { await CheckAsync(); };
            FormClosing += delegate { if (transfer != null) transfer.Cancel(); };
        }

        public async System.Threading.Tasks.Task CheckAsync()
        {
            if (checking || transfer != null) return;
            checking = true;
            check.Busy = true;
            download.Enabled = releaseNotes.Enabled = false;
            status.Text = "正在检查 GitHub 正式版本…";
            ready = null;
            download.Text = "下载并校验";
            try
            {
                latest = await client.CheckAsync();
                if (IsDisposed) return;
                bool newer = latest.IsNewerThan(new Version(AppIdentity.Version));
                status.Text = newer ? "发现新版本 " + latest.Version.ToString(3) : latest.Version < new Version(AppIdentity.Version) ? "当前使用较新的本机版本。GitHub 正式版为 " + latest.Version.ToString(3) + "。" : "当前已是最新版本。";
                download.Enabled = newer;
                releaseNotes.Enabled = true;
            }
            catch (Exception error) { if (!IsDisposed) status.Text = error is InvalidDataException || error is InvalidOperationException ? error.Message : "检查更新失败，请稍后重试。"; }
            finally { checking = false; if (!IsDisposed) check.Busy = false; }
        }

        async System.Threading.Tasks.Task DownloadAsync()
        {
            if (ready != null)
            {
                Open(new ProcessStartInfo("explorer.exe", "\"" + Path.GetDirectoryName(ready) + "\"") { UseShellExecute = true });
                return;
            }
            if (latest == null || transfer != null) return;
            transfer = new CancellationTokenSource();
            check.Enabled = false;
            download.Busy = true;
            progress.Value = 0; progress.Visible = true;
            status.Text = "正在下载并核对 SHA256…关闭窗口可取消。";
            try
            {
                string path = await client.DownloadAsync(latest, Path.Combine(AppPaths.DataDirectory, "updates"), new Progress<int>(value => { if (!IsDisposed) progress.Value = value; }), transfer.Token);
                if (IsDisposed) return;
                ready = path;
                status.Text = "SHA256 校验通过。退出旧版后，完整解压并运行新版。";
                download.Text = "打开下载目录";
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = "下载已取消。"; }
            catch (Exception error) { if (!IsDisposed) status.Text = error is InvalidDataException || error is InvalidOperationException ? error.Message : "下载失败，请检查网络后重试。"; }
            finally
            {
                transfer.Dispose(); transfer = null;
                if (!IsDisposed) { download.Busy = false; check.Enabled = true; progress.Visible = ready != null; }
            }
        }

        void Open(ProcessStartInfo target)
        {
            try { Process.Start(target); }
            catch { status.Text = "无法打开，请检查默认浏览器或资源管理器后重试。"; }
        }
    }
}
