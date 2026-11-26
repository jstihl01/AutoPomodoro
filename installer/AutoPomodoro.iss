#if Ver < EncodeVer(6,3,0)
#error AutoPomodoro requiere Inno Setup 6.3.0 o posterior.
#endif
#define MyAppName "AutoPomodoro"
#ifndef MyAppVersion
#error La version de la aplicacion debe proceder de build.ps1
#endif
#ifndef MyBinaryVersion
#error Falta la version numerica del instalador
#endif
#ifndef MyPublishDir
#error Falta la carpeta de publicacion de esta compilacion
#endif
#define MyAppPublisher "jstihl01"
#define MyAppExeName "AutoPomodoro.exe"

[Setup]
#ifdef MyInstallerTestRoot
#ifdef MySignInstaller
#error Un instalador de prueba no puede firmarse.
#endif
AppId=AutoPomodoroTest-{#MyInstallerTestId}
AppName=AutoPomodoro (prueba aislada)
DefaultDirName={#MyInstallerTestRoot}\installed
UninstallDisplayName=AutoPomodoro (prueba aislada)
#else
AppId={{AFA9B34B-028A-4BD3-9A99-48A27847A31B}
AppName={#MyAppName}
DefaultDirName={localappdata}\Programs\{#MyAppName}
UninstallDisplayName={#MyAppName}
AppMutex=Pomodoro-8146708D-9AF6-43D9-87B8-76F527A8AC19
#endif
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyBinaryVersion}
VersionInfoProductVersion={#MyBinaryVersion}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\releases
OutputBaseFilename=AutoPomodoro-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\autopomodoro.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName};Pomodoro.exe
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes
#ifdef MySignInstaller
SignTool=AutoPomodoroSign
SignedUninstaller=yes
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\Pomodoro.exe"
#ifndef MyInstallerTestRoot
Type: files; Name: "{autoprograms}\Pomodoro.lnk"
Type: files; Name: "{autodesktop}\Pomodoro.lnk"

[Icons]
Name: "{autoprograms}\AutoPomodoro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\AutoPomodoro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir AutoPomodoro"; Flags: nowait postinstall skipifsilent
#endif

[Code]
const
  ReparsePoint = $400;
  DirectoryAttribute = $10;

function GetFileAttributes(FileName: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function OwnSettingsFile(const Name: String): Boolean;
var
  Prefix, Id, Extension: String;
  I: Integer;
begin
  Result := True;
  if (Name = 'settings.json') or (Name = 'settings.json.bak') or (Name = 'settings.json.tmp') then
    Exit;

  Result := False;
  Prefix := 'settings.json.';
  if Pos(Prefix, Name) <> 1 then
    Exit;
  Id := Copy(Name, Length(Prefix) + 1, 32);
  if Length(Id) <> 32 then
    Exit;
  Extension := Copy(Name, Length(Prefix) + 33, Length(Name));
  if (Extension <> '.tmp') and (Extension <> '.recovered') then
    Exit;
  for I := 1 to Length(Id) do
    if Pos(Copy(Id, I, 1), '0123456789abcdef') = 0 then
      Exit;
  Result := True;
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
var
  SettingsDir: String;
  Attributes: LongWord;
  Entry: TFindRec;
begin
  if Step <> usPostUninstall then
    Exit;
#ifdef MyInstallerTestRoot
  SettingsDir := ExpandConstant('{app}\..\profile\AutoPomodoro');
#else
  SettingsDir := ExpandConstant('{localappdata}\AutoPomodoro');
#endif
  Attributes := GetFileAttributes(SettingsDir);
  if (Attributes = $FFFFFFFF) or ((Attributes and DirectoryAttribute) = 0) or
     ((Attributes and ReparsePoint) <> 0) then
    Exit;
  if FindFirst(SettingsDir + '\settings.json*', Entry) then
  begin
    try
      repeat
        if ((Entry.Attributes and (DirectoryAttribute or ReparsePoint)) = 0) and
           OwnSettingsFile(Lowercase(Entry.Name)) then
          if not DeleteFile(SettingsDir + '\' + Entry.Name) then
            Log('AutoPomodoro: no se pudo eliminar ' + Entry.Name);
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
  RemoveDir(SettingsDir);
end;
