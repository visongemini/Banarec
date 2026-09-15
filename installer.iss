[Setup]
AppId={{5A498A99-58C9-4193-B12F-50252995DBFA}
AppName=Banarec · 香蕉录屏
AppVersion=2.1.0
AppPublisher=Banarec
DefaultDirName={localappdata}\Programs\Banarec
DefaultGroupName=Banarec
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=dist
OutputBaseFilename=Banarec-Setup-2.1.0
SetupIconFile=assets\Banarec.ico
UninstallDisplayIcon={app}\Banarec.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
DisableProgramGroupPage=yes
AppMutex=Local\Banarec.Desktop
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=2.1.0.0
VersionInfoDescription=Banarec · 香蕉录屏 安装程序
UninstallDisplayName=Banarec · 香蕉录屏

[Languages]
Name: "chinesesimp"; MessagesFile: "third_party\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce
Name: "autostart"; Description: "登录时自动启动（驻留托盘）"; Flags: checkedonce

[Files]
Source: "release\Banarec.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\Banarec.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\Banarec.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\Banarec.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\ScreenRecorderLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\第三方许可.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Banarec · 香蕉录屏"; Filename: "{app}\Banarec.exe"
Name: "{autodesktop}\Banarec · 香蕉录屏"; Filename: "{app}\Banarec.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Banarec"; ValueData: """{app}\Banarec.exe"" --startup"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Banarec.exe"; Description: "打开 Banarec"; Flags: nowait postinstall skipifsilent
