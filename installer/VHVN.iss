#ifndef ReleaseVersion
  #define ReleaseVersion "0.2.1"
#endif
#ifndef ReleaseOutput
  #define ReleaseOutput "..\artifacts"
#endif

[Setup]
AppId={{3B514CFA-147E-47C1-857B-87C6F5D20FD1}
AppName=VHVN - Visual Helper for Visual Novels
AppVersion={#ReleaseVersion}
AppPublisher=BahaMert
AppPublisherURL=https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-
AppSupportURL=https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-/issues
AppUpdatesURL=https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-/releases
DefaultDirName={localappdata}\Programs\VHVN
DefaultGroupName=VHVN
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
OutputDir={#ReleaseOutput}
OutputBaseFilename=VHVN-Setup
SetupIconFile=..\assets\VHVN.ico
UninstallDisplayIcon={app}\bin\VisualNovelHelper.exe
WizardStyle=modern
WizardSizePercent=120
WizardResizable=yes
AppMutex=Local\VisualNovelHelper
CloseApplications=no
RestartApplications=no
Compression=lzma2
SolidCompression=yes
SetupLogging=yes

[LangOptions]
DialogFontSize=12

[Messages]
FinishedLabel=VHVN is installed. Open VHVN to finish first-time game setup, then start Fata Morgana through Steam. You can open VHVN later from your desktop or Start menu.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\bin\VisualNovelHelper.exe"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "..\bridge\AfterInit2.tjs"; DestDir: "{app}\bridge"; Flags: ignoreversion
Source: "..\profiles\*.json"; DestDir: "{app}\profiles"; Flags: ignoreversion
Source: "..\tools\Textractor-5.2.0\Textractor\x86\*.exe"; DestDir: "{app}\tools\Textractor-5.2.0\Textractor\x86"; Flags: ignoreversion
Source: "..\tools\Textractor-5.2.0\Textractor\x86\*.dll"; DestDir: "{app}\tools\Textractor-5.2.0\Textractor\x86"; Flags: ignoreversion
Source: "..\third-party\*"; DestDir: "{app}\third-party"; Flags: ignoreversion
Source: "..\source\src\*.cs"; DestDir: "{app}\source\src"; Flags: ignoreversion
Source: "..\source\build.ps1"; DestDir: "{app}\source"; Flags: ignoreversion
Source: "..\assets\VHVN.ico"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\portable.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VERSION.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\START HERE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD PARTY.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VHVN"; Filename: "{app}\bin\VisualNovelHelper.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\VHVN"; Filename: "{app}\bin\VisualNovelHelper.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\bin\VisualNovelHelper.exe"; Description: "Open &VHVN"; Flags: postinstall nowait skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    if not Exec(ExpandConstant('{app}\bin\VisualNovelHelper.exe'), '--remove-installed-bridge',
      ExpandConstant('{app}'), SW_SHOWNORMAL, ewWaitUntilTerminated, ExitCode) then begin
      MsgBox('VHVN could not check its game integration. Run the installer to repair VHVN, then uninstall again.', mbError, MB_OK);
      Abort;
    end;
    if ExitCode <> 0 then begin
      MsgBox('Uninstall stopped. Resolve the game integration message, then try again. VHVN and your saves have been kept.', mbError, MB_OK);
      Abort;
    end;
  end;
end;
