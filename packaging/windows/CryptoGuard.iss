#ifndef PayloadDir
  #define PayloadDir "..\..\artifacts\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installer"
#endif
#define AppVersion "0.22.2-rc.1"
[Setup]
AppId={{2F744D37-A343-4D10-940B-894B1A58A744}
AppName=SentinelZone CryptoGuard
AppVersion={#AppVersion}
AppPublisher=SentinelZone
DefaultDirName={autopf}\SentinelZone\CryptoGuard
DisableDirPage=yes
DefaultGroupName=SentinelZone CryptoGuard
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19044
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=SentinelZone-CryptoGuard-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
UninstallDisplayName=SentinelZone CryptoGuard (data retained on uninstall)
InfoBeforeFile=INSTALL-NOTES.txt
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}\versions\{#AppVersion}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Manage-Service.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Start-IdleHelper.ps1"; DestDir: "{app}\versions\{#AppVersion}"; Flags: ignoreversion
[Icons]
Name: "{group}\CryptoGuard usage guide"; Filename: "{app}\versions\{#AppVersion}\README.md"
[Code]
var ServiceHealthy: Boolean;
function ServiceScript(Action, Binary: String): Boolean;
var Code: Integer; Params: String;
begin
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Manage-Service.ps1') + '" -Action ' + Action;
  if Binary <> '' then Params := Params + ' -BinaryPath "' + Binary + '"';
  Result := Exec(ExpandConstant('{sysnative}\WindowsPowerShell\v1.0\powershell.exe'), Params, ExpandConstant('{win}'), SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  Log('CryptoGuard service action ' + Action + ' returned ' + IntToStr(Code));
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  { Existing versions stay untouched until the new payload is complete. }
  if FileExists(ExpandConstant('{app}\versions\{#AppVersion}\CryptoGuardAgent.exe')) then
    Result := 'This version already exists. Uninstall it before reinstalling the same version; local data is retained. A different version can be upgraded directly.';
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ServiceHealthy := ServiceScript('Install',ExpandConstant('{app}\versions\{#AppVersion}\CryptoGuardAgent.exe'));
    if not ServiceHealthy then
      RaiseException('Service health check failed. Previous service path was restored when available. Inspect the installation before retrying.');
  end;
end;
function GetCustomSetupExitCode: Integer;
begin
  if ServiceHealthy then Result := 0 else Result := 10;
end;
function InitializeUninstall(): Boolean;
begin
  Result := ServiceScript('Uninstall','');
  if not Result then MsgBox('Service could not be stopped. Uninstall cancelled to preserve the running installation.',mbError,MB_OK);
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    if not UninstallSilent then MsgBox('Agent identity, configuration and local records remain in ProgramData\SentinelZone\CryptoGuard.',mbInformation,MB_OK);
end;
