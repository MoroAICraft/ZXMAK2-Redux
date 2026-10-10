# v87 Portable — TEST ONLY, 2026-10-10

Package: `L:\Work_two\ZX\ZXMAK2-v87-Portable-Test-20261010`.
Source: `L:\Work_two\ZX\ZXMAK2-Fork`, branch `codex/portable-v87`.
Baseline: c57bf2b6744be4f00c75908ed4cce9311a7bb8b7, published v86.
Previous accepted control remains v84; v85/v86 and all user D: installs retained.
**Only the user's explicit confirmation can make v87 the next working base.**

## Runtime layout

Root: ZXMAK2.exe/config, Test.exe/config, README-v87.txt, boot.zip, ROMS.PAK,
ZXMAK2.vmz/.vmide/.cmos (and .nvram when created). Runtime is 73 files.
Clean archive is 70 files: removes only the three present active state files,
not native sound, Yamaha instruments in ROMS.PAK, SoundFont or Test.exe.
No SD collection or BC Commander is bundled; Full retains its old descriptor
of an external BC folder but does not include/copy that folder.

- Lib/Managed: managed runtime, plugin, debugger and XNA DLLs retained.
- Lib/Native: unchanged ymfm_opl4, omni_opn, omni_midi and SAASound binaries.
- Config: templates, Unity/plugin/log config, keyboard map; interface.xml and
  joystick/debugger preferences are created here during use.
- Resources/ROMs: separate overrides, user-removable; ROMS.PAK stays root.
- Resources/SoundBanks: unchanged GeneralUser-GS.sf2.
- Docs: updated hotkeys, joystick guide, licences, historical CHM and original
  debugger/assembler HTML; new portable Help/index.html is the primary help.
- Data: State/MoonSound, Sessions/SD, Saves, Recordings, Logs, Backups, Cache,
  Temp. Published runtime contains NO diagnostic data or probes.

## Host changes, not a new hardware implementation

PortablePaths derives root from the entry EXE, never the moved Engine DLL or
process CWD. Internal references are serialized relative to root; paths through
an internal junction are not treated as safe owned write targets. A recursive
copy into its own source is rejected. Save files use a same-volume temporary
file and atomic replacement with backups under Data/Backups.

EXE probing loads managed libraries from Lib/Managed. The small EXE bootstrap
sets the native library directory before dependent code is JIT-compiled. It does
not change CWD or Windows settings. An exclusive local instance lock prevents
two user EXEs from sharing one writable copy; Factory Reset releases it before
restart. Missing startup dependencies fail visibly rather than using another
installation. Separate writable copies can be used independently.

UI preferences now use Config/interface.xml. A first-run read-only import of
the old registry values is allowed; no registry key is created/written. Failure
to read that optional key falls back to defaults. Logging now targets Data/Logs,
not C:\Logs. Host window size is constrained to current screen work area without
changing guest ULA mode or overwriting a valid preferred size during initial fit.

MoonSound instrument selection is Resources/ROMs, compatible root/roms files,
then ROMS.PAK. Invalid standalone instrument size is not silently substituted.
Writable flash is Data/State/MoonSound/<factory-ROM SHA256>.bin, independent of
install name, drive and username. Exact matching legacy install/ROM flash may be
copied read-only into local state; arbitrary old install files are not guessed.
If migrating a different former D: installation, copy its correct flash state
explicitly; do not assume it was automatically found in AppData.

CPU, hardware port equations, timing, audio DSP and keyboard/NMI routing are
unchanged. Existing cancelled v83 FIR is not included. Devices retain their root
sidecars; this is not a new independent per-model profile manager. Existing CMOS
formats/sharing semantics were not redesigned.

## Media policy and lifecycle

External sources are read only by the host; guest writes use owned state:

- External SD folder: on explicit Save, copy its CURRENT PC tree into a unique
  Data/Saves/SD-Folders directory, then selectively apply guest changes using
  the existing checked transaction. Independent PC edits survive. A same-file
  conflict stops saving. Failed remount retains the original session. Success
  switches the descriptor to the local folder. Later PC edits/Refresh therefore
  apply to that local copy, not the old external original. Nothing is fully
  copied on initial mount. A failed operation can leave an unused local copy;
  its path is logged, original/session retained.
- External SD image/VHD: Save materializes logical sectors to an independent
  local RAW .img, including guest changes, then switches descriptor. Source is
  not modified. Internal images keep their existing commit behavior.
- External floppy: accepted Save writes a local image and updates its path.
  Atomic-save failure restores the previous source and dirty flag.
- External HDD: first actual guest write creates a full local image, preserving
  sector position/geometry; subsequent writes target it. No full copy on mount.
  This initial copy can pause the host and needs disk space; no claim of free
  instantaneous multi-GB copy. Final paths are persisted on normal close.
- External VMZ import and CMOS/VMIDE/NVRAM sidecars are copied locally. Relative
  external references are resolved at import time; originals stay untouched.

Missing/inaccessible optional media is an empty slot, not a fabricated blank
image or factory reset. Saved descriptors are retained where supported so a
later run can find the source. Physical CD/DVD fallback is existing behavior;
physical joystick bindings/profiles are retained, no random controller selected.

Refresh keeps its save/discard/cancel choices, selective merge and successful
cold-start behavior. PC-only refresh asks no unnecessary question. Close/eject/
model change handles BOTH Z-controller and NeoGS SD sessions. Save destinations
are inside the install; Save As dialogs explain an outside choice rather than
silently writing somewhere else.

