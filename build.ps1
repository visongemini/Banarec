param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'release'))
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'Windows x64 and .NET Framework 4.8 are required.' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
& (Join-Path $root 'scripts\restore.ps1') -OutputDirectory $OutputDirectory
$refs = @('System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll',
    "$framework\WPF\WindowsBase.dll", "$framework\WPF\PresentationCore.dll",
    "$framework\WPF\PresentationFramework.dll", "$OutputDirectory\ScreenRecorderLib.dll")
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',
    "/win32manifest:$root\src\app.manifest", "/win32icon:$root\assets\Banarec.ico",
    "/out:$OutputDirectory\Banarec.exe", "/resource:$root\src\Main.xaml,Main.xaml")
foreach ($reference in $refs) { $compilerArgs += "/r:$reference" }
$compilerArgs += @(Get-ChildItem "$root\src\*.cs" | Sort-Object Name | ForEach-Object FullName)
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close applications using the output directory and retry.' }
Copy-Item "$root\assets\Banarec.ico","$root\assets\Banarec.png","$root\src\Banarec.exe.config" $OutputDirectory -Force
$guide = Get-ChildItem (Join-Path $root 'docs') -Filter '*.txt' | Select-Object -First 1
if (!$guide) { throw 'The user guide is missing.' }
Copy-Item $guide.FullName (Join-Path $OutputDirectory 'guide.txt') -Force
Copy-Item "$root\third_party\ScreenRecorderLib.LICENSE" (Join-Path $OutputDirectory 'THIRD-PARTY-LICENSES.txt') -Force
Write-Host "Built $OutputDirectory\Banarec.exe"
