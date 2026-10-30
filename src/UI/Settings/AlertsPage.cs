namespace TokenDock
{
    class AlertsPage : SettingsPage
    {
        readonly System.Windows.Forms.CheckBox enabled, quota, battery, balance, recovery;
        readonly System.Windows.Forms.NumericUpDown quotaLevel, batteryLevel, cny, usd;
        public AlertsPage(Settings settings)
        {
            Section("通知", "提醒与任务栏显示选择相互独立。");
            enabled = Check("开启额度与电量提醒", settings.AlertsEnabled);
            Section("订阅额度", "额度不足或恢复可用时通知。");
            quota = Check("订阅额度不足时提醒", settings.QuotaAlertsEnabled);
            recovery = Check("额度周期更新或从低位恢复时提醒", settings.QuotaRecoveryAlertsEnabled);
            quotaLevel = Number(settings.QuotaAlertPercent, 0, 100, 0);
            Row("剩余额度 ≤ %", quotaLevel);
            Section("设备电量", "低电量状态只提醒一次，恢复后可再次触发。");
            battery = Check("设备低电量时提醒", settings.BatteryAlertsEnabled);
            batteryLevel = Number(settings.BatteryAlertPercent, 0, 100, 0);
            Row("设备电量 ≤ %", batteryLevel);
            Section("API 余额", "DeepSeek 的不同币种分别设置阈值。");
            balance = Check("DeepSeek 余额不足时提醒", settings.BalanceAlertsEnabled);
            cny = Number(settings.CnyBalanceAlert, 0, 100000, 2);
            Row("人民币余额 ≤", cny);
            usd = Number(settings.UsdBalanceAlert, 0, 100000, 2);
            Row("美元余额 ≤", usd);
            Hint("同一低位状态只提醒一次，恢复后再次降低才会提醒。Windows 的通知设置可能影响提醒展示。");
            Hint("提醒按已接入的数据触发，与任务栏常驻显示选择独立。");
            Hint("恢复提醒需要先后两次成功读取；首次启动不会凭当前额度补发提醒。");
        }

        public override void Apply(Settings settings)
        {
            settings.AlertsEnabled = enabled.Checked;
            settings.QuotaAlertsEnabled = quota.Checked;
            settings.QuotaRecoveryAlertsEnabled = recovery.Checked;
            settings.BatteryAlertsEnabled = battery.Checked;
            settings.BalanceAlertsEnabled = balance.Checked;
            settings.QuotaAlertPercent = (int)quotaLevel.Value;
            settings.BatteryAlertPercent = (int)batteryLevel.Value;
            settings.CnyBalanceAlert = cny.Value;
            settings.UsdBalanceAlert = usd.Value;
        }
    }
}
