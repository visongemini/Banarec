param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\release'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $root '.cache'
$package = Join-Path $cache 'screenrecorderlib.7.0.1.zip'
$expected = '5032DB2821316391FE0322739D658BD8CE4EF059B02542E97B09FA1E47C04E84'
$uri = 'https://api.nuget.org/v3-flatcontainer/screenrecorderlib/7.0.1/screenrecorderlib.7.0.1.nupkg'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force $cache,$OutputDirectory | Out-Null
if (!(Test-Path $package)) { Invoke-WebRequest -Uri $uri -OutFile $package -UseBasicParsing }
if ((Get-FileHash $package -Algorithm SHA256).Hash -ne $expected) { throw "Dependency checksum mismatch: $package" }
$expanded = Join-Path $cache 'screenrecorderlib.7.0.1'
Expand-Archive -LiteralPath $package -DestinationPath $expanded -Force
Copy-Item (Join-Path $expanded 'build\x64\ScreenRecorderLib.dll') $OutputDirectory -Force
Write-Host 'ScreenRecorderLib 7.0.1 restored; SHA-256 verified.'
