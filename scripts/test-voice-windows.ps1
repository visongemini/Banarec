$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ("BanaStudio-VoiceWindows-" + [Guid]::NewGuid().ToString('N') + '.exe')
$artifacts = Join-Path $root 'tests\artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.IO.Compression.dll','System.Security.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Windows.Forms.dll',
    (Join-Path $framework 'WPF\UIAutomationClient.dll'),(Join-Path $framework 'WPF\UIAutomationTypes.dll'),(Join-Path $framework 'WPF\WindowsBase.dll'),(Join-Path $framework 'WPF\PresentationCore.dll'),(Join-Path $framework 'WPF\PresentationFramework.dll'))
$args = @('/nologo','/target:winexe','/platform:x64',"/out:$output")
foreach ($reference in $references) { $args += "/r:$reference" }
$args += @((Join-Path $root 'tests\VoiceWindowsPreview.cs'),(Join-Path $root 'tests\TestPaths.cs'),(Join-Path $root 'src\VoiceWindows.cs'),(Join-Path $root 'src\Voice.cs'),(Join-Path $root 'src\VoiceFlash.cs'),(Join-Path $root 'src\VoiceNative.cs'),(Join-Path $root 'src\Hotkeys.cs'),(Join-Path $root 'src\WindowBackdrop.cs'),(Join-Path $root 'src\RecordingBorder.cs'))
try {
    & $compiler @args
    if ($LASTEXITCODE -ne 0) { throw 'Voice window preview build failed.' }
    $oldTestOutput = $env:BANAREC_TEST_OUTPUT
    $env:BANAREC_TEST_OUTPUT = $artifacts
    $process = Start-Process $output -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Voice window preview failed with exit $($process.ExitCode)." }
} finally {
    $env:BANAREC_TEST_OUTPUT = $oldTestOutput
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
}
Write-Host 'Voice windows passed. Evidence: tests\artifacts\voice-hud.png and voice-library.png'
