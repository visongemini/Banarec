$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ("BanaStudio-VoicePreview-" + [Guid]::NewGuid().ToString('N') + '.exe')
$artifacts = Join-Path $root 'tests\artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$args = @('/nologo','/target:winexe','/platform:x64',"/out:$output",'/r:System.Xaml.dll',
    ("/r:" + (Join-Path $framework 'WPF\WindowsBase.dll')),
    ("/r:" + (Join-Path $framework 'WPF\PresentationCore.dll')),
    ("/r:" + (Join-Path $framework 'WPF\PresentationFramework.dll')),
    ("/resource:" + (Join-Path $root 'src\Main.xaml') + ',Main.xaml'),
    (Join-Path $root 'tests\VoicePreview.cs'),(Join-Path $root 'tests\TestPaths.cs'))
try {
    & $compiler @args
    if ($LASTEXITCODE -ne 0) { throw 'Voice preview build failed.' }
    $oldTestOutput = $env:BANAREC_TEST_OUTPUT
    $env:BANAREC_TEST_OUTPUT = $artifacts
    $process = Start-Process $output -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Voice preview failed with exit $($process.ExitCode)." }
} finally {
    $env:BANAREC_TEST_OUTPUT = $oldTestOutput
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
}
Write-Host 'Voice preview passed. Evidence: tests\artifacts\voice-page.png'
