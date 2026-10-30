using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TokenDock
{
    interface IBluetoothSource
    {
        Task<BluetoothSnapshot> ReadAsync(Settings settings);
    }

    class BluetoothService : IBluetoothSource
    {
        public async Task<BluetoothSnapshot> ReadAsync(Settings settings)
        {
            string script = Path.Combine(AppPaths.BinaryDirectory, "bluetooth.ps1");
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            using (var process = new Process
            {
                StartInfo = ProcessRunner.Info(shell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\"" + (settings.GattFallback ? " -ReadGatt" : ""))
            }

            )
            {
                process.Start();
                object result = Json.Read(await ProcessRunner.ReadAll(process, 45000));
                var devices = new List<BluetoothDevice>();
                foreach (var row in Json.Items(Json.Get(result, "devices")))
                {
                    double? battery = Json.Number(Json.Get(row, "battery"));
                    if (battery < 0 || battery > 100)
                        battery = null;
                    object connected = Json.Get(row, "connected");
                    devices.Add(new BluetoothDevice { name = Json.Text(Json.Get(row, "name")), id = Json.Text(Json.Get(row, "id")), connected = connected is bool ? (bool? )connected : null, battery = battery, source = Json.Text(Json.Get(row, "source")) });
                }

                return new BluetoothSnapshot
                {
                    Devices = devices,
                    Status = Json.Text(Json.Get(result, "status")),
                    Updated = DateTime.Now
                };
            }
        }
    }
}
