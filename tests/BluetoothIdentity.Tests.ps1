$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\src\Bluetooth\DeviceIdentity.ps1')
$script:checks = 0
function Check($Condition, $Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:checks++
}
function Reset-Devices {
    $script:results = @{}; $script:containers = @{}; $script:addresses = @{}
}
function Row($Id, $Connected, $Battery) {
    return [ordered]@{name='Identical keyboard';id=$Id;connected=$Connected;battery=$Battery;source='test';diagnostic=''}
}
$containerA = '11111111-2222-3333-4444-555555555555'
$containerB = '22222222-3333-4444-5555-666666666666'
Check ((ConvertTo-BluetoothContainerKey ('{' + $containerA.ToUpperInvariant() + '}')) -eq $containerA) 'container braces and case normalize'
Check ($null -eq (ConvertTo-BluetoothContainerKey ([guid]::Empty))) 'empty GUID cannot merge unrelated devices'
Check ((ConvertTo-BluetoothAddressKey 'aa:bb:cc:dd:ee:ff') -eq 'AABBCCDDEEFF') 'address punctuation and case normalize'
Reset-Devices
Add-BluetoothEndpoint (Row $containerA $false 20) $containerA 'aa:bb:cc:dd:ee:ff'
Add-BluetoothEndpoint (Row $containerB $true 100) $containerB 'AABBCCDDEEFF'
Check ($script:results.Count -eq 1) 'same hardware address merges different endpoint containers'
$device = Find-BluetoothResult ('{' + $containerB.ToUpperInvariant() + '}') $null
Check ($device.connected -eq $true -and $device.battery -eq 100) 'connected endpoint and its battery win'
Add-BluetoothEndpoint (Row 'inactive' $false 5) $containerA 'AABBCCDDEEFF'
Check ($device.battery -eq 100) 'inactive endpoint cannot replace connected battery'
$device.battery = $null
$pnpDevice = Find-BluetoothResult ('{' + $containerA.ToUpperInvariant() + '}') $null
$pnpDevice.battery = 100
Check ((Find-BluetoothResult $containerB $null).battery -eq 100) 'PnP container alias resolves the same merged device'
Add-BluetoothEndpoint (Row 'second' $true 40) '33333333-4444-5555-6666-777777777777' '112233445566'
Check ($script:results.Count -eq 2) 'identical names with different identities stay separate'
Reset-Devices
Add-BluetoothEndpoint (Row $containerA $false $null) $containerA $null
Add-BluetoothEndpoint (Row 'address-only' $true 0) $null 'AABBCCDDEEFF'
Add-BluetoothEndpoint (Row 'bridge' $true $null) $containerA 'AABBCCDDEEFF'
Check ($script:results.Count -eq 1) 'later endpoint bridges existing identity groups'
Check ((Find-BluetoothResult $containerA $null).battery -eq 0) 'real zero survives bridge merge'
Check ((Find-BluetoothResult $null 'aa:bb:cc:dd:ee:ff').id -eq $containerA) 'all aliases are updated after merge'
Write-Output "PASS $script:checks Bluetooth identity checks"
