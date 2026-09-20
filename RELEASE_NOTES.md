Download **VHVN-Setup.exe** below. Close VHVN and the game, run the installer, then open VHVN. No uninstall or manual folder cleanup is needed for recognized older helper installations.

New in 0.2.3:

- **Modified English Steam executables are no longer blocked just because their hash changed.** Setup checks for a 32-bit Windows executable and the expected English game archives.
- Compatible modified builds use the structured game-text bridge. VHVN reports Connected after receiving live bridge data and explains when game text is not responding.
- The version-specific fallback capture is used only for the verified reference executable. It is never attached to an unverified modified build.
- Owned game integration can still be removed if the game executable is subsequently changed to an unsupported engine.

Also includes the 0.2.2 fixes: Repeat remembers dialogue rather than hovered names, shortcut keys work normally outside the focused game, and recognized legacy helper installations can be replaced through a spoken confirmation with backups.

Compatibility mode is not a guarantee for every patch or engine replacement. Tested locally with synthetic executable/header changes; the friend's actual modified game still needs a live trial. Windows 10/11 x64 and installed Windows speech voices are required. The EXE is currently unsigned.

Players: choose **VHVN-Setup.exe**, not the Source code ZIPs. SHA256SUMS.txt contains the installer checksum.