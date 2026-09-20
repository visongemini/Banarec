[Setup]
AppId={{5A498A99-58C9-4193-B12F-50252995DBFA}
AppName=BanaStudio
AppVersion=3.0.0
AppPublisher=BanaStudio
DefaultDirName={localappdata}\Programs\BanaStudio
DefaultGroupName=BanaStudio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=dist
OutputBaseFilename=BanaStudio-Setup-3.0.0
SetupIconFile=assets\BanaStudio.ico
UninstallDisplayIcon={app}\BanaStudio.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
DisableProgramGroupPage=yes
AppMutex=Local\Banarec.Desktop
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=3.0.0.0
VersionInfoDescription=BanaStudio 安装程序
UninstallDisplayName=BanaStudio

[Languages]
Name: "chinesesimp"; MessagesFile: "third_party\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce
Name: "autostart"; Description: "登录时自动启动（驻留托盘）"; Flags: checkedonce

[Files]
Source: "release\BanaStudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\BanaStudio.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\BanaStudio.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\BanaStudio.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\ScreenRecorderLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\guide.txt"; DestDir: "{app}"; DestName: "使用说明.txt"; Flags: ignoreversion
Source: "release\THIRD-PARTY-LICENSES.txt"; DestDir: "{app}"; DestName: "第三方许可.txt"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\Banarec.exe"
Type: files; Name: "{app}\Banarec.exe.config"
Type: files; Name: "{app}\Banarec.ico"
Type: files; Name: "{app}\Banarec.png"
Type: files; Name: "{autoprograms}\Banarec · 香蕉录屏.lnk"
Type: files; Name: "{autodesktop}\Banarec · 香蕉录屏.lnk"

[Icons]
Name: "{autoprograms}\BanaStudio"; Filename: "{app}\BanaStudio.exe"
Name: "{autodesktop}\BanaStudio"; Filename: "{app}\BanaStudio.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Banarec"; ValueData: """{app}\BanaStudio.exe"" --startup"; Check: HadExistingAutoStart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "BanaStudio"; Flags: deletevalue uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Banarec"; ValueData: """{app}\BanaStudio.exe"" --startup"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BanaStudio.exe"; Description: "打开 BanaStudio"; Flags: nowait postinstall skipifsilent

[Code]
var
  ExistingAutoStart: Boolean;

function InitializeSetup(): Boolean;
begin
  ExistingAutoStart :=
    RegValueExists(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Banarec') or
    RegValueExists(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'BanaStudio');
  Result := True;
end;

function HadExistingAutoStart(): Boolean;
begin
  Result := ExistingAutoStart;
end;
