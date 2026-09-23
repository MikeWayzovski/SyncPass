; Compile after publish-windows.bat so publish\win-x64 contains a clean factory payload.
; Requires Inno Setup 6: https://jrsoftware.org/isdl.php

#define AppName "Trimble Connector"
#define AppVersion "2.5.5"
#define AppPublisher "MikeWayzovski"
#define ServiceName "TrimbleConnector"
#define DashboardUrl "http://localhost:5000"

[Setup]
AppId={{D37B4A10-14C0-410E-80A0-9B0554FA8888}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Trimble Connector
DefaultGroupName=Trimble Connector
DisableProgramGroupPage=yes
InfoBeforeFile=installer\welcome-nl.txt
OutputBaseFilename=TrimbleConnector-Setup-v{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=no

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
dutch.OpenDashboard=Open het dashboard na installatie (inloggen en eerste map koppelen)
dutch.DesktopIcon=Snelkoppeling op het bureaublad
dutch.ResetData=Opnieuw beginnen: verwijder bestaande mappen, tokens en sync-geschiedenis
dutch.UninstallWipe=Wil je ook inloggegevens, mappenkoppelingen en sync-geschiedenis verwijderen?%nKies Nee om die later te kunnen hergebruiken.
english.OpenDashboard=Open the dashboard after setup (sign in and link the first folder)
english.DesktopIcon=Create a desktop shortcut
english.ResetData=Start over: remove existing folders, tokens, and sync history
english.UninstallWipe=Also remove sign-in data, folder mappings, and sync history?%nChoose No if you want to keep them for a later install.

[Tasks]
Name: "opendashboard"; Description: "{cm:OpenDashboard}"; Flags: checkedonce
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked
Name: "resetdata"; Description: "{cm:ResetData}"; Flags: unchecked; Check: PreviousInstallExists

[Files]
; Factory payload from publish-windows.bat — never pack data/, tokens, or live jobs.
Source: "publish\win-x64\*"; DestDir: "{app}"; Excludes: "data\*,*.token,appsettings.json,appsettings.Development.json,.application_credentials,install-service.bat,uninstall-service.bat"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\win-x64\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion
Source: "publish\win-x64\appsettings.json"; DestDir: "{tmp}"; DestName: "appsettings.factory.json"; Flags: dontcopy

[Icons]
Name: "{group}\Trimble Connector Dashboard"; Filename: "{#DashboardUrl}"
Name: "{group}\Uninstall Trimble Connector"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Trimble Connector"; Filename: "{#DashboardUrl}"; Tasks: desktopicon

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\TrimbleConnector.exe"" DisplayName= ""{#AppName}"" start= auto"; Flags: runhidden; StatusMsg: "Registering Trimble Connector service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Trimble Connector Daemon - Automatic Network Drive to Trimble Connect Cloud Sync Service"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} actions= restart/60000/restart/60000/restart/60000 reset= 86400"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden; StatusMsg: "Starting Trimble Connector service..."

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden; RunOnceId: "StopTrimbleConnector"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden; RunOnceId: "DeleteTrimbleConnector"

[Code]
var
  DeleteUserDataOnUninstall: Boolean;

function PreviousInstallExists(): Boolean;
begin
  Result := DirExists(ExpandConstant('{app}\data')) or
            FileExists(ExpandConstant('{app}\data\sync-jobs.json')) or
            FileExists(ExpandConstant('{app}\data\refresh.token'));
end;

procedure WipeUserData();
var
  AppDir: String;
begin
  AppDir := ExpandConstant('{app}');
  DelTree(AppDir + '\data', True, True, True);
  DeleteFile(AppDir + '\.application_credentials');
  ExtractTemporaryFile('appsettings.factory.json');
  FileCopy(ExpandConstant('{tmp}\appsettings.factory.json'), AppDir + '\appsettings.json', False);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Ignore errors so a first install does not fail when the service is absent. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    if WizardIsTaskSelected('resetdata') then
      WipeUserData();
  end;
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('opendashboard') then
    begin
      Sleep(2500);
      ShellExec('', '{#DashboardUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  DeleteUserDataOnUninstall :=
    MsgBox(CustomMessage('UninstallWipe'), mbConfirmation, MB_YESNO) = IDYES;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserDataOnUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    DelTree(AppDir + '\data', True, True, True);
    DeleteFile(AppDir + '\appsettings.json');
    DeleteFile(AppDir + '\.application_credentials');
  end;
end;
