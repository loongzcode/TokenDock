using System;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    sealed class DeviceBatteryService : IBluetoothSource
    {
        readonly IBluetoothSource bluetooth, receiver;
        public DeviceBatteryService() : this(new BluetoothService(), new Flx1ReceiverSource())
        {
        }

        public DeviceBatteryService(IBluetoothSource first, IBluetoothSource second)
        {
            bluetooth = first;
            receiver = second;
        }

        public async Task<BluetoothSnapshot> ReadAsync(Settings settings)
        {
            var first = SafeRead(bluetooth, settings);
            var second = SafeRead(receiver, settings);
            await Task.WhenAll(first, second);
            return Merge(first.Result, second.Result);
        }

        static async Task<BluetoothSnapshot> SafeRead(IBluetoothSource source, Settings settings)
        {
            try
            {
                return await source.ReadAsync(settings);
            }
            catch
            {
                return null;
            }
        }

        public static BluetoothSnapshot Merge(BluetoothSnapshot bluetooth, BluetoothSnapshot receiver)
        {
            if (bluetooth == null && receiver == null)
                throw new InvalidOperationException("设备扫描失败");
            var snapshots = new[]
            {
                bluetooth,
                receiver
            }.Where(item => item != null).ToArray();
            return new BluetoothSnapshot
            {
                Updated = DateTime.Now,
                Status = (bluetooth == null || receiver == null ? "设备扫描失败：部分来源不可用；" : "") + "设备状态已更新",
                Devices = snapshots.SelectMany(item => item.Devices).GroupBy(device => device.Identity).Select(group => group.OrderByDescending(device => device.connected == true && (device.battery.HasValue || device.charging == true)).ThenByDescending(device => device.connected == true).ThenByDescending(device => device.transport == "2.4G").First()).ToList()
            };
        }
    }
}
