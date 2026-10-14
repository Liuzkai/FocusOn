#ifndef SourceDir
  #error SourceDir is required
#endif
[Setup]
AppId={{1F5736B5-76A4-474A-BA25-4AC531D99D47}
AppName=FocusOn
AppVersion={#AppVersion}
AppPublisher=Liuzkai
AppPublisherURL=https://github.com/Liuzkai/FocusOn
AppSupportURL=https://github.com/Liuzkai/FocusOn/issues
AppUpdatesURL=https://github.com/Liuzkai/FocusOn/releases
DefaultDirName={localappdata}\Programs\FocusOn
DefaultGroupName=FocusOn
PrivilegesRequired=lowest
MinVersion=10.0.17763
ArchitecturesAllowed={#Architecture}
ArchitecturesInstallIn64BitMode={#Architecture}
OutputDir={#OutputDir}
OutputBaseFilename=FocusOn-{#Runtime}-Setup
SetupIconFile=..\..\src\FocusOn\Assets\focuson.ico
UninstallDisplayIcon={app}\FocusOn.exe
LicenseFile=..\..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=FocusOn.exe
RestartApplications=no
DisableProgramGroupPage=yes
Uninstallable=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\FocusOn"; Filename: "{app}\FocusOn.exe"; IconFilename: "{app}\FocusOn.exe"
Name: "{autodesktop}\FocusOn"; Filename: "{app}\FocusOn.exe"; IconFilename: "{app}\FocusOn.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\FocusOn.exe"; Description: "Open FocusOn"; Flags: nowait postinstall skipifsilent
[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var StartupCommand: String;
begin
  if CurUninstallStep = usPostUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FocusOn', StartupCommand) then
      if Pos('"' + ExpandConstant('{app}\FocusOn.exe') + '"', StartupCommand) = 1 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FocusOn');
end;
