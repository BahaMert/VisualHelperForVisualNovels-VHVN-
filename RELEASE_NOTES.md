Download **VHVN-Setup.exe** below. Close VHVN and the game, run the installer, then open VHVN and approve the game-integration update. Restart the game to load the new bridge. No manual file deletion is needed.

New in 0.2.4:

- **Ctrl or a configurable hold-to-skip key:** Free Ctrl for Magnifier is on by default and uses Caps Lock. Hold to skip and release to stop. Choose another key by typing, selecting or recording it. Turn Free Ctrl for Magnifier off to restore the game's original Ctrl skip. Ctrl/Alt are never globally blocked, and assigned keys work normally outside the game. Caps Lock does not change capitalization while acting as the skip key.
- **Earlier dialogue:** supported passages publish as soon as the first character reaches the renderer; the known in-sentence pause macro no longer forces a wait for the full visual sentence. Removed an extra speech-cancel call and reduced reader polling latency. Scene transitions, unsupported script commands and Windows voice startup can still add delay; zero-delay narration is not promised.
- **Opening image-card reading:** Windows OCR reads the opening's image cards when their transitions begin. It uses the game card layer, without desktop capture or a cloud service. Read opening image cards can be disabled in settings. This is specific to Fata's scripted opening, not a general movie/subtitle reader. OCR can misread unusual text, and requires a Windows OCR language.

Includes the earlier modified-executable compatibility, dialogue-only Repeat, game-focused shortcuts and accessible upgrade confirmation fixes.

Validation: native engine fixtures including the actual CtrlSkip plugin, remap/restore/load/lease-expiry behavior, dialogue and opening transitions; reader regression and UI checks; local opening-card OCR and isolated install/update/removal. Live testing with the player's Magnifier setup is still needed.