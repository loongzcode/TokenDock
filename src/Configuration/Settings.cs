using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TokenDock
{
    class Settings
    {
        public int SchemaVersion { get; set; }
        public string CodexPath { get; set; }
        public string DeepSeekKey { get; set; }
        public bool ShowBar { get; set; }
        public bool GattFallback { get; set; }
        public int RefreshSeconds { get; set; }
        public string TaskbarPosition { get; set; }
        public int TaskbarCustomOffset { get; set; }
        public bool ShowCodex { get; set; }
        public bool ShowClaude { get; set; }
        public bool ShowDeepSeek { get; set; }
        public bool ShowDevices { get; set; }
        public string TaskbarStyle { get; set; }
        public bool HoverPreviewEnabled { get; set; }
        public List<string> ItemOrder { get; set; }
        public bool QuotaRecoveryAlertsEnabled { get; set; }
        public bool HistoryEnabled { get; set; }
        public bool TaskbarPositionLocked { get; set; }
        public List<ReceiverBinding> Flx1Receivers { get; set; }
        public string CodexMetric { get; set; }
        public string ClaudeMetric { get; set; }
        public List<string> HiddenDeviceIds { get; set; }
        public bool AlertsEnabled { get; set; }
        public bool QuotaAlertsEnabled { get; set; }
        public bool BatteryAlertsEnabled { get; set; }
        public bool BalanceAlertsEnabled { get; set; }
        public int QuotaAlertPercent { get; set; }
        public int BatteryAlertPercent { get; set; }
        public decimal CnyBalanceAlert { get; set; }
        public decimal UsdBalanceAlert { get; set; }

        public Settings()
        {
            SchemaVersion = 1;
            CodexPath = "";
            DeepSeekKey = "";
            ShowBar = true;
            GattFallback = true;
            RefreshSeconds = 120;
            TaskbarPosition = "auto";
            TaskbarCustomOffset = 12;
            ShowCodex = ShowClaude = ShowDeepSeek = ShowDevices = true;
            TaskbarStyle = "full";
            HoverPreviewEnabled = true;
            ItemOrder = new List<string>
            {
                "codex",
                "claude",
                "deepseek"
            };
            QuotaRecoveryAlertsEnabled = true;
            HistoryEnabled = true;
            Flx1Receivers = new List<ReceiverBinding>();
            CodexMetric = ClaudeMetric = "auto";
            HiddenDeviceIds = new List<string>();
            AlertsEnabled = QuotaAlertsEnabled = BatteryAlertsEnabled = BalanceAlertsEnabled = true;
            QuotaAlertPercent = 10;
            BatteryAlertPercent = 20;
            CnyBalanceAlert = 5;
            UsdBalanceAlert = 1;
        }

        public static Settings Load()
        {
            return File.Exists(AppPaths.SettingsFile) ? FromJson(File.ReadAllText(AppPaths.SettingsFile)) : new Settings();
        }

        public static Settings FromJson(string json)
        {
            var value = Json.Serializer().Deserialize<Settings>(json);
            if (value == null)
                throw new InvalidDataException("配置为空，请检查备份。");
            if (value.SchemaVersion > 1)
                throw new InvalidDataException("配置来自更新的 TokenDock 版本，当前程序不会覆盖它。");
            value.Normalize();
            return value;
        }

        public void Normalize()
        {
            SchemaVersion = 1;
            CodexPath = CodexPath ?? "";
            DeepSeekKey = DeepSeekKey ?? "";
            RefreshSeconds = Math.Max(60, Math.Min(3600, RefreshSeconds));
            QuotaAlertPercent = Math.Max(0, Math.Min(100, QuotaAlertPercent));
            BatteryAlertPercent = Math.Max(0, Math.Min(100, BatteryAlertPercent));
            CnyBalanceAlert = Math.Max(0, Math.Min(100000, CnyBalanceAlert));
            UsdBalanceAlert = Math.Max(0, Math.Min(100000, UsdBalanceAlert));
            HiddenDeviceIds = HiddenDeviceIds ?? new List<string>();
            Flx1Receivers = (Flx1Receivers ?? new List<ReceiverBinding>()).Where(binding => binding != null && System.Text.RegularExpressions.Regex.IsMatch(binding.Fingerprint ?? "", "^[A-F0-9]{24}$")).GroupBy(binding => binding.Fingerprint).Select(group => group.First()).Take(16).ToList();
            ItemOrder = TaskbarOrder.Normalize(ItemOrder);
            CodexMetric = CodexMetric ?? "auto";
            ClaudeMetric = ClaudeMetric ?? "auto";
            TaskbarPosition = TaskbarPosition ?? "auto";
            TaskbarStyle = TaskbarStyle == "compact" ? "compact" : "full";
        }

        public void Save()
        {
            Normalize();
            Json.AtomicWrite(AppPaths.SettingsFile, Json.Write(this));
        }

        public string RevealKey()
        {
            return ProtectedSecretStore.Reveal(DeepSeekKey);
        }

        public void SetKey(string key)
        {
            DeepSeekKey = ProtectedSecretStore.Protect(key);
        }
    }
}
