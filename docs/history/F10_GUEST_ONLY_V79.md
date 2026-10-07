# v79 — F10 without native menu activation

Base: v78, commit 29248ad. Working checkout: omnisound-integration; the dirty
primary checkout and all existing published/working runtimes are untouched.

No explicit F10 ShortcutKeys assignment exists in the main-window menus.
The activation is the default Windows/WinForms F10 menu selection behavior.

Minimal runtime change (ZXMAK2.Host.WinForms.dll only):
- HostKeyboardMessageFilter consumes F10 WM_KEYDOWN/UP and WM_SYSKEYDOWN/UP
  for the active main window or its open menus, before native menu dispatch.
- MainView.ProcessCmdKey consumes F10 before calling base/MenuStrip, also
  covering direct preprocessing. Modifier combinations do not select menus.
- F10 is NOT registered as a host shortcut in HostKeyboardRouter: doing that
  would block the guest through the shared physical-key ownership policy.
- Both keyboard mapping files, physical readers, guest keyboard and firmware
  are unchanged. Already-open UI menus still own keyboard input normally;
  F10 returns to the guest after menu close and physical release.
- Other windows' local debugger actions are unchanged (not menu activation).

KeyboardRoutingProbe passed 565 assertions: all previous ownership, menu,
focus/reset/reacquisition and Max Apply checks plus F10 preprocessing,
modifier combinations, all four message types, guest availability, open-menu
isolation, release/reset recovery, no main-menu F10 shortcuts, no stealing
from another inactive-window context.

Release solution built successfully; only pre-existing missing-ruleset warnings.
Testing was in-process WinForms regression, not physical keyboard/UI automation.
A manual press/release in the user's installed runtime remains a useful check.

Packaging follows existing conventions: separate L:\Work_two\ZX v79 folder,
release and Full/Clean ZIPs. v78 is the rollback, never overwritten. Compare
all runtime file hashes: only Host.WinForms.dll, HOTKEYS.md and version README
may differ. Keep Test.exe, benchmark, ROMS.PAK, native sound DLLs and music/SF2.
No GitHub publication.
