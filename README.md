# VisualHelperForVisualNovel (VHVN)

A Windows read-aloud helper for **The House in Fata Morgana**, designed to make playing with low vision easier while keeping the game's normal controls.

Click to advance dialogue as usual. The helper speaks the current passage and lets you hear supported choices, menu controls, and backlog entries by hovering over them. It does not choose answers, advance the story, or save/load for you.

**Status: v0.2 preview.** Currently supports one verified English Steam build of the game. Other games, languages, and executable versions are not supported yet. Full-game coverage and use with players' screen readers and Magnifier setups still need testing.

## Requirements

- Windows 10 or 11, 64-bit.
- Your own installed English Steam copy of *The House in Fata Morgana* matching the supported build.
- Windows .NET Framework 4.x and an installed Windows speech voice.

Setup checks the game executable before installing. The helper is not tied to a particular username, drive letter, Steam library, or folder on the developer's PC.

## Download and install

1. On this repository's GitHub page, select **Code → Download ZIP** (or clone the repository).
2. Extract the entire ZIP to a folder you can keep, such as `Documents\VisualNovelHelper`. Keep all its files together; do not run it from inside the ZIP.
3. **Close the game**, then open **Set up game helper.cmd** in the extracted folder.
4. Check the detected game folder. If necessary, select **Browse** and choose the folder containing `fata.exe`. You can find it through Steam's **Manage → Browse local files**.
5. Select **Install / update**. Windows may request permission to write to the game's folder; only setup needs this permission.
6. Start the game through Steam, then open **Start reading helper.cmd**. Wait for **Connected**, and return to the game.

**Keep the helper folder separate from the game.** Leave the game wherever Steam installed it. Setup adds the integration file to the game's folder for you. You do not need developer tools, a PowerShell command, or a permanent script-policy change to use the helper.

After the first setup, just start the game and the helper whenever you want to play.

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

Configure voice, speed, and volume in the helper. Repeat and speech-toggle shortcuts are **unassigned by default**, since keys such as Ctrl and Shift are already used by visual novels.

To set a shortcut, open its editor and either:

- Type a combination, such as `F9`, `Alt+R`, or `Ctrl+Shift+F9`.
- Choose a key from the list.
- Select **Record keys** and press the combination directly.

Select **Save** to apply it or **Unassign** to remove it. Escape cancels key recording. Choose a combination that does not conflict with the game or your assistive software.

The helper uses larger native controls with accessible names and keyboard navigation. Use Tab and Shift+Tab to move between controls. **Read helper controls aloud** provides speech when controls receive focus or are hovered; turn it off if you prefer your own screen reader. Compatibility with every assistive-tool configuration is not yet verified.

## Moving, updating, and removing

You can move the extracted helper folder as long as you keep its contents together. Preferences, logs, and verified save backups are stored under:

```text
%APPDATA%\VisualNovelHelper
```

Run setup once on each new PC or Windows account.

To update, close the game and helper, extract the new version, and run its **Set up game helper.cmd**. Setup updates only an extension it recognizes and whose ownership hash matches.

To uninstall game integration, close the game and run **Remove game helper.cmd**. It removes only the helper's unchanged `AfterInit2.tjs` and ownership record. Saves and backups remain. You can then delete the extracted helper folder.

If setup finds an unknown or modified `AfterInit2.tjs`, it refuses to overwrite it. Remove the old integration using its own uninstall procedure first. This also applies to early development copies that used a different ownership manifest.

## Troubleshooting and limits

- **Game not detected:** use Browse to select the folder containing `fata.exe`.
- **Unsupported game version:** this profile checks the exact executable hash. Other builds and languages need verification and an appropriate adapter/profile.
- **Helper does not connect:** close other Textractor/capture sessions and old helper instances, restart the game, then reopen the helper under your normal Windows account.
- **An update seems inactive:** restart the game; the bridge loads at game startup.
- **No audio:** check the selected Windows voice, volume, and speech setting, then focus the game.
- **A control stays silent or a name is missing:** report the screen and the action that triggered it. Unusual layouts, restored text after loading, and complex passages still need broader testing.

This is a preview, not a claim of complete game accessibility. The application is not signed with a commercial publisher certificate. Automated checks cover narration policy, bridge behavior, shortcut entry, UI startup, and relocated-package installation/update/removal; those checks do not replace testing with the player.

## Privacy

Speech uses installed Windows voices; the helper does not send game text to a cloud speech service. Local logs may contain dialogue and spoilers. Share only relevant, reviewed excerpts when reporting a problem. This repository contains no game assets, saves, personal preferences, or play-session logs.

## For developers

The reader is C# WinForms using `System.Speech`. Game capture and narration policy are separate: the current KiriKiri/Fata bridge exports structured visible state, and shared code decides when to speak. Textractor is the fallback capture source. A second engine adapter has not yet been demonstrated.

| Path | Purpose |
| --- | --- |
| `bin/` | Ready-to-run helper executable |
| `source/src/` | C# reader, settings, setup, accessibility, and tests |
| `source/build.ps1` | Build using the Windows .NET Framework compiler |
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

Supported game executable SHA-256:

```text
8AE01E946B52ECDC38D02B3F394B70793C7A4628D7A46D204030311B9B447A93
```

## Third-party components

Textractor v5.2.0 is by Artikash and contributors and is licensed under GPL-3.0. Its matching source and license are included. See [THIRD PARTY.txt](THIRD%20PARTY.txt) and [the Textractor license](third-party/Textractor-LICENSE.txt) for component details. Microsoft runtime components retain their applicable terms. The helper's own source is included; this repository currently does not specify a separate license for that original code.

This is an independent accessibility project and is not affiliated with the game's developers or publishers.