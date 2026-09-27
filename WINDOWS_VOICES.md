# Windows natural voice integration

## Repository analysis

Base: `1482d06` (0.2.4). The application is a 64-bit .NET Framework WinForms executable built by `source/build.ps1`. `App.cs` owns the settings and game loop. `Core.cs` separates narration policy from speech via `ISpeechOutput`; `StructuredView.cs`, the game bridge, and Textractor provide text. Opening cards use local Windows OCR. Game integration and save protection are handled independently by `PortableSetup.cs`.

The original speech wrapper used `System.Speech`. Installing Aria for Windows Magnifier does not expose it through that interface or the normal WinRT `SpeechSynthesizer.AllVoices` list on this PC. The missing dependency was a SAPI adapter and compatible local model. After installation, both the original System.Speech API and native SAPI can discover Aria. An application code change alone cannot unlock the installed Magnifier package.

## Changes

- `WindowsSpeech.cs` uses native SAPI enumeration, full Windows descriptions (including Natural), and exact token identity for playback. COM resources are released on the owning UI thread.
- Saved `VoiceId` distinguishes same-name voices. Existing `Voice` preferences migrate by name. A missing voice does not erase the preferred ID; an available voice is used temporarily with a visible explanation.
- Speech is asynchronous and explicitly plain text. Cancellation purges queued speech. Completion and failures are polled without blocking the UI, and failures appear in Voice status and the log.
- The settings window includes Refresh voices and an accessible natural voice setup dialog. Build uses the Framework's Microsoft.CSharp assembly; no NuGet or external runtime dependency was added to VHVN.
- No game capture, bridge, shortcut behavior, saves, or installation ownership policy was changed.

## PC setup performed

The existing MicrosoftWindows.Voice.en-US.Aria.2 version **1.0.2.0** remains installed for Magnifier/Narrator.

The adapter and a separate **1.0.1.0** Aria model are installed under `C:\Program Files\NaturalVoiceSAPIAdapter`. Both x64 and x86 SAPI components are registered, making Aria available to compatible applications of either architecture. The model is extracted under `NarratorVoices\Aria`, rather than replacing or downgrading the Windows-installed package. Adapter settings disable Edge and Azure online voices for the current Windows account; those settings are per-user, while component registration is machine-wide.

The current user's VHVN preference selects the verified local Aria token. The adapter has an entry in Windows Installed apps pointing to its own uninstaller.

Adapter release 0.2.9 archive SHA-256 (matched GitHub's asset digest):

`7129d8675925e5a141addd820ce55b2ea4af0708c901e3a3e8e225e1ce15b4ce`

The compatible MSIX was linked by the adapter maintainer's wiki and passed Windows Authenticode verification with signer **Microsoft Windows / Microsoft Corporation**. Downloads and models are not committed or bundled in this repository. The adapter's installer manages removal; uninstall both components before deleting its folder. VHVN removal intentionally leaves this separately installed system component alone.

## Validation

- Framework x64 build: passed.
- Native x64 and x86 SAPI discovery: David, Hazel, Zira, and **Microsoft Aria (Natural) - English (United States)**.
- VHVN `--voice-test`: passed for all four voices, including real WAV output, literal angle-bracket text, native cancellation and replacement, preference migration, exact token selection, missing-token rejection, refresh, and repeated disposal.
- `--self-test-portable`: passed narration, parsing, transport, focus, opening OCR policy and shortcut lifecycle tests. The native TJS checksum interop fixture is explicitly skipped because it is not distributed by the upstream repository. The strict `--self-test` still requires it.
- `--ui-smoke`: passed using isolated settings. No real game was launched or modified. Live play and Magnifier coexistence during gameplay have not been tested.

## References

- [Adapter documentation](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter)
- [Compatible voice packages](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter/wiki/Narrator-natural-voice-download-links)
- [Adapter settings](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter/wiki/Configurable-registry-values)
- [Microsoft SAPI SpVoice](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms723602(v=vs.85))
- [Microsoft speech flags](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee125223(v=vs.85))
