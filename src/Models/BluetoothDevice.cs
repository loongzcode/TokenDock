using System;

namespace TokenDock
{
    class BluetoothDevice
    {
        public string Identity
        {
            get
            {
                return !string.IsNullOrWhiteSpace(id) ? id : !string.IsNullOrWhiteSpace(name) ? name : "unknown";
            }
        }

        public string name { get; set; }
        public string id { get; set; }
        public bool? connected { get; set; }
        public double? battery { get; set; }
        public string source { get; set; }
        public string kind { get; set; }
        public string transport { get; set; }
        public bool? charging { get; set; }
    }
}
