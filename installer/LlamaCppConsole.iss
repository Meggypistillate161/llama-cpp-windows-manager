#define AppName "llama.cpp Console"
#define AppExeName "LlamaCppConsole.exe"
#ifndef AppVersion
#define AppVersion "1.1.0"
#endif
#ifndef SourceDir
#define SourceDir "..\dist\LlamaCppConsole-win-x64"
#endif
#ifndef OutputDir
#define OutputDir "..\dist\installer"
#endif

[Setup]
AppId={{5C6D440C-0EE0-4FEC-8D86-6AADEAA24620}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=LocalLlmConsole contributors
AppPublisherURL=https://github.com/alekk89/llama.cpp-Console
AppSupportURL=https://github.com/alekk89/llama.cpp-Console/issues
AppUpdatesURL=https://github.com/alekk89/llama.cpp-Console/releases
DefaultDirName={code:GetDefaultDirName}
DefaultGroupName={#AppName}
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=LlamaCppConsole-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\LocalLlmConsole.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
AppMutex=Local\llama.cpp-console-single-instance
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#AppVersion}
VersionInfoCompany=LocalLlmConsole contributors
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeleteAppDataOnUninstall: Boolean;

function GetDefaultDirName(Param: string): string;
begin
  if DirExists('D:\') then
    Result := 'D:\LlamaCppConsole'
  else
    Result := ExpandConstant('{localappdata}\Programs\LlamaCppConsole');
end;

function InitializeUninstall(): Boolean;
var
  DataDir: string;
begin
  Result := True;
  DeleteAppDataOnUninstall := False;
  DataDir := ExpandConstant('{app}\data');

  if DirExists(DataDir) then
  begin
    DeleteAppDataOnUninstall :=
      MsgBox(
        'Uninstall llama.cpp Console?' + #13#10 + #13#10 +
        'Your models, runtimes, logs, cache, and settings in:' + #13#10 +
        DataDir + #13#10 + #13#10 +
        'will be kept by default. Delete this data too?',
        mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteAppDataOnUninstall then
    DelTree(ExpandConstant('{app}\data'), True, True, True);
end;
