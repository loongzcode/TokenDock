using System;
using System.Collections.Generic;

namespace TokenDock
{
    class BluetoothSnapshot
    {
        public List<BluetoothDevice> Devices = new List<BluetoothDevice>();
        public string Status;
        public DateTime? Updated;
    }
}
