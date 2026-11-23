; Installateur d'OK Régis (Inno Setup 6).
; Construit par installer\build-installer.ps1, qui prépare le dossier "staging" (appli + modèles vocaux FR/EN/ES).
;
; Installe pour l'utilisateur courant : aucun droit administrateur nécessaire.

#ifndef AppVersion
  #define AppVersion "1.0.2"
#endif
#define AppName "OK Régis"
#define AppExe "OkRegis.App.exe"

[Setup]
AppId={{6F1C4C58-3B0E-4D7B-9F2A-0B1D5E7A9C42}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SamSamDEV
AppPublisherURL=https://github.com/SamSamDEV/OK-Regis
AppSupportURL=https://github.com/SamSamDEV/OK-Regis/issues
DefaultDirName={autopf}\OK Regis
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=OKRegis-Setup-{#AppVersion}
SetupIconFile=..\src\OkRegis.App\Assets\okregis.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
; ferme l'appli si elle tourne pendant l'installation / la mise à jour
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; l'appli (autonome : aucun .NET à installer)
Source: "staging\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; les modèles vocaux des trois langues : rien à télécharger au premier lancement
Source: "staging\models\*"; DestDir: "{app}\models"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; à la désinstallation, on retire aussi le démarrage automatique créé par l'appli
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "OK-Regis"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
