using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TokenDock
{
    sealed class ReceiverBinding
    {
        public string Fingerprint { get; set; }
        public string BluetoothId { get; set; }
    }

    sealed class Flx1ReceiverSource : IBluetoothSource
    {
        public Task<BluetoothSnapshot> ReadAsync(Settings settings)
        {
            var bindings = settings.Flx1Receivers.ToArray();
            return Task.Run(() => Read(bindings));
        }

        static BluetoothSnapshot Read(ReceiverBinding[] bindings)
        {
            var snapshot = new BluetoothSnapshot
            {
                Status = "2.4G 接收器",
                Updated = DateTime.Now
            };
            if (bindings.Length == 0)
                return snapshot;
            foreach (var endpoint in HidControl.Enumerate())
            {
                var binding = bindings.FirstOrDefault(item => item.Fingerprint == endpoint.Fingerprint);
                if (binding == null)
                    continue;
                var device = new BluetoothDevice
                {
                    id = string.IsNullOrEmpty(binding.BluetoothId) ? "receiver:flx1:" + binding.Fingerprint : binding.BluetoothId,
                    name = "FLX1",
                    kind = "mouse",
                    transport = "2.4G",
                    source = "2.4G 接收器 · 暂不可用"
                };
                snapshot.Devices.Add(device);
                try
                {
                    if (!Flx1Protocol.Matches(HidControl.Query(endpoint, 3)))
                        continue;
                    byte[] status = HidControl.Query(endpoint, 0x0E);
                    if (!Flx1Protocol.Online(status))
                        continue;
                    var power = Flx1Protocol.Read(status, HidControl.Query(endpoint, 0x30));
                    if (power == null)
                        continue;
                    if (!Flx1Protocol.Online(HidControl.Query(endpoint, 0x0E)))
                        continue;
                    device.connected = true;
                    device.charging = power.Charging;
                    device.battery = power.Battery;
                    device.source = power.Charging ? "2.4G 接收器 · 充电中，百分比暂不显示" : "2.4G 接收器 · 厂商上报";
                }
                catch
                { /* Fail closed rather than reusing a cached receiver battery. */
                }
            }

            snapshot.Updated = DateTime.Now;
            return snapshot;
        }

        public static bool ValidateBinding(string fingerprint)
        {
            var endpoint = HidControl.Enumerate().FirstOrDefault(item => item.Fingerprint == fingerprint);
            return endpoint != null && Flx1Protocol.Matches(HidControl.Query(endpoint, 3));
        }
    }
}
