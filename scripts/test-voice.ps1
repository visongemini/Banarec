param([switch]$KeepBinary)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 x64 compiler is required.' }
$output = Join-Path ([IO.Path]::GetTempPath()) ("BanaStudio-VoiceUnitTest-" + [Guid]::NewGuid().ToString('N') + '.exe')
$references = @(
    'System.dll',
    'System.Core.dll',
    'System.IO.Compression.dll',
    'System.Security.dll',
    'System.Web.Extensions.dll',
    'System.Xaml.dll',
    'System.Windows.Forms.dll',
    (Join-Path $framework 'WPF\UIAutomationClient.dll'),
    (Join-Path $framework 'WPF\UIAutomationTypes.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),
    (Join-Path $framework 'WPF\PresentationCore.dll'),
    (Join-Path $framework 'WPF\PresentationFramework.dll')
)
$args = @('/nologo','/target:exe','/platform:x64',"/out:$output")
foreach ($reference in $references) { $args += "/r:$reference" }
$args += @(
    (Join-Path $root 'tests\VoiceUnitTest.cs'),
    (Join-Path $root 'src\Voice.cs'),
    (Join-Path $root 'src\VoiceFlash.cs'),
    (Join-Path $root 'src\VoiceNative.cs'),
    (Join-Path $root 'src\Hotkeys.cs'),
    (Join-Path $root 'src\WindowBackdrop.cs')
)
try {
    & $compiler @args
    if ($LASTEXITCODE -ne 0) { throw 'Voice unit test build failed.' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw "Voice unit tests failed with exit $LASTEXITCODE." }
} finally {
    if (!$KeepBinary -and (Test-Path -LiteralPath $output)) { Remove-Item -LiteralPath $output -Force }
}
