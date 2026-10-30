using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    class DisplayPage : SettingsPage
    {
        readonly UsageMonitor monitor;
        readonly CheckBox showBar, codex, claude, deepseek, devices, hoverPreview, lockPosition;
        readonly ComboBox position, codexMetric, claudeMetric, style;
        readonly CheckedListBox deviceList;
        readonly Dictionary<string, bool> selection = new Dictionary<string, bool>();
        bool filling;
        string deviceSignature = "";
        List<string> itemOrder;
        readonly string[] modes =
        {
            "auto",
            "left",
            "right",
            "custom"
        };
        readonly string[] metrics =
        {
            "auto",
            "short",
            "weekly",
            "minimum",
            "tokens"
        };
        class DeviceChoice
        {
            public string Id, Text;
            public override string ToString()
            {
                return Text;
            }
        }

        public DisplayPage(UsageMonitor source)
        {
            monitor = source;
            var settings = source.Config;
            itemOrder = TaskbarOrder.Normalize(settings.ItemOrder);
            Section("任务栏", "决定常驻内容的外观与位置。");
            showBar = Check("显示任务栏用量", settings.ShowBar);
            position = Choice(new[] { "自动寻找空位", "靠左", "靠右", "自定义（拖动）" }, Array.IndexOf(modes, settings.TaskbarPosition));
            position.Name = "TaskbarPosition";
            lockPosition = new ModernToggle
            {
                Text = "锁定位置",
                Checked = settings.TaskbarPositionLocked,
                AutoSize = true,
                AccessibleDescription = "仅防止鼠标误拖动，仍可在设置中切换显示位置"
            };
            Row("显示位置", position);
            Full(lockPosition);
            style = Choice(new[] { "名称 + 数值", "紧凑：图标 + 数值" }, settings.TaskbarStyle == "compact" ? 1 : 0);
            style.Name = "TaskbarStyle";
            Row("常驻样式", style);
            var order = Theme.Button("调整显示顺序…", 160);
            order.Click += delegate
            {
                using (var dialog = new ItemOrderWindow(TaskbarOrder.Choices(monitor, itemOrder)))
                    if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                        itemOrder = dialog.Order;
            };
            Row("项目排序", order);
            hoverPreview = Check("悬停时显示额度与设备速览", settings.HoverPreviewEnabled);
            Section("AI 服务", "只展示已接入且有有效数据的项目。");
            codex = new ModernToggle
            {
                Text = "Codex",
                Checked = settings.ShowCodex,
                AutoSize = true,
                AccessibleName = "任务栏显示 Codex"
            };
            codexMetric = MetricChoice(settings.CodexMetric, "账户累计 Token");
            Row(codex, codexMetric);
            claude = new ModernToggle
            {
                Text = "Claude Code",
                Checked = settings.ShowClaude,
                AutoSize = true,
                AccessibleName = "任务栏显示 Claude Code"
            };
            claudeMetric = MetricChoice(settings.ClaudeMetric, "最近会话 Token");
            Row(claude, claudeMetric);
            deepseek = new ModernToggle
            {
                Text = "DeepSeek 余额",
                Checked = settings.ShowDeepSeek,
                AutoSize = true
            };
            Full(deepseek);
            Section("设备电量", "选择需要常驻任务栏的设备。");
            devices = new ModernToggle
            {
                Text = "设备电量",
                Checked = settings.ShowDevices,
                AutoSize = true
            };
            Full(devices);
            Hint("无有效数据时隐藏，真实的零额度、零余额和零电量仍显示。");
            deviceList = new CheckedListBox
            {
                Name = "DeviceSelection",
                Height = 116,
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
            deviceList.ItemCheck += delegate (object sender, ItemCheckEventArgs e)
            {
                if (!filling)
                    selection[((DeviceChoice)deviceList.Items[e.Index]).Id] = e.NewValue == CheckState.Checked;
            };
            Full(deviceList);
            Hint("勾选需要常驻任务栏的设备。新增设备默认显示；断开或电量未知时自动隐藏。");
            monitor.Changed += OnChanged;
            LoadDevices();
        }

        ComboBox Choice(string[] labels, int index)
        {
            var result = new ModernComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
            result.Items.AddRange(labels);
            result.SelectedIndex = Math.Max(0, index);
            return result;
        }

        ComboBox MetricChoice(string value, string tokenLabel)
        {
            return Choice(new[] { "自动选择账户额度", "短期额度", "每周额度", "最紧张的额度", tokenLabel }, Array.IndexOf(metrics, value));
        }

        void OnChanged()
        {
            LoadDevices();
        }

        void LoadDevices()
        {
            string signature = string.Join("|", monitor.Devices.Select(device => device.Identity + ":" + device.connected + ":" + device.battery + ":" + device.charging));
            if (signature == deviceSignature && deviceList.Items.Count > 0)
                return;
            deviceSignature = signature;
            filling = true;
            deviceList.BeginUpdate();
            try
            {
                deviceList.Items.Clear();
                foreach (var device in monitor.Devices.OrderBy(d => d.name))
                {
                    if (!selection.ContainsKey(device.Identity))
                        selection[device.Identity] = !monitor.Config.HiddenDeviceIds.Contains(device.Identity);
                    string status = device.connected == false ? "未连接" : device.charging == true ? "充电中" : device.battery.HasValue ? device.battery.Value.ToString("0") + "%" : "电量未知";
                    deviceList.Items.Add(new DeviceChoice { Id = device.Identity, Text = (device.name ?? "蓝牙设备").Trim() + "    " + status }, selection[device.Identity]);
                }
            }
            finally
            {
                deviceList.EndUpdate();
                filling = false;
            }
        }

        public override void Apply(Settings settings)
        {
            settings.ShowBar = showBar.Checked;
            settings.ShowCodex = codex.Checked;
            settings.ShowClaude = claude.Checked;
            settings.ShowDeepSeek = deepseek.Checked;
            settings.ShowDevices = devices.Checked;
            settings.TaskbarStyle = style.SelectedIndex == 1 ? "compact" : "full";
            settings.HoverPreviewEnabled = hoverPreview.Checked;
            settings.TaskbarPositionLocked = lockPosition.Checked;
            settings.ItemOrder = new List<string>(itemOrder);
            settings.TaskbarPosition = modes[position.SelectedIndex];
            settings.CodexMetric = metrics[codexMetric.SelectedIndex];
            settings.ClaudeMetric = metrics[claudeMetric.SelectedIndex];
            var hidden = new HashSet<string>(settings.HiddenDeviceIds);
            foreach (var entry in selection)
            {
                if (entry.Value)
                    hidden.Remove(entry.Key);
                else
                    hidden.Add(entry.Key);
            }

            settings.HiddenDeviceIds = hidden.ToList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                monitor.Changed -= OnChanged;
            base.Dispose(disposing);
        }
    }
}
