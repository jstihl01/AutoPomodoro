#define MyAppName "AutoPomodoro"
#define MyAppVersion "1.3.0"
#define MyAppPublisher "jstihl01"
#define MyAppExeName "AutoPomodoro.exe"

[Setup]
AppId={{AFA9B34B-028A-4BD3-9A99-48A27847A31B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AutoPomodoro-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\autopomodoro.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName};Pomodoro.exe
RestartApplications=no
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
AppMutex=Pomodoro-8146708D-9AF6-43D9-87B8-76F527A8AC19
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\Pomodoro.exe"
Type: files; Name: "{autoprograms}\Pomodoro.lnk"
Type: files; Name: "{autodesktop}\Pomodoro.lnk"

[Icons]
Name: "{autoprograms}\AutoPomodoro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\AutoPomodoro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir AutoPomodoro"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\AutoPomodoro"
Type: filesandordirs; Name: "{localappdata}\Pomodoro"
