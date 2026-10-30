param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools\compiler.ps1')
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
$compiler = Get-FrameworkCompiler
$references = Get-AppReferences $compiler
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -Recurse -File | Sort-Object FullName | Select-Object -ExpandProperty FullName)
& $compiler /nologo /target:exe /platform:anycpu /codepage:65001 "/out:$PSScriptRoot\bin\TokenDock.Tests.exe" "/reference:$PSScriptRoot\bin\TokenDock.exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $PSScriptRoot 'bin\TokenDock.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& (Join-Path $PSScriptRoot 'tests\BluetoothIdentity.Tests.ps1')
