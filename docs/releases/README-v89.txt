ZXMAK2 v89 - Portable SD Folder Fix TEST

Based on preserved v88, including its shared ymfm YM2203 sound-card fixes.
Sound/CPU/video/keyboard implementations and native libraries are unchanged.

SD folder Save now supports long destination paths on .NET Framework 4.6.2+
(4.8 recommended) using per-EXE configuration, not Windows registry changes.
Large external folders are still copied into this emulator's Data/Saves on
first explicit Save; the external original is NOT overwritten. A modal progress
window keeps the interface responsive during copying/transaction. Please wait:
a large collection's first full copy genuinely needs time and free disk space.
Do not force-close while saving. Guest stays paused until save completes.

Refresh availability follows the live SD folder's resolved installation-relative
path and is updated in the toolbar. Save/discard/cancel, successful-refresh cold
start, close/eject prompts and missing-media behavior are retained. Save failure
keeps the session and resumes a previously running VM; it does not accept exit.

Keep the whole release folder together, including Lib, Config, Resources, Docs,
root boot.zip/ROMS.PAK and sidecars. External media/music/BC Commander are not
bundled. Source, verified archives, baseline-v88 and test logs are in the package.
Do not mix DLLs with earlier releases. Old v88 and user D: copies are untouched.
This build becomes the working base only after explicit user confirmation.
See V89_RELEASE_REPORT.md for exact coverage and limitations.
