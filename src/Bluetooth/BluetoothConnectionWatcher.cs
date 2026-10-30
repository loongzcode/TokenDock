using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace TokenDock
{
    // Watches paired endpoints only; callbacks are marshalled by the controller.
    sealed class BluetoothConnectionWatcher : IDisposable
    {
        readonly List<DeviceWatcher> watchers = new List<DeviceWatcher>();
        readonly object gate = new object ();
        readonly Dictionary<string, bool> connections = new Dictionary<string, bool>();
        bool disposed;
        public event Action Changed;
        public string LastError { get; private set; }

        public bool Active
        {
            get
            {
                if (disposed)
                    return false;
                foreach (var watcher in watchers)
                    if (watcher.Status == DeviceWatcherStatus.Started || watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                        return true;
                return false;
            }
        }

        public void Start()
        {
            try
            {
                Add(BluetoothDeviceSelector(false));
                Add(BluetoothDeviceSelector(true));
            }
            catch (Exception error)
            {
                LastError = error.Message;
                Dispose();
            }
        }

        static string BluetoothDeviceSelector(bool lowEnergy)
        {
            return lowEnergy ? BluetoothLEDevice.GetDeviceSelectorFromPairingState(true) : Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        }

        void Add(string selector)
        {
            var watcher = DeviceInformation.CreateWatcher(selector, new[] { "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += Added;
            watcher.Updated += Updated;
            watcher.Removed += Removed;
            watchers.Add(watcher);
            watcher.Start();
        }

        void Added(DeviceWatcher sender, DeviceInformation info)
        {
            object value;
            bool connected = info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out value) && value is bool && (bool)value;
            lock (gate)
            {
                if (disposed)
                    return;
                connections[info.Id] = connected;
            }

            Raise();
        }

        void Updated(DeviceWatcher sender, DeviceInformationUpdate info)
        {
            object value;
            if (!info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out value) || !(value is bool))
                return;
            bool changed;
            lock (gate)
            {
                if (disposed)
                    return;
                bool previous;
                changed = !connections.TryGetValue(info.Id, out previous) || previous != (bool)value;
                connections[info.Id] = (bool)value;
            }

            if (changed)
                Raise();
        }

        void Removed(DeviceWatcher sender, DeviceInformationUpdate info)
        {
            lock (gate)
            {
                if (disposed)
                    return;
                connections.Remove(info.Id);
            }

            Raise();
        }

        void Raise()
        {
            var handler = Changed;
            if (handler != null)
                handler();
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                connections.Clear();
            }

            foreach (var watcher in watchers)
            {
                watcher.Added -= Added;
                watcher.Updated -= Updated;
                watcher.Removed -= Removed;
                try
                {
                    if (watcher.Status == DeviceWatcherStatus.Started || watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                        watcher.Stop();
                }
                catch
                {
                }
            }

            watchers.Clear();
        }
    }
}
