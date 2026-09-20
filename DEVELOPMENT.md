# Development handoff

## Current implementation

The repository is now the release source for VHVN. Version 0.2.1 adds a normal Windows EXE installer, desktop/Start menu shortcuts, automatic first-run game setup, and automatic connection when the game opens. Legacy CMD launchers are retained only in the repository for older portable users; the installer does not include them.

The reader executable lives in bin; its root is the parent of that directory. portable.txt selects current-user AppData for settings, logs, state and backups, including in installed copies. Do not embed a developer path. Do not modify game archives, exe or saves. Game integration is a separately owned/hash-checked AfterInit2.tjs file, with a verified save backup before installation/update. Removing a modified/unowned bridge must remain a refusal.

source/src/PortableSetup.cs handles first-run detection, setup, cancellation and ownership-safe removal. App.cs runs setup once as needed, then waits for the game if necessary. RemoveInstalled is invoked only during actual uninstallation after confirmation; failure aborts the Windows uninstaller before it deletes the reader. Settings/saves/backups remain.

The game observer and speech behavior from 0.2 are unchanged. Speaker-name hover and friend Magnifier/screen-reader validation still need live feedback. No all-game/all-engine/all-build claim; the Fata executable fingerprint remains exact.

## Build and release

1. Set the same version in VERSION.txt and source/src/AssemblyInfo.cs; update RELEASE_NOTES.md.
2. Run source/build.ps1 with Windows PowerShell process-only execution-policy bypass. Windows Framework csc.exe, System.Speech and WinMetadata are needed. No SDK project is required. The original app icon is assets/VHVN.ico.
3. installer/Build-Release.ps1 -Iscc <ISCC.exe> rebuilds and copies the reader into bin, then creates artifacts/VHVN-Setup.exe and SHA256SUMS.txt. Use official Inno Setup6.7.3. Runtime files/source/notices are allowlisted in installer/VHVN.iss; no CMDs/private state.
4. Test before committing the rebuilt executable. Commit source and bin together. The CI workflow packages that tested binary (SkipReaderBuild); it does not rebuild the C# reader on the hosted runner.
5. Push the version change to main. .github/workflows/release.yml downloads a hash-pinned official compiler, builds the installer, and creates the release/tag with EXE+checksum. Job token is scoped to repository contents write. Existing versions are refused, not overwritten. Workflow can also run manually.
6. Check the Actions result and verify the release asset. README top links must remain the actual repository's /releases/latest and /releases/latest/download/VHVN-Setup.exe.

## Validation completed for 0.2.1

- Reader compilation and native --self-test and --ui-smoke passed; inspected rendered helper UI.
- Self-test needs a synthetic native-TJS fixture at work/engine-probe/bridge-test.txt. This is test data from the earlier development workspace, not game text; without it the checksum-interop assertion fails. Full native-engine fixtures are not distributed here.
- --portable-test <copied-game-exe> <isolated-workspace> passed: first launch needs setup, configured launch skips it, modified/removed integration requires attention; installation/update/removal and save/hash protections preserved.
- Installer built successfully. A silent installation in an isolated folder contained no CMDs, installed the exact tested reader and created a Start menu shortcut targeting it directly.
- Test uninstallation removed its files, shortcut and registration, without touching game files or saves. Test first verified no existing installed VHVN/current-user game integration. The /GROUP switch is ignored because the program-group page is disabled; expected group is VHVN.
- No changes to original game bridge text in this release. New first-run/automatic-wait behavior still needs a player's ordinary launch trial. Signing certificate not configured.

## Remaining work

Confirm release publishing, then get first-time install and launch feedback from the player. Further work includes full-game/load-restored/complex-layout coverage, assistive tools, reconnect/native-crash investigation, fallback transport and a second engine adapter. Do not silently change game input behavior or restore default Ctrl/Shift shortcuts.