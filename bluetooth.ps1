param([switch]$ReadGatt)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType=WindowsRuntime]
$null = [Windows.Devices.Enumeration.DeviceInformationCollection, Windows.Devices.Enumeration, ContentType=WindowsRuntime]
$null = [Windows.Devices.Enumeration.DeviceInformationKind, Windows.Devices.Enumeration, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.BluetoothLEDevice, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.BluetoothCacheMode, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult, Windows.Devices.Bluetooth, ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType=WindowsRuntime]

$script:asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetGenericArguments().Count -eq 1 -and
    $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
} | Select-Object -First 1
function Await-WinRT($Operation, [Type]$ResultType, [int]$Timeout = 4000) {
    $task = $script:asTask.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    if (-not $task.Wait($Timeout)) { try { $Operation.Cancel() } catch {}; throw 'Device request timed out' }
    return $task.GetAwaiter().GetResult()
}
function Get-PropertyValue($Properties, [string]$Key) {
    if ($Properties -is [System.Collections.IDictionary]) { return $Properties[$Key] }
    foreach ($entry in $Properties) { if ($entry.Key -eq $Key) { return $entry.Value } }
    return $null
}

. (Join-Path $PSScriptRoot 'DeviceIdentity.ps1')
$script:containers = @{}
$script:results = @{}
$script:addresses = @{}
$script:errors = 0
$script:diagnostics = @()
$props = [string[]]@('System.Devices.Aep.DeviceAddress', 'System.Devices.Aep.IsConnected',
    'System.Devices.BatteryLife', 'System.Devices.Aep.ContainerId')
