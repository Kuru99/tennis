#ifndef AppVersion
  #define AppVersion "0.1.1-beta"
#endif
#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef RepoDir
  #error RepoDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif

[Setup]
AppId={{6E9C0C1E-7E90-4A44-9A45-0E9DDC2E7E51}
AppName=PRIDE COURT
AppVersion={#AppVersion}
AppVerName=PRIDE COURT {#AppVersion}
AppPublisher=Pride Court Studio
AppPublisherURL=https://github.com/Kuru99/tennis
AppSupportURL=https://github.com/Kuru99/tennis/issues
AppUpdatesURL=https://github.com/Kuru99/tennis/releases
DefaultDirName={localappdata}\Programs\PrideCourt
DefaultGroupName=PRIDE COURT
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=PrideCourt-Windows-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\プライド・コート.exe
UninstallDisplayName=PRIDE COURT {#AppVersion}
VersionInfoVersion=0.1.1.0
VersionInfoCompany=Pride Court Studio
VersionInfoDescription=PRIDE COURT Windows Beta Installer
VersionInfoProductName=PRIDE COURT
VersionInfoProductVersion=0.1.1.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成"; GroupDescription: "追加アイコン"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*_BurstDebugInformation_DoNotShip\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoDir}\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "{#RepoDir}\Assets\_Project\Docs\PROTOTYPE_CONTROLS.md"; DestDir: "{app}"; DestName: "PROTOTYPE_CONTROLS.md"; Flags: ignoreversion
Source: "{#RepoDir}\Assets\_Project\Docs\THIRD_PARTY_ASSETS.md"; DestDir: "{app}"; DestName: "THIRD_PARTY_ASSETS.md"; Flags: ignoreversion

[Icons]
Name: "{group}\PRIDE COURT"; Filename: "{app}\プライド・コート.exe"; WorkingDir: "{app}"; Comment: "PRIDE COURT"
Name: "{autodesktop}\PRIDE COURT"; Filename: "{app}\プライド・コート.exe"; WorkingDir: "{app}"; Comment: "PRIDE COURT"; Tasks: desktopicon

[Run]
Filename: "{app}\プライド・コート.exe"; Description: "PRIDE COURTを起動"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
