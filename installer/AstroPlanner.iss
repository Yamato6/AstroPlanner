; Instalador de AstroPlanner (Inno Setup 6).
;
; Lo genera el CI en cada tag v*:
;   iscc /DMyAppVersion=1.0.0 /DPublishDir=..\publish /DOutputDir=..\dist installer\AstroPlanner.iss
;
; Se instala para el usuario actual, sin pedir permisos de administrador, en
; %LocalAppData%\Programs\AstroPlanner. Los datos del usuario (configuración,
; plan, bitácora) viven en %LocalAppData%\AstroPlanner y no se tocan al
; desinstalar ni al actualizar.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define MyAppName "AstroPlanner"
#define MyAppPublisher "Yamato6"
#define MyAppURL "https://github.com/Yamato6/AstroPlanner"
#define MyAppExeName "AstroPlanner.exe"

[Setup]
; AppId fijo: las versiones futuras se instalan encima de esta y no se duplican.
AppId={{8C3E5D2A-6F41-4B7E-9A0C-2D7B1E4F6A93}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=© 2026 {#MyAppPublisher}
VersionInfoVersion={#Copy(MyAppVersion, 1, Pos("-", MyAppVersion + "-") - 1)}
VersionInfoProductName={#MyAppName}
VersionInfoDescription=Instalador de {#MyAppName}

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes

LicenseFile=..\LICENSE
SetupIconFile=..\AstroPlanner.UI\Assets\AstroPlanner.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
WizardSizePercent=110

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppName}_{#MyAppVersion}_Setup

; Si AstroPlanner está abierto, se ofrece cerrarlo para poder actualizar.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Planificador de observaciones astronómicas"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; Nada en [UninstallDelete]: la carpeta de datos del usuario se conserva a propósito.
