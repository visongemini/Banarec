param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1')
if (!$CompilerPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $CompilerPath = $command.Source }
    else {
        $candidates = @(
            (Join-Path $root '.cache\Inno Setup 6\ISCC.exe'),
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )
        $CompilerPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}
if (!$CompilerPath -or !(Test-Path $CompilerPath)) { throw 'Install Inno Setup 6, then provide -CompilerPath pointing to ISCC.exe.' }
& $CompilerPath /Q (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$installer = Join-Path $root 'dist\BanaStudio-Setup-3.0.0.exe'
if (!(Test-Path $installer)) { throw 'Installer build completed without the expected BanaStudio 3.0.0 output.' }
$portable = Join-Path $root 'dist\BanaStudio-Portable-3.0.0.zip'
$portableNames = @(
    'BanaStudio.exe',
    'BanaStudio.exe.config',
    'BanaStudio.ico',
    'BanaStudio.png',
    'ScreenRecorderLib.dll',
    'guide.txt',
    'THIRD-PARTY-LICENSES.txt'
)
$portableFiles = $portableNames | ForEach-Object {
    $path = Join-Path $root ('release\' + $_)
    if (!(Test-Path -LiteralPath $path)) { throw "Portable package input is missing: $_" }
    $path
}
Compress-Archive -LiteralPath $portableFiles -DestinationPath $portable -CompressionLevel Optimal -Force
if (!(Test-Path $portable)) { throw 'Portable package build failed.' }
$sums = Join-Path $root 'dist\SHA256SUMS.txt'
$hashLines = @($installer,$portable) | ForEach-Object {
    $file = Get-Item -LiteralPath $_
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
[IO.File]::WriteAllLines($sums,$hashLines,[Text.Encoding]::ASCII)
Write-Host "Built $installer"
Write-Host "Built $portable"
Write-Host "Wrote $sums"