$scanTimer = [Diagnostics.Stopwatch]::StartNew()
foreach ($transport in @('Classic', 'LE')) {
    try {
        $selector = if ($transport -eq 'LE') { [Windows.Devices.Bluetooth.BluetoothLEDevice]::GetDeviceSelectorFromPairingState($true) }
                    else { [Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelectorFromPairingState($true) }
        $operation = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector, $props,
            [Windows.Devices.Enumeration.DeviceInformationKind]::AssociationEndpoint)
        $infos = Await-WinRT $operation ([Windows.Devices.Enumeration.DeviceInformationCollection]) 6000
        foreach ($info in $infos) {
            if ([string]::IsNullOrWhiteSpace($info.Name)) { continue }
            $address = Get-PropertyValue $info.Properties 'System.Devices.Aep.DeviceAddress'
            $container = Get-PropertyValue $info.Properties 'System.Devices.Aep.ContainerId'
            $containerKey = ConvertTo-BluetoothContainerKey $container
            $addressKey = ConvertTo-BluetoothAddressKey $address
            $dedupe = if ($containerKey) { $containerKey } elseif ($addressKey) { $addressKey } else { $info.Id }
            $connected = Get-PropertyValue $info.Properties 'System.Devices.Aep.IsConnected'
            $row = [ordered]@{ name = $info.Name; id = $dedupe; connected = $connected; battery = $null; source = ''; diagnostic = '' }
            foreach ($propertyName in @('System.Devices.BatteryLife')) {
                $v = Get-PropertyValue $info.Properties $propertyName
                if ($null -ne $v -and "$v" -match '^\d{1,3}$' -and [int]$v -le 100) {
                    $row.battery = [int]$v; $row.source = 'Windows 上报'; break
                }
            }
            $device = $null
            try {
                # Only query paired, connected LE devices. Never initiate pairing or maintain connections.
                if ($ReadGatt -and $transport -eq 'LE' -and $connected -eq $true -and
                    $null -eq $row.battery -and $scanTimer.Elapsed.TotalSeconds -lt 26) {
                    $normalizedAddress = "$address" -replace '[^0-9A-Fa-f]', ''
                    if ($normalizedAddress.Length -eq 12) {
                        $device = Await-WinRT ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromBluetoothAddressAsync([Convert]::ToUInt64($normalizedAddress,16))) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
                    } else {
                        $device = Await-WinRT ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromIdAsync($info.Id)) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
                    }
                    if ($null -eq $device) { $row.diagnostic = 'Windows 未返回可访问的 BLE 设备对象' }
                    if ($null -ne $device -and "$($device.ConnectionStatus)" -eq 'Connected') {
                        $serviceResult = Await-WinRT ($device.GetGattServicesForUuidAsync([guid]'0000180f-0000-1000-8000-00805f9b34fb',
                            [Windows.Devices.Bluetooth.BluetoothCacheMode]::Cached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult])
                        if ("$($serviceResult.Status)" -ne 'Success' -or @($serviceResult.Services).Count -eq 0) {
                            $serviceResult = Await-WinRT ($device.GetGattServicesForUuidAsync([guid]'0000180f-0000-1000-8000-00805f9b34fb',
                                [Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult]) 8000
                        }
                        $row.diagnostic = '标准电量服务查询：' + "$($serviceResult.Status)" + '，服务数量：' + @($serviceResult.Services).Count
                        foreach ($service in $serviceResult.Services) {
                            try {
                                $characteristics = Await-WinRT ($service.GetCharacteristicsForUuidAsync([guid]'00002a19-0000-1000-8000-00805f9b34fb',
                                    [Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult]) 6000
                                foreach ($characteristic in $characteristics.Characteristics) {
                                    $value = Await-WinRT ($characteristic.ReadValueAsync([Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult])
                                    $row.diagnostic = '标准电量读取：' + "$($value.Status)"
                                    if ("$($value.Status)" -eq 'Success' -and $value.Value.Length -gt 0) {
                                        $reader = [Windows.Storage.Streams.DataReader]::FromBuffer($value.Value)
                                        try { $n = $reader.ReadByte(); if ($n -le 100) { $row.battery = [int]$n; $row.source = '标准 BLE 电量服务' } }
                                        finally { $reader.Dispose() }
                                    }
                                    if ($null -ne $row.battery) { break }
                                }
                            } finally { $service.Dispose() }
                            if ($null -ne $row.battery) { break }
                        }
                    }
                }
            } catch { $row.diagnostic = '电量读取失败：' + $_.Exception.Message } finally { if ($null -ne $device) { try { $device.Dispose() } catch {} } }
            Add-BluetoothEndpoint $row $container $address
        }
    } catch { $script:errors++; $script:diagnostics += ($transport + ': ' + $_.Exception.Message) }
}

# Windows PnP battery property fallback, joined by container GUID. Presence is NOT connection status.
if (Get-Command Get-PnpDevice -ErrorAction SilentlyContinue) {
    try {
        $pnp = @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object { $_.InstanceId -match '^(BTH|HID)' })
        # Batch queries avoid exhausting the scan budget before reaching battery nodes.
        $batteryProperties = @()
        if ($pnp.Count -gt 0) {
            $batteryProperties = @(Get-PnpDeviceProperty -InstanceId @($pnp.InstanceId) -KeyName @('{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2','{49CD1F76-5626-4B17-A4E8-18B4AA1A2213} 10') -ErrorAction SilentlyContinue |
                Where-Object { $null -ne $_.Data -and "$($_.Data)" -match '^\d{1,3}$' -and [int]$_.Data -le 100 })
        }
        foreach ($batteryProperty in $batteryProperties) {
            $instanceId = $batteryProperty.InstanceId
            $containerProperty = Get-PnpDeviceProperty -InstanceId $instanceId -KeyName 'DEVPKEY_Device_ContainerId' -ErrorAction SilentlyContinue
            $matched = Find-BluetoothResult $containerProperty.Data $null
            if ($null -eq $matched) {
                $parentProperty = Get-PnpDeviceProperty -InstanceId $instanceId -KeyName 'DEVPKEY_Device_Parent' -ErrorAction SilentlyContinue
                $identity = $instanceId + ' ' + "$($parentProperty.Data)"
                if ($identity -match '(?i)DEV_([0-9A-F]{12})') { $matched = Find-BluetoothResult $null $Matches[1] }
            }
            if ($null -ne $matched) {
                $matched.battery = [int]$batteryProperty.Data
                $matched.source = 'Windows 上报（可能有缓存）'
                $matched.diagnostic = '已从 Windows PnP 属性读取电量'
            }
        }
    } catch { $script:diagnostics += ('Windows PnP 电量读取：' + $_.Exception.Message) }
}
$devices = @($script:results.Values | Sort-Object name)
$status = if ($script:errors -eq 2) { '蓝牙枚举不可用，请检查蓝牙适配器、系统服务与权限。' }
          elseif ($script:errors -gt 0) { '部分蓝牙扫描失败；已显示成功读取的设备。' }
          elseif ($devices.Count -eq 0) { '未发现已配对蓝牙设备。' }
          else { '自动发现已配对设备；未上报电量的设备显示不可用。' }
@{ devices = $devices; status = $status; scanErrors = $script:errors; diagnostics = $script:diagnostics } | ConvertTo-Json -Depth 5 -Compress
