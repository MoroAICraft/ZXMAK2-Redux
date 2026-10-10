ZXMAK2 v87 Portable TEST — 2026-10-10

TEST ONLY. User confirmation is required before this becomes the next work base.
Based on v86, not the cancelled heavy v83 audio experiment.
Copy the entire release folder. Run ZXMAK2.exe; keep all subfolders.

Root remains: boot.zip, ROMS.PAK and ZXMAK2.vmz/vmide/cmos/nvram.
Lib/Managed and Lib/Native contain runtime dependencies.
Config contains host settings/templates; Resources contains separate ROMs/banks.
Docs/Help/index.html is the local help opened through Help.
Data contains owned state, logs, temporary files and local saved media.

External media is never overwritten. External SD folder Save creates a local
folder copy and switches to it; external SD image Save creates a raw IMG copy.
External HDD uses a local full copy on first actual guest write, not on mount.
Large copies require free space and can take time.
Missing configured optional media means an empty slot. Internal paths are relative
to EXE root, independent of drive letter, root name and working directory.

Refresh save/discard/cancel and close/eject prompts remain required.
After an unclean shutdown, current source contents are used; stale journals are
not applied automatically. Unsaved changes can be lost after a crash.

F12 reset; Ctrl+F12 CMOS reset; Ctrl+Alt+F12 factory reset (confirmation).
Print Screen = VM NMI. Guest/AVR/debugger keys are explained separately in Help.
No hardware ports, timing, CPU, renderer or accepted audio algorithm changed.

Windows/.NET, legacy DirectX and x86 native prerequisites still apply.
Windows/third-party traces are outside emulator control. Portable storage requires
a writable install folder; no silent AppData/TEMP fallback.
See V87_RELEASE_REPORT.md outside runtime for checks, limitations and rollback.
