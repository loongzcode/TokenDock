function ConvertTo-BluetoothContainerKey($Value) {
    $parsed = [guid]::Empty
    if ([guid]::TryParse("$Value", [ref]$parsed) -and $parsed -ne [guid]::Empty) { return $parsed.ToString('D') }
    return $null
}

function ConvertTo-BluetoothAddressKey($Value) {
    $normalized = ("$Value" -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    if ($normalized.Length -eq 12) { return $normalized }
    return $null
}

function Find-BluetoothResult($Container, $Address) {
    $containerKey = ConvertTo-BluetoothContainerKey $Container
    $addressKey = ConvertTo-BluetoothAddressKey $Address
    if ($containerKey -and $script:containers.ContainsKey($containerKey)) { return $script:results[$script:containers[$containerKey]] }
    if ($addressKey -and $script:addresses.ContainsKey($addressKey)) { return $script:results[$script:addresses[$addressKey]] }
    return $null
}

function Merge-BluetoothReading($Target, $Incoming) {
    $wasConnected = $Target.connected -eq $true
    if ($Incoming.connected -eq $true) { $Target.connected = $true }
    elseif ($null -eq $Target.connected) { $Target.connected = $Incoming.connected }
    # An inactive endpoint must not replace a reading from the connected endpoint.
    if ($null -ne $Incoming.battery -and ($null -eq $Target.battery -or -not $wasConnected -or $Incoming.connected -eq $true)) {
        $Target.battery = $Incoming.battery; $Target.source = $Incoming.source
    }
    if ($Incoming.diagnostic) { $Target.diagnostic = $Incoming.diagnostic }
}

function Add-BluetoothEndpoint($Row, $Container, $Address) {
    $containerKey = ConvertTo-BluetoothContainerKey $Container
    $addressKey = ConvertTo-BluetoothAddressKey $Address
    $existing = Find-BluetoothResult $Container $Address
    if ($null -eq $existing -and $script:results.ContainsKey($Row.id)) { $existing = $script:results[$Row.id] }
    if ($null -eq $existing) { $existing = $Row; $script:results[$Row.id] = $Row }
    else { Merge-BluetoothReading $existing $Row }
    # A later endpoint can bridge two previously separate container/address groups.
    if ($addressKey -and $script:addresses.ContainsKey($addressKey)) {
        $otherId = $script:addresses[$addressKey]
        if ($otherId -ne $existing.id) {
            Merge-BluetoothReading $existing $script:results[$otherId]
            $script:results.Remove($otherId)
            foreach ($index in @($script:containers, $script:addresses)) {
                foreach ($key in @($index.Keys)) { if ($index[$key] -eq $otherId) { $index[$key] = $existing.id } }
            }
        }
    }
    if ($containerKey) { $script:containers[$containerKey] = $existing.id }
    if ($addressKey) { $script:addresses[$addressKey] = $existing.id }
}
