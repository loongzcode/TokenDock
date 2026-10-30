function Get-FrameworkCompiler {
    $frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $frameworkCompiler)) { throw '.NET Framework C# compiler not found.' }
    return $frameworkCompiler
}
function Get-AppReferences([string]$Compiler) {
    $frameworkDirectory = Split-Path -Parent $Compiler
    return @(
        '/reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Security.dll,System.Web.Extensions.dll,System.Net.Http.dll',
        "/reference:$frameworkDirectory\WPF\WindowsBase.dll",
        "/reference:$frameworkDirectory\WPF\UIAutomationClient.dll",
        "/reference:$frameworkDirectory\WPF\UIAutomationTypes.dll",
        "/reference:$frameworkDirectory\System.Runtime.dll",
        "/reference:$frameworkDirectory\System.ObjectModel.dll",
        "/reference:$frameworkDirectory\System.Runtime.InteropServices.WindowsRuntime.dll",
        "/reference:$env:WINDIR\System32\WinMetadata\Windows.Devices.winmd",
        "/reference:$env:WINDIR\System32\WinMetadata\Windows.Foundation.winmd"
    )
}
