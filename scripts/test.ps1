param([ValidateSet('Integration','Hotkey','Preview','Annotation')][string]$Suite = 'Integration')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (Get-Process Banarec -ErrorAction SilentlyContinue) { throw 'Exit Banarec using its tray menu before running desktop tests.' }
& (Join-Path $root 'build.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$entry = switch ($Suite) { 'Hotkey' { 'HotkeyTest' } 'Annotation' { 'AnnotationTest' } default { $Suite } }
$destination = Join-Path $root "release\$entry.exe"
$args = @('/nologo','/target:winexe','/platform:x64',"/main:$entry", "/out:$destination",
    '/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Xaml.dll',
    "/r:$framework\WPF\WindowsBase.dll", "/r:$framework\WPF\PresentationCore.dll",
    "/r:$framework\WPF\PresentationFramework.dll", "/r:$root\release\ScreenRecorderLib.dll",
    "/resource:$root\src\Main.xaml,Main.xaml", "$root\tests\$entry.cs", "$root\tests\TestPaths.cs")
$args += @(Get-ChildItem "$root\src\*.cs" | Sort-Object Name | ForEach-Object FullName)
& "$framework\csc.exe" @args
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
$settings = Join-Path $env:LOCALAPPDATA 'Banarec\settings.ini'
$hadSettings = Test-Path $settings
$backup = if ($hadSettings) { [IO.File]::ReadAllBytes($settings) } else { $null }
try {
    $process = Start-Process $destination -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Test failed with exit $($process.ExitCode). Check tests\artifacts." }
} finally {
    if ($hadSettings) { [IO.File]::WriteAllBytes($settings,$backup) }
    elseif (Test-Path $settings) { Remove-Item -LiteralPath $settings }
}
Write-Host "$Suite passed. Evidence: tests\artifacts"
