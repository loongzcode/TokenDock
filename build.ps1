param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools\compiler.ps1')
$buildDirectory = if ($OutputDirectory) { $OutputDirectory } else { Join-Path $PSScriptRoot 'bin' }
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$buildDirectory = (Resolve-Path -LiteralPath $buildDirectory).Path
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | Select-Object -ExpandProperty FullName)
$compiler = Get-FrameworkCompiler
$references = Get-AppReferences $compiler
$resources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\Assets\Brands') -Filter '*.png' -File | ForEach-Object { '/resource:' + $_.FullName + ',TokenDock.Brands.' + $_.Name })
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 "/out:$buildDirectory\TokenDock.exe" "/win32manifest:$PSScriptRoot\src\app.manifest" @references @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bluetooth.ps1') -Destination (Join-Path $buildDirectory 'bluetooth.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\Bluetooth\DeviceIdentity.ps1') -Destination (Join-Path $buildDirectory 'DeviceIdentity.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\App.config') -Destination (Join-Path $buildDirectory 'TokenDock.exe.config') -Force
Write-Output "Built $buildDirectory\TokenDock.exe"
