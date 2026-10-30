param([switch]$SkipValidation)
$ErrorActionPreference = 'Stop'
if (-not $SkipValidation) { & (Join-Path $PSScriptRoot 'build.ps1'); & (Join-Path $PSScriptRoot 'test.ps1') -SkipBuild }
$application = Join-Path $PSScriptRoot 'bin\TokenDock.exe'
$version = ([version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($application).FileVersion)).ToString(3)
$distribution = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $distribution -Force | Out-Null
$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$stage = Join-Path $tempBase ('TokenDock-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $runtime = Join-Path $stage ('TokenDock-' + $version)
    $source = Join-Path $stage ('TokenDock-' + $version + '-source')
    New-Item -ItemType Directory -Path $runtime,$source | Out-Null
    foreach ($name in @('TokenDock.exe','TokenDock.exe.config','bluetooth.ps1','DeviceIdentity.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('bin\' + $name)) -Destination (Join-Path $runtime $name) }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\使用说明.txt') -Destination (Join-Path $runtime '使用说明.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\图标来源.txt') -Destination (Join-Path $runtime '图标来源.txt')
    foreach ($directory in @('src','tests','tools','docs')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $directory) -Recurse -File) {
            $relative = $file.FullName.Substring($PSScriptRoot.Length + 1)
            if ($relative -match '(^|\\)(bin|obj)(\\|$)') { continue }
            $destination = Join-Path $source $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    foreach ($name in @('TokenDock.sln','README.md','CHANGELOG.txt','.gitignore','build.ps1','test.ps1','package.ps1','bluetooth.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source $name) }
    $runtimeZip = Join-Path $distribution ('TokenDock-' + $version + '.zip')
    $sourceZip = Join-Path $distribution ('TokenDock-' + $version + '-source.zip')
    Compress-Archive -LiteralPath $runtime -DestinationPath $runtimeZip -Force
    Compress-Archive -LiteralPath $source -DestinationPath $sourceZip -Force
    Write-Output $runtimeZip
    Write-Output $sourceZip
} finally {
    $resolvedStage = [System.IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($tempBase,[System.StringComparison]::OrdinalIgnoreCase) -or [System.IO.Path]::GetFileName($resolvedStage) -notmatch '^TokenDock-package-[0-9a-f]{32}$') { throw 'Refusing unsafe cleanup path.' }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
