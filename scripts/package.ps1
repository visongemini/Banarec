param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1')
if (!$CompilerPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $CompilerPath = $command.Source }
    else {
        $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
        $CompilerPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}
if (!$CompilerPath -or !(Test-Path $CompilerPath)) { throw 'Install Inno Setup 6, then provide -CompilerPath pointing to ISCC.exe.' }
& $CompilerPath /Q (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
