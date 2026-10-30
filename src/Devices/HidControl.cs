using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace TokenDock
{
    sealed class HidEndpoint
    {
        public string Path, Fingerprint, Manufacturer;
        public ushort Vendor, Product, UsagePage, Usage, InputLength, OutputLength;
    }

    static class HidControl
    {
        [StructLayout(LayoutKind.Sequential)]
        struct InterfaceData
        {
            public int Size;
            public Guid Guid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Attributes
        {
            public int Size;
            public ushort Vendor, Product, Version;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Caps
        {
            public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort Links, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
        }

        [DllImport("hid.dll")]
        static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr info);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
        [DllImport("hid.dll")]
        static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
        [DllImport("hid.dll")]
        static extern bool HidD_GetManufacturerString(SafeFileHandle handle, byte[] value, int length);
        [DllImport("hid.dll")]
        static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, byte[] value, int length);
        [DllImport("hid.dll")]
        static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")]
        static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]
        static extern bool HidD_FlushQueue(SafeFileHandle handle);
        [DllImport("hid.dll")]
        static extern int HidP_GetCaps(IntPtr data, out Caps caps);
        public static bool Eligible(HidEndpoint endpoint)
        {
            return endpoint.Vendor == 0xA8A5 && endpoint.Product == 0x2255 && endpoint.Manufacturer == "YJX-CHIP" && endpoint.UsagePage == 0xFF01 && endpoint.Usage == 0x10 && endpoint.InputLength == 65 && endpoint.OutputLength == 65;
        }

        public static List<HidEndpoint> Enumerate()
        {
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            var rows = new List<HidEndpoint>();
            if (set == new IntPtr(-1))
                return rows;
            try
            {
                for (uint index = 0;; index++)
                {
                    var data = new InterfaceData
                    {
                        Size = Marshal.SizeOf(typeof(InterfaceData))
                    };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data))
                        break;
                    uint needed;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                    if (needed < 8 || needed > 65536)
                        continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, needed, out needed, IntPtr.Zero))
                            continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (path.IndexOf("vid_a8a5&pid_2255", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        using (var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                        {
                            if (handle.IsInvalid)
                                continue;
                            var attributes = new Attributes
                            {
                                Size = Marshal.SizeOf(typeof(Attributes))
                            };
                            if (!HidD_GetAttributes(handle, ref attributes))
                                continue;
                            IntPtr preparsed;
                            if (!HidD_GetPreparsedData(handle, out preparsed))
                                continue;
                            try
                            {
                                Caps caps;
                                if (HidP_GetCaps(preparsed, out caps) != 1114112)
                                    continue;
                                var bytes = new byte[512];
                                string manufacturer = HidD_GetManufacturerString(handle, bytes, bytes.Length) ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : "";
                                Array.Clear(bytes, 0, bytes.Length);
                                string serial = HidD_GetSerialNumberString(handle, bytes, bytes.Length) ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : "";
                                string identity = attributes.Vendor.ToString("X4") + ":" + attributes.Product.ToString("X4") + ":" + (serial.Length > 0 ? serial : path.ToLowerInvariant());
                                string fingerprint;
                                using (var hash = SHA256.Create())
                                    fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 24);
                                var endpoint = new HidEndpoint
                                {
                                    Path = path,
                                    Fingerprint = fingerprint,
                                    Manufacturer = manufacturer,
                                    Vendor = attributes.Vendor,
                                    Product = attributes.Product,
                                    Usage = caps.Usage,
                                    UsagePage = caps.UsagePage,
                                    InputLength = caps.InputLength,
                                    OutputLength = caps.OutputLength
                                };
                                if (Eligible(endpoint))
                                    rows.Add(endpoint);
                            }
                            finally
                            {
                                HidD_FreePreparsedData(preparsed);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }

            return rows;
        }

        public static byte[] Query(HidEndpoint endpoint, byte command)
        {
            if (!Eligible(endpoint))
                throw new InvalidOperationException("不匹配已验证的厂商控制接口。");
            byte[] packet = Flx1Protocol.Query(command);
            using (var handle = CreateFile(endpoint.Path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    return null;
                using (var stream = new FileStream(handle, FileAccess.ReadWrite, 65, true))
                {
                    HidD_FlushQueue(handle);
                    var write = stream.WriteAsync(packet, 0, packet.Length);
                    if (Task.WhenAny(write, Task.Delay(1200)).GetAwaiter().GetResult() != write)
                    {
                        CancelIoEx(handle, IntPtr.Zero);
                        return null;
                    }

                    write.GetAwaiter().GetResult();
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(1500);
                    for (int index = 0; index < 16; index++)
                    {
                        int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                        if (remaining <= 0)
                            return null;
                        var reply = new byte[65];
                        var read = stream.ReadAsync(reply, 0, reply.Length);
                        if (Task.WhenAny(read, Task.Delay(remaining)).GetAwaiter().GetResult() != read)
                        {
                            CancelIoEx(handle, IntPtr.Zero);
                            return null;
                        }

                        int length = read.GetAwaiter().GetResult();
                        if (length == 65 && reply[0] == 0 && reply[1] == 0xAA && reply[2] == command)
                            return reply;
                    }
                }
            }

            return null;
        }
    }
}
