using System;
using System.Windows.Forms;

namespace TokenDock
{
    class ConnectionPage : SettingsPage
    {
        readonly TextBox codex, key;
        readonly CheckBox startup, gatt, updates;
        readonly NumericUpDown refresh;
        bool clearKey;
        public bool StartupEnabled
        {
            get
            {
                return startup.Checked;
            }
        }

        public ConnectionPage(Settings settings, Action<string> feedback)
        {
            Section("Codex", "使用本机已登录的 Codex 账户。");
            codex = Input(settings.CodexPath, false);
            var browse = Theme.Button("选择文件", 100);
            browse.Click += delegate
            {
                using (var dialog = new OpenFileDialog
                {
                    Filter = "Codex CLI|codex.exe|应用程序|*.exe"
                }

                )
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        codex.Text = dialog.FileName;
            };
            Row("CLI 路径", WithAction(codex, browse, 100));
            Hint("留空自动查找 codex.exe。");
            Section("Claude Code", "同步订阅额度和 Token 计数。");
            var copy = Theme.Button("复制配置", 104);
            copy.Click += delegate
            {
                Clipboard.SetText(ClaudeProvider.Configuration(Application.ExecutablePath));
                feedback("已复制。请将 statusLine 合并进 Claude Code 设置。");
            };
            Row("接入配置", copy);
            Hint("通过状态栏同步额度和 Token 计数，不读取聊天内容。");
            Section("DeepSeek", "连接 API 账户，查看可用余额。");
            key = Input("", true);
            var clear = Theme.Button("清除", 80);
            clear.Click += delegate
            {
                clearKey = true;
                key.Clear();
                feedback("点击保存后清除已保存的 DeepSeek Key。");
            };
            Row("API Key", WithAction(key, clear, 80));
            Hint(string.IsNullOrEmpty(settings.DeepSeekKey) ? "Key 仅保存到本机，并使用 Windows 加密。" : "已经保存 Key；输入框留空保留原值。");
            Section("刷新与启动", "蓝牙连接变化会自动触发设备刷新。");
            refresh = Number(settings.RefreshSeconds, 60, 3600, 0);
            refresh.Name = "RefreshSeconds";
            Row("刷新间隔（秒）", refresh);
            gatt = Check("读取标准蓝牙电量", settings.GattFallback);
            startup = Check("登录 Windows 后启动", StartupRegistration.IsEnabled());
            updates = Check("自动检查新版本（启动及每 6 小时）", settings.CheckUpdatesEnabled);
            updates.Name = "CheckUpdatesEnabled";
        }

        public override void Apply(Settings settings)
        {
            settings.CodexPath = codex.Text.Trim().Trim('"');
            if (clearKey)
                settings.SetKey("");
            if (!string.IsNullOrWhiteSpace(key.Text))
                settings.SetKey(key.Text);
            settings.RefreshSeconds = (int)refresh.Value;
            settings.GattFallback = gatt.Checked;
            settings.CheckUpdatesEnabled = updates.Checked;
        }
    }
}