Folder crash policy remains a fresh scan, no old folder journal applied.
Normal SD image recovery is still a separate confirmation-based workflow;
internal image identities are relative and survive moving the whole folder.
This distinction preserves existing image recovery, not a promise to recover
discarded/unsaved folder data after a crash.

F12, Ctrl+F12, Ctrl+Alt+F12, PrintScreen/NMI, boot.zip Quick Load are retained.
Factory Reset deletes the active four ROOT state files, not Config/Data/media/
ROM/flash. The help distinguishes general, AVR, guest and debugger contexts.
Obsolete assembler DLL auto-delete/auto-download on load failure is disabled;
a compatible optional Pasmo2 DLL can be supplied manually in Lib/Native.

## Completed checks

- Full MSBuild solution, Release/Any CPU; user EXEs marked x86. Only pre-existing
  missing rule-set warnings. Developer output copies Config and local help;
  PrepareRuntime is the reproducible structured packaging step.
- Final build tested in a separate diagnostics/final-validation copy, not the
  published runtime. 64 portable checks: root vs DLL/CWD, write confinement,
  instance lock, external folder copy/merge/conflict/rollback, raw SD/HDD copy,
  missing media, settings reload, NeoGS save/discard/cancel, reset paths, VMZ/
  sidecar import, floppy save and injected locked-target rollback.
- 78 basic checks: all 22 model templates load ROM/video/audio frames and four
  additional Evo/Spectrum switches. This is a smoke test, not full conformance.
- Physically copied runtime in a renamed Unicode/spaces/# directory, foreign
  CWD; 15 root/settings/ROM/template/missing-media checks. Test.exe MoonSound
  and MIDI synth/bank pass without a developer native PATH hiding lookup errors.
- Legacy regression: 565 keyboard assertions, PrintScreen keyup/hold/menu/F10/
  F12; 22 SD lifecycle assertions, 157 Refresh assertions, 6184 SD SPI checks,
  7 golden renderer vectors, 46 contention checks, 41 INT/NMI checks, 197981 WAIT
  checks, 12748 video checks; Tape clock, machine-settings and full Test/ULA.
- MoonSound at 44100/48000/96000, flash synthetic-ROM persistence, OmniSound,
  MultiSound Max, OPL/DOS states, master volume and EVO display checks pass.
- 583176 common audio checks, native Moon PCM hash 9F3EE7BD381A9180 unchanged.
- Ten CPU/RAM/video/PCM/I/O fingerprints MATCH an isolated v86 run with the
  SAME current VMZ, including 3.5/7/14 MHz and model round-trip. The historical
  v86 stored log used another profile: the aligned fresh baseline was used,
  not a false comparison between different inputs. Control runtime only had its
  diagnostic log destination changed; published baseline files were untouched.
- Desktop smoke via Computer Use on diagnostics/gui-smoke: actual EXE boots
  BaseConf, Alt+P opens Machine Settings, Cancel returns to the main window,
  Help -> View Help opens the local v87 HTML in the default browser. No working
  D: emulator or desktop settings were modified.
- Full/Clean ZIP: every file size and SHA256 verified, mandatory dependencies
  present, no probes or test Data. Baseline-v86 is an exact 58-file copy.
- Sources: checkpoint ZIP/bundles retained, final combined source ZIP includes
  pinned OmniSound and both verified full local Git histories. No GitHub push.

Logs/manifests are in package/diagnostics, final-validation/diagnostics, source.
No GUI screenshot payload was saved by automation; live observations confirmed
the window and help. Existing startup diagnostic messages (initial null-path
CMOS before profile load and discovery of internal renderers without public
default constructors) were observed, not silently claimed fixed by this work.

## Not completed / limitations

- User acceptance, second physical PC, flash drive/FAT/exFAT, real different
  DPI/monitor arrangement, network-share/offline latency and all optional runtime
  dependency combinations. No 100% portable/no-crash claim.
- Physical controllers, real hardware, full applications/games on every model,
  manual OmniPlayer/NedoOS/MoonService long playback; unit sound is not a human
  listening test. Optional FFmpeg recording runtime is not in the v86 package;
  its path checks were adapted in source but recording not exercised here.
- No new performance/DSP optimization. Large saved media requires space/time.
- Known CPU NMI prefix-boundary issue remains OUTSIDE this portable work.
- No OS-wide ProcMon audit was run. Static own-path audit and targeted tests do
  not control Windows/browser/driver/third-party logs/caches/registry traces.
- Backup copies and crashed old journals can consume space. No automatic deletion
  of user state/history is introduced. Read-only install reports inability to
  persist; no silent AppData/TEMP fallback. Extreme path length remains subject
  to .NET Framework/Windows limits; atomic replacement on other filesystems needs
  the separate flash-drive test.

## Rollback and next work

Do not mix DLL versions. Exit v87, then use a complete preserved v86/v85/v84
runtime or archive. Keep original external media. New owned copies in Data/Saves
are independent user data; keep/export them before rolling back. UI and flash
settings live in different places from old versions; reverting binaries alone
does not migrate them back automatically. Checkpoint ZIPs and local Git bundles
restore source history; user D: directories were never overwritten.

Only after the user explicitly says the build works AND chooses it for further
work may the version catalogue mark it accepted.
