; Inno Setup Script for XTimelineViewer Kotsume Edition
#define MyAppName "XTimelineViewer Kotsume Edition"
#ifndef MyAppVersion
#define MyAppVersion "2.4.0"
#endif
#define MyAppPublisher "Kotsume Project"
#define MyAppURL "https://github.com/kotao-boop/xtimelineviewer-kotsume"
#define MyAppExeName "XTimelineViewer.exe"
#ifndef SourceDir
#define SourceDir "..\publish\x64"
#endif

[Setup]
AppId={{D6F9E134-8A8C-4C9D-9F0A-3C2B1D8E7F6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=XTimelineViewer-Kotsume-v{#MyAppVersion}-Setup
OutputDir=..\dist
SetupIconFile=..\Assets\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
; Windows App SDKの再配布条件に従い、利用者へMicrosoftの条項を表示して同意を得る。
; プロジェクト本体のMIT Licenseと全NOTICEはインストール先にも含まれる。
LicenseFile={#SourceDir}\licenses\Microsoft-WindowsAppSDK-LICENSE.txt
InfoBeforeFile=..\PRIVACY.md
InfoAfterFile=..\THIRD-PARTY-NOTICES.md

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
