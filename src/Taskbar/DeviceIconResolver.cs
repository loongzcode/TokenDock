using System;
using System.Text.RegularExpressions;

namespace TokenDock
{
    static class DeviceIconResolver
    {
        public static string ResolveDevice(BluetoothDevice device)
        {
            return device.kind == "mouse" || device.kind == "keyboard" || device.kind == "headphones" ? device.kind : Resolve(device.name);
        }

        public static string Resolve(string name)
        {
            name = name ?? "";
            if (Regex.IsMatch(name, @"keyboard|键盘|(?:^|[\s_-])RK[\s_-]*(?:R\d+|\d+)", RegexOptions.IgnoreCase))
                return "keyboard";
            if (Regex.IsMatch(name, @"mouse|鼠标|trackpad|触控板", RegexOptions.IgnoreCase))
                return "mouse";
            if (Regex.IsMatch(name, @"headphone|headset|earbud|earphone|耳机|AirPods|\bBuds\b", RegexOptions.IgnoreCase))
                return "headphones";
            return "device";
        }
    }
}
