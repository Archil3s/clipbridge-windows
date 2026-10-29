#define AppName "ClipBridge"
#define AppVersion "0.6.0"
#define AppPublisher "ClipBridge Project"
#define AppExeName "ClipBridge.exe"

[Setup]
AppId={{7C7A539D-B6C8-496F-8A80-EB445F1874E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\ClipBridge
DefaultGroupName=ClipBridge
OutputDir=..\installer-output
OutputBaseFilename=ClipBridge-Setup
Compression=zip
SolidCompression=no
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
DisableProgramGroupPage=yes

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ClipBridge"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\ClipBridge"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch ClipBridge"; Flags: nowait postinstall skipifsilent
