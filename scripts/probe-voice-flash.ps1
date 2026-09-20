param(
    [Parameter(Mandatory = $true)][string]$WavPath,
    [Parameter(Mandatory = $true)][string]$EnvFile,
    [switch]$SaveDpapi
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ("BanaStudio-VoiceFlashProbe-" + [Guid]::NewGuid().ToString('N') + '.exe')
$references = @(
    'System.dll','System.Core.dll','System.IO.Compression.dll','System.Security.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Windows.Forms.dll',
    (Join-Path $framework 'WPF\UIAutomationClient.dll'),(Join-Path $framework 'WPF\UIAutomationTypes.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),(Join-Path $framework 'WPF\PresentationCore.dll'),(Join-Path $framework 'WPF\PresentationFramework.dll')
)
$args = @('/nologo','/target:exe','/platform:x64',"/out:$output")
foreach ($reference in $references) { $args += "/r:$reference" }
$args += @((Join-Path $root 'tests\VoiceFlashProbe.cs'),(Join-Path $root 'src\Voice.cs'),(Join-Path $root 'src\VoiceFlash.cs'),(Join-Path $root 'src\VoiceNative.cs'),(Join-Path $root 'src\Hotkeys.cs'),(Join-Path $root 'src\WindowBackdrop.cs'))
try {
    & $compiler @args
    if ($LASTEXITCODE -ne 0) { throw 'Flash probe build failed.' }
    $probeArgs = @((Resolve-Path -LiteralPath $WavPath).Path,(Resolve-Path -LiteralPath $EnvFile).Path)
    if ($SaveDpapi) { $probeArgs += '--save-dpapi' }
    & $output @probeArgs
    if ($LASTEXITCODE -ne 0) { throw "Flash probe failed with exit $LASTEXITCODE." }
} finally {
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
}
