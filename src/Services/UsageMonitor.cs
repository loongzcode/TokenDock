using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    class UsageMonitor
    {
        public Settings Config;
        public UsageSnapshot Codex { get; private set; }
        public UsageSnapshot Claude { get; private set; }
        public UsageSnapshot DeepSeek { get; private set; }
        public List<BluetoothDevice> Devices { get; private set; }
        public string BluetoothStatus { get; private set; }
        public DateTime? BluetoothUpdated { get; private set; }

        public event Action Changed;
        readonly Dictionary<string, IUsageProvider> providers;
        readonly IBluetoothSource bluetooth;
        Task activeRefresh;
        Task activeBluetoothRefresh;
        DateTime lastApi = DateTime.MinValue;
        public UsageMonitor(Settings config) : this(config, new IUsageProvider[] { new CodexProvider(), new ClaudeProvider(), new DeepSeekProvider() }, new DeviceBatteryService())
        {
        }

        public UsageMonitor(Settings config, IEnumerable<IUsageProvider> sources, IBluetoothSource bluetoothSource)
        {
            Config = config;
            providers = sources.ToDictionary(source => source.Id);
            bluetooth = bluetoothSource;
            Codex = new UsageSnapshot
            {
                Name = "Codex",
                Status = "读取中",
                Detail = "正在连接本机 Codex…"
            };
            Claude = new UsageSnapshot
            {
                Name = "Claude Code",
                Status = "未连接",
                Detail = "在连接设置中配置 Claude Code 状态栏桥接。"
            };
            DeepSeek = new UsageSnapshot
            {
                Name = "DeepSeek",
                Status = "未配置",
                Detail = "在连接设置中填写 DeepSeek API Key。"
            };
            Devices = new List<BluetoothDevice>();
            BluetoothStatus = "正在扫描已配对设备…";
        }

        public void Emit()
        {
            if (Changed != null)
                Changed();
        }

        public Task Refresh(bool force)
        {
            if (activeRefresh != null && !activeRefresh.IsCompleted)
                return force ? RefreshAfter(activeRefresh) : activeRefresh;
            activeRefresh = RefreshCore(force);
            return activeRefresh;
        }

        public Task RefreshBluetooth()
        {
            if (activeBluetoothRefresh != null && !activeBluetoothRefresh.IsCompleted)
                return RefreshBluetoothAfter(activeBluetoothRefresh);
            activeBluetoothRefresh = ReadBluetooth();
            return activeBluetoothRefresh;
        }

        async Task RefreshBluetoothAfter(Task previous)
        {
            await previous;
            // Join a newer refresh if another caller already started it.
            if (activeBluetoothRefresh != previous && activeBluetoothRefresh != null && !activeBluetoothRefresh.IsCompleted)
                await activeBluetoothRefresh;
            else
                await RefreshBluetooth();
        }

        async Task RefreshAfter(Task previous)
        {
            await previous;
            await Refresh(true);
        }

        async Task RefreshCore(bool force)
        {
            try
            {
                await ReadProvider("claude");
                if (!force && (DateTime.UtcNow - lastApi).TotalSeconds < Math.Max(60, Config.RefreshSeconds))
                    return;
                lastApi = DateTime.UtcNow;
                await Task.WhenAll(ReadProvider("codex"), ReadProvider("deepseek"), RefreshBluetooth());
            }
            finally
            {
                Emit();
            }
        }

        UsageSnapshot Snapshot(string id)
        {
            return id == "codex" ? Codex : id == "claude" ? Claude : DeepSeek;
        }

        void Store(string id, UsageSnapshot value)
        {
            if (id == "codex")
                Codex = value;
            else if (id == "claude")
                Claude = value;
            else
                DeepSeek = value;
        }

        async Task ReadProvider(string id)
        {
            IUsageProvider provider;
            if (!providers.TryGetValue(id, out provider))
                return;
            try
            {
                Store(id, await provider.ReadAsync(Config));
            }
            catch (Exception error)
            {
                Store(id, UsageSnapshot.Failed(Snapshot(id), error.Message));
            }

            Emit();
        }

        async Task ReadBluetooth()
        {
            try
            {
                var snapshot = await bluetooth.ReadAsync(Config);
                Devices = snapshot.Devices;
                BluetoothStatus = snapshot.Status;
                BluetoothUpdated = snapshot.Updated;
            }
            catch
            {
                BluetoothStatus = "设备扫描失败，上次结果仅供参考。";
                BluetoothUpdated = null;
            }

            Emit();
        }
    }
}
