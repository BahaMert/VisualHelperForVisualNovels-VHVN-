# Development handoff
## Latest changes — 0.2.3

Exact executable SHA256 is no longer an installation gate. GameCompatibility.Inspect accepts the reference exe as verified (existing fallback unchanged), or an x86 PE32 executable beside data.xp3/data_en.xp3 with expected XP3 headers as a bridge-only candidate. This is a heuristic, not proof of patch compatibility. The gate checks PE structure/machine/flags with bounded reads; no file-size match or archive hash pinning. Modified executables never start Textractor or use its fixed offsets. TextractorAdapter.BridgeOnly returns before CLI creation, polls game lifetime, and receives fresh validated bridge status from StructuredViewReader.HasLiveState. Connected is announced only after live transport; a 15-second missing-text status can recover when data later arrives. Stale/corrupt/old-process files cannot validate the connection. No game script/bridge changes were needed. Logs record the actual executable hash and fallbackEnabled=false for diagnostics.

CompatibilityTests changes only copied executables (overlay append, large-address-aware header flag, architecture corruption) and creates invented archive-header fixtures. Modified files are never executed by tests. Confirms relaxed install/removal, fallback classification, missing/invalid archives and wrong architecture rejection, and removal of owned integration after unsupported engine change. Self-tests cover delayed/missing/recovered bridge status and transport freshness. Friend's actual patch remains untested; request patch identity/logs if the live bridge does not respond. Never claim that any modified build is guaranteed to work.

Use version0.2.3 for the next release. This section supersedes older exact-fingerprint support claims below; exact hashes still protect extension ownership and approval, independently of executable compatibility.
## Latest changes — 0.2.2

- Dialogue now carries RememberForRepeat; only actual fallback dialogue or a semantic passage updates the repeat buffer. Structured hover, menus and backlog speech are transient. ViewDecision.IsDialogue marks passage speech; a new connection clears stale dialogue. Regression covers names/menus between passages, mute and repeat.
- Removed RegisterHotKey entirely from the reader. GameShortcuts installs a WH_KEYBOARD_LL callback on a dedicated native message-loop thread, checks the actual foreground game PID for each event, and forwards all keys outside it. Only configured game-focused combinations are consumed. No text is recorded/logged. Speech/UI work is posted back to the window, never performed in the callback. ShortcutPolicy covers exact modifiers, held keys, keyup and focus changes. Recording in settings suspends interception. See Microsoft LowLevelKeyboardProc documentation for callback time/foreground/message-loop constraints.
- Setup identifies known old helper bodies using profiles/legacy-bridges.json: exact normalized SHA256 of all code after a strictly validated vnhOutputPath declaration. Only that old per-PC output path and CRLF/LF differences are normalized; arbitrary code changes are not accepted. The six known body hashes were generated from preserved original bridge versions, including current portable/developer variants; no old private paths/scripts are distributed.
- Existing recognized bridge replacement requires a spoken, keyboard-accessible confirmation. Approval is the exact original file hash, passed through the elevation worker and rechecked before replacement. Old bridge and prior marker are backed up. Cancel/no approval leaves existing files untouched; unknown/modified unrelated extensions still refused. This supersedes older instructions to manually remove legacy developer bridges.
- Native full self-tests (repeat policy, structured classification, key focus policy and hook lifecycle), UI smoke and replacement-dialog cancellation passed. Inspected replacement-dialog screenshot. Isolated game-copy migration test passed recognized legacy adoption without metadata, confirmation required, stale approval refused, exact old-file backup, save preservation and unknown/modified protection. No real game input was synthesized and the live installation was not changed by these tests.

Release procedure remains below. Both assembly and VERSION.txt are now0.2.2. Friend/live checks after updating: hover a name then Repeat; Alt-Tab and type assigned keys; use a recognized older installation to check the spoken Replace/Cancel prompt. Existing same-version bridge need not be replaced merely because the reader changed.

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