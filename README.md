# VisualHelperForVisualNovel (VHVN)

**[Download VHVN for Windows (.exe)](https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-/releases/latest/download/VHVN-Setup.exe)** · [All releases](https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-/releases)

Download **VHVN-Setup.exe**, run it, and open **VHVN** from your desktop or Start menu. No command files, ZIP extraction, or developer tools are needed. The GitHub Source code ZIPs are for developers.

A Windows read-aloud helper for **The House in Fata Morgana**, designed to make playing with low vision easier while keeping the game's normal controls.

Click to advance dialogue as usual. The helper speaks the current passage and lets you hear supported choices, menu controls, and backlog entries by hovering over them. It does not choose answers, advance the story, or save/load for you.

**Status: v0.2.3 preview.** Designed for the English Steam game. Modified 32-bit executables can use compatibility mode when the expected English game data is present. Other games, languages, and replacement engines are not generally supported. Full-game coverage and use with players' screen readers and Magnifier setups still need testing.

## Requirements

- Windows 10 or 11, 64-bit.
- Your own installed English Steam copy of *The House in Fata Morgana*.
- Windows .NET Framework 4.x and an installed Windows speech voice.

Setup checks the executable format and English game data. A changed executable hash alone no longer blocks installation. The helper is not tied to a particular username, drive letter, Steam library, or folder on the developer's PC.

## Download and install

1. **[Download VHVN-Setup.exe](https://github.com/BahaMert/VisualHelperForVisualNovels-VHVN-/releases/latest/download/VHVN-Setup.exe)** and run it.
2. Follow the installer. It creates a Start menu entry and, by default, a desktop shortcut.
3. Open **VHVN**. On the first launch, close the game and check the automatically detected game folder. If needed, use **Browse** to select the folder containing `fata.exe`.
4. Select **Install / update** once. Windows may request permission to add the integration file to the game's folder; the reader runs under your normal account.
5. Start the game through Steam. VHVN connects automatically when it opens.

After that, just open **VHVN** alongside the game and play normally. You may open either one first. If the game folder changes or the bridge needs an update, VHVN shows game setup again.

Leave the game wherever Steam installed it. The installer handles the helper's files separately; you do not need to move anything into the game folder yourself.

## Playing

| Action | What the helper does |
| --- | --- |
| Advance dialogue normally | Reads the current passage. Supported lines begin speaking during the text animation; more complex lines may wait until rendering finishes. |
| Advance quickly or skip | Interrupts older speech as the current passage changes. |
| Hover a choice or supported menu item | Reads its label. |
| Hover a backlog entry | Reads the entry. |
| Hover the printed speaker name briefly | Reads the name on demand. Names are not automatically added to every sentence. Leave and hover again to repeat. |
| Open a supported confirmation dialog | Announces its question and available answers. |
| Hover a save/load slot | Reads available slot details, with “Most recent save” first when applicable. |

Supported controls include the bottom toolbar, settings controls, and save/load panels. Some image-only controls use labels verified for this game; arbitrary image-based menus are not automatically understood.

Leaving the game stops gameplay speech. Returning does not automatically replay old dialogue. Minimize the helper to keep it in the notification area; reopen its settings or choose **Exit** there.

## Voice and shortcuts

Configure voice, speed, and volume in the helper. Repeat and speech-toggle shortcuts are **unassigned by default**, since keys such as Ctrl and Shift are already used by visual novels. **Repeat always rereads the latest dialogue passage**, even after a speaker name, menu item, or backlog entry was spoken. Speaker names are read only by hovering over them.

To set a shortcut, open its editor and either:

- Type a combination, such as `F9`, `Alt+R`, or `Ctrl+Shift+F9`.
- Choose a key from the list.
- Select **Record keys** and press the combination directly.

Select **Save** to apply it or **Unassign** to remove it. Escape cancels key recording. Shortcuts act only while the game has focus. In other apps, the same keys work normally, so you can type messages or email without closing VHVN. Choose a combination that does not conflict with the game or your assistive software.

The helper uses larger native controls with accessible names and keyboard navigation. Use Tab and Shift+Tab to move between controls. **Read helper controls aloud** provides speech when controls receive focus or are hovered; turn it off if you prefer your own screen reader. Compatibility with every assistive-tool configuration is not yet verified.

### Windows natural voices

VHVN supports compatible natural voices exposed through Windows SAPI. Open **Natural voices / setup** in the helper for instructions. Use **Refresh voices**, select the desired voice, then **Test voice**. Voice choices are saved by token ID; older name-only preferences are migrated. If a saved voice is missing, the helper uses an available voice temporarily and retains the saved choice until you select a replacement.

Aria appearing in Magnifier or Narrator does not mean other speech applications can access it. To use an offline natural voice in VHVN and other SAPI applications:

1. Download [NaturalVoiceSAPIAdapter](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter) and extract it to a permanent folder. It is an optional third-party component, not included in VHVN.
2. Follow the adapter's [compatible voice package instructions](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter/wiki/Narrator-natural-voice-download-links). Its current guidance requires an older package extracted into a separate voice folder; newer Store packages are incompatible. Keep your existing Magnifier/Narrator voice installed. Set the adapter's **Local voice path** to the extracted voice folder, or use its default `NarratorVoices` folder beside `Installer.exe`.
3. Run the adapter's `Installer.exe` and install its **64-bit** component for VHVN. Install **32-bit** as well for other 32-bit SAPI apps. Registration requires administrator permission. This makes the voice available to compatible SAPI apps; it does not add it to every application's private speech engine.
4. Disable **Microsoft Edge online voices** and **Azure online voices** in the adapter for offline reading. Close its settings and refresh voices in VHVN. The adapter briefly caches discovery results; restart VHVN if a newly installed voice does not appear.

Validated locally with NaturalVoiceSAPIAdapter 0.2.9 and the Microsoft-signed Aria 2 package version 1.0.1.0, alongside the newer 1.0.2.0 Magnifier package. This is third-party compatibility support and may change with future Windows or adapter updates. Use the adapter's installer to uninstall its components before moving or deleting its files. VHVN's installer/uninstaller does not manage the optional adapter or voice models.

### Skip and Magnifier

**Free Ctrl for Magnifier** is enabled by default. Hold **Caps Lock** to skip and release it to stop. Caps Lock does not toggle capitalization while it is the active game skip key. Choose **Hold-to-skip key** to type, select or record a different key; **Unassigned** disables the replacement key while keeping Ctrl free.

If you prefer the game's normal **Ctrl-to-skip**, turn **Free Ctrl for Magnifier** off. This choice is saved. Ctrl and Alt continue to reach Windows/Magnifier, and keys behave normally outside the game. Remapping applies while VHVN is running, including when speech is muted; closing VHVN restores the original game behavior. The game integration must be updated and the game restarted for this feature.

### Earlier dialogue and the opening

Supported dialogue starts reading as soon as the game provides a safely bounded passage; visual character animation and the known pause macro do not need to finish first. Scene transitions, unsupported script commands and the selected Windows voice can still introduce latency.

**Read opening image cards (Windows OCR)** reads text embedded in the scripted opening's images at their transitions. It uses local Windows OCR on the game's card layer, not desktop screenshots or a remote service. Disable this option if you prefer the opening without narration. An installed Windows OCR language is required. OCR may make mistakes with unusual lettering; this feature is specific to this opening, not arbitrary movie files. Late recognition results are discarded when the scene changes or the game loses focus.

## Updating and removing

Download and run the latest installer to update VHVN. Close the helper before updating. On the next launch, it checks whether the game's integration also needs updating.

To uninstall, close the game and helper, then remove **VHVN - Visual Helper for Visual Novels** through Windows **Installed apps / Apps & features**. Uninstall removes only its verified game extension and installed application files. If the extension was changed or game integration cannot be removed, uninstall stops and explains the issue.

Your saves, preferences, logs, and verified backups are kept. Helper data lives in:

```text
%APPDATA%\VisualNovelHelper
```

On another PC or Windows account, run the installer and complete first-time game setup again. There are no paths tied to the developer's PC.

When setup finds a recognized earlier VHVN integration, including supported early development copies without an ownership record, it asks **Replace helper** or **Cancel**. The prompt can be spoken and operated by keyboard. Replacement backs up the previous extension and preserves saves and settings; cancellation leaves the old installation untouched. You do not need to find or delete files. An unrelated or unrecognized modified extension is left unchanged to protect other mods.

## Troubleshooting and limits

- **Game not detected:** use Browse to select the folder containing `fata.exe`.
- **Modified executable:** VHVN automatically uses compatibility mode with a supported 32-bit executable and the expected `data.xp3` and `data_en.xp3` archives. In this mode it waits for live structured text from the game; fixed-address fallback capture stays disabled.
- **Game text is not responding:** restart the game after setup. If the message persists, the modification may change the engine or scripts in a way that needs adapter work. Share the patch/tool name and relevant helper logs; do not undo a working compatibility patch just to satisfy a hash check.
- **Helper does not connect:** close other Textractor/capture sessions and old helper instances, restart the game, then reopen the helper under your normal Windows account.
- **An update seems inactive:** restart the game; the bridge loads at game startup.
- **No audio:** check the selected Windows voice, volume, and speech setting, then focus the game.
- **A control stays silent or a name is missing:** report the screen and the action that triggered it. Unusual layouts, restored text after loading, and complex passages still need broader testing.

This is a preview, not a claim of complete game accessibility. The application and installer are currently unsigned. Automated checks cover narration policy, bridge behavior, shortcut entry, UI startup, and relocated-package installation/update/removal; those checks do not replace testing with the player.

## Privacy

Speech is passed to the selected Windows SAPI voice. Desktop voices and local natural voice models work offline. Third-party adapters can also expose online voices, which send the spoken text to their provider; disable online voices in the adapter when you want local-only reading. Local logs may contain dialogue and spoilers. Share only relevant, reviewed excerpts when reporting a problem. This repository contains no game assets, saves, personal preferences, or play-session logs.

## For developers

The reader is C# WinForms using native Windows SAPI automation (`SAPI.SpVoice`). Game capture and narration policy are separate: the current KiriKiri/Fata bridge exports structured visible state, and shared code decides when to speak. Textractor is the fallback capture source. A second engine adapter has not yet been demonstrated.

| Path | Purpose |
| --- | --- |
| `bin/` | Ready-to-run helper executable |
| `source/src/` | C# reader, settings, setup, accessibility, and tests |
| `source/build.ps1` | Build using the Windows .NET Framework compiler |
| `installer/` | Windows installer definition and release build script |
| `.github/workflows/release.yml` | Publish an EXE release when the version changes on main |
| `bridge/AfterInit2.tjs` | Generated original game integration code |
| `profiles/` | Verified game identity and control-label rules |
| `tools/` | Bundled Textractor command-line capture and dependencies |
| `third-party/` | Matching Textractor source archive and license |

To rebuild the reader on a compatible Windows development machine, run from the repository root:

```powershell
New-Item -ItemType Directory -Force .\source\bin | Out-Null
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\source\build.ps1
```

This allows scripts only in the launched PowerShell process. It outputs `source\bin\VisualNovelHelper.exe`. With the helper closed, copy that executable to the repository's `bin` folder to run it with the bundled profiles and tools. The generated bridge is shipped separately; this build step does not regenerate it. Some engine integration fixtures belong to the development workspace and are not included in this portable distribution.

Speech validation: run `bin\VisualNovelHelper.exe --voice-test` to check voice identity migration, discovery, refresh, WAV generation, and cancellation/replacement with each installed SAPI voice. Test text is synthetic, and audio is written to `tests/`; online voices, if enabled, still contact their provider. `--ui-smoke` captures the settings and voice setup windows. `--self-test-portable` runs the shared policy suite and explicitly reports the unavailable native TJS fixture as skipped; the original `--self-test` remains strict and requires that fixture. See [WINDOWS_VOICES.md](WINDOWS_VOICES.md) for the analysis and validation record.


To build the EXE installer, use [Inno Setup 6.7.3](https://jrsoftware.org/isdl.php) and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Release.ps1 -Iscc "C:\path\to\ISCC.exe"
```

This rebuilds the reader and outputs `artifacts\VHVN-Setup.exe` plus `SHA256SUMS.txt`. No scripts are needed by the player.

For a release, update `VERSION.txt`, `source/src/AssemblyInfo.cs`, and the release notes, build/test the reader, and commit the updated `bin/VisualNovelHelper.exe` with its source. Pushing the version change to `main` runs the release workflow. It packages that tested binary using a checksum-pinned installer compiler and publishes the installer and checksum in GitHub Releases. An existing version is never overwritten. The workflow can also be started manually.
Reference executable SHA-256 (enables the previously verified fallback capture; other compatible executables use the structured bridge only):

```text
8AE01E946B52ECDC38D02B3F394B70793C7A4628D7A46D204030311B9B447A93
```

## Third-party components

Textractor v5.2.0 is by Artikash and contributors and is licensed under GPL-3.0. Its matching source and license are included. See [THIRD PARTY.txt](THIRD%20PARTY.txt) and [the Textractor license](third-party/Textractor-LICENSE.txt) for component details. Microsoft runtime components retain their applicable terms. The helper's own source is included; this repository currently does not specify a separate license for that original code.

This is an independent accessibility project and is not affiliated with the game's developers or publishers.
