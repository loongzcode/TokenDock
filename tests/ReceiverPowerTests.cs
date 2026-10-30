using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TokenDock
{
    static class ReceiverPowerTests
    {
        public static string Run()
        {
            int count = 0;
            Action<bool, string> check = delegate (bool valid, string message)
            {
                if (!valid)
                    throw new Exception("FAIL: " + message);
                count++;
            };
            var status = Status();
            var battery = Battery(38, 0);
            check(Flx1Protocol.Online(status), "validated FLX1 online status is accepted");
            var power = Flx1Protocol.Read(status, battery);
            check(power != null && power.Battery == 38 && !power.Charging, "actual battery frame returns 38 percent");
            var offline = (byte[])status.Clone();
            offline[5] = 0x2E;
            for (int i = 14; i < 26; i++)
                offline[i] = 0xFF;
            check(!Flx1Protocol.Online(offline) && Flx1Protocol.Read(offline, battery) == null, "power-off status rejects a receiver's cached 38 percent");
            var charged = Flx1Protocol.Read(status, Battery(100, 1));
            check(charged != null && charged.Charging && !charged.Battery.HasValue, "charging frame exposes state rather than a misleading 100 percent");
            check(Flx1Protocol.Read(status, Battery(0, 0)).Battery == 0, "real zero battery is retained");
            check(Flx1Protocol.Read(status, Battery(101, 0)) == null && Flx1Protocol.Read(status, Battery(100, 2)) == null, "invalid level and unknown charging state fail closed");
            var notification = (byte[])battery.Clone();
            notification[2] = 0xFA;
            check(Flx1Protocol.Read(status, notification) == null, "unsolicited notification is not a battery response");
            var wrongReport = (byte[])battery.Clone();
            wrongReport[0] = 1;
            check(Flx1Protocol.Read(status, wrongReport) == null && Flx1Protocol.Read(status, new byte[64]) == null, "wrong report ID and truncated response are rejected");
            var wrongFlags = (byte[])battery.Clone();
            wrongFlags[7] = 0;
            check(Flx1Protocol.Read(status, wrongFlags) == null, "wrong protocol envelope is rejected");
            var badStage = (byte[])status.Clone();
            badStage[13] = 7;
            check(!Flx1Protocol.Online(badStage), "invalid active profile cannot establish receiver liveness");
            var emptyStatus = (byte[])status.Clone();
            Array.Clear(emptyStatus, 14, 12);
            check(!Flx1Protocol.Online(emptyStatus), "empty sensor configuration is unavailable, not online");
            var identity = new byte[65];
            identity[1] = 0xAA;
            identity[2] = 3;
            Encoding.ASCII.GetBytes("YJX.co.mouse263213May  7 2025").CopyTo(identity, 9);
            check(Flx1Protocol.Matches(identity), "tested firmware family is recognized");
            identity[9] = (byte)'X';
            check(!Flx1Protocol.Matches(identity), "unverified firmware is not called FLX1");
            foreach (byte command in new byte[]
            {
                3,
                0x0E,
                0x30
            }

            )
            {
                var query = Flx1Protocol.Query(command);
                check(query.Length == 65 && query[0] == 0 && query[1] == 0x55 && query[2] == command, "documented read query uses the native report layout");
            }

            bool blocked = false;
            try
            {
                Flx1Protocol.Query(0x0F);
            }
            catch (ArgumentOutOfRangeException)
            {
                blocked = true;
            }

            check(blocked, "configuration-write commands are not part of the reader");
            var endpoint = new HidEndpoint
            {
                Vendor = 0xA8A5,
                Product = 0x2255,
                Manufacturer = "YJX-CHIP",
                UsagePage = 0xFF01,
                Usage = 0x10,
                InputLength = 65,
                OutputLength = 65
            };
            check(HidControl.Eligible(endpoint), "only the verified vendor control collection is eligible");
            endpoint.UsagePage = 1;
            endpoint.Usage = 2;
            check(!HidControl.Eligible(endpoint), "normal mouse input collection is never queried");
            var receiver = new BluetoothSnapshot
            {
                Updated = DateTime.Now,
                Devices = new List<BluetoothDevice>
                {
                    new BluetoothDevice
                    {
                        id = "paired",
                        name = "FLX1",
                        kind = "mouse",
                        transport = "2.4G",
                        connected = true,
                        battery = 38
                    }
                }
            };
            var bluetooth = new BluetoothSnapshot
            {
                Updated = DateTime.Now,
                Devices = new List<BluetoothDevice>
                {
                    new BluetoothDevice
                    {
                        id = "paired",
                        name = "FLX1",
                        connected = false
                    },
                    new BluetoothDevice
                    {
                        id = "keyboard",
                        name = "Keyboard",
                        connected = true,
                        battery = 100
                    }
                }
            };
            var merged = DeviceBatteryService.Merge(bluetooth, receiver);
            check(merged.Devices.Count == 2 && merged.Devices.Single(device => device.id == "paired").battery == 38, "explicit peer identity merges Bluetooth and receiver without duplicate devices");
            check(DeviceBatteryService.Merge(null, receiver).Devices.Single().battery == 38, "Bluetooth failure does not hide the independently read receiver");
            receiver.Devices[0].connected = null;
            receiver.Devices[0].battery = null;
            bluetooth.Devices[0].connected = true;
            bluetooth.Devices[0].battery = 20;
            check(DeviceBatteryService.Merge(bluetooth, receiver).Devices.Single(device => device.id == "paired").battery == 20, "active Bluetooth reading wins over an unavailable receiver");
            receiver.Devices[0].connected = true;
            receiver.Devices[0].charging = true;
            var item = TaskbarItemBuilder.Build(null, null, null, receiver.Devices, true, new Settings()).Single();
            check(item.Value == "充电中" && item.IconKind == "mouse", "taskbar uses a mouse icon and charging state without an invented percentage");
            check(new AlertEngine().Evaluate(new Settings(), new UsageSnapshot[0], receiver.Devices, true).Count == 0, "charging does not trigger a low-battery alert");
            check(new HistoryCapture().Capture(new UsageSnapshot[0], receiver.Devices, DateTime.Now, DateTime.Now).Count == 0, "charging is not recorded as a false full battery history point");
            check(Settings.FromJson("{}").Flx1Receivers.Count == 0, "other users' receivers are not automatically bound");
            return "PASS " + count + " receiver power checks";
        }

        static byte[] Envelope(byte command, byte type)
        {
            var data = new byte[65];
            data[1] = 0xAA;
            data[2] = command;
            data[3] = 0xA5;
            data[5] = type;
            data[6] = data[7] = data[8] = 1;
            return data;
        }

        static byte[] Status()
        {
            var data = Envelope(0x0E, 0x2F);
            data[12] = 6;
            data[13] = 3;
            int[] values =
            {
                800,
                1200,
                1600,
                2400,
                4800,
                10000
            };
            for (int i = 0; i < values.Length; i++)
            {
                data[14 + i * 2] = (byte)values[i];
                data[15 + i * 2] = (byte)(values[i] >> 8);
            }

            return data;
        }

        static byte[] Battery(byte value, byte charging)
        {
            var data = Envelope(0x30, 0x0A);
            data[9] = value;
            data[10] = charging;
            return data;
        }
    }
}
