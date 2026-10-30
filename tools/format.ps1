$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Formatting requires PowerShell 7; build and test also support Windows PowerShell.' }
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$normalize = [Microsoft.CodeAnalysis.SyntaxNodeExtensions].GetMethods() | Where-Object { $_.Name -eq 'NormalizeWhitespace' -and $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$normalize = $normalize.MakeGenericMethod([Microsoft.CodeAnalysis.SyntaxNode])
$projectRoot = Split-Path -Parent $PSScriptRoot
foreach ($directory in @('src','tests')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot $directory) -Filter '*.cs' -Recurse -File) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([System.IO.File]::ReadAllText($file.FullName))
        $formatted = $normalize.Invoke($null, @($tree.GetRoot(), '    ', "`r`n", $false)).ToFullString()
        [System.IO.File]::WriteAllText($file.FullName, $formatted + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
    }
}
Write-Output 'C# source formatted.'
