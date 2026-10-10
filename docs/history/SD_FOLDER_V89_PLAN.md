# v89 SD folder save/Refresh — checkpoint

User requested v89, not overwriting v88. Main baseline ccb62c8,
pinned OmniSound 756cb47 unchanged. Branch codex/sd-folder-v89.
Package L:\Work_two\ZX\ZXMAK2-v89-Portable-SD-Folder-Fix-Test-20261010.
User runtime D:\release88_Portable_SoundCards is read-only diagnostic scope;
user source L:\Work_two\ZX\ZX_IMG\flash_pentevo is never altered.

Evidence: user log 19:13:28 PathTooLongException in CopyDirectory during
PrepareClose/CommitExternalFolder; ~87549 files / 3546001188 bytes in source.
87k whole-tree first Save is mandated by the agreed copy-on-save policy, not
a deadlock demonstrated by the log. Lack of progress made copy look frozen.
Refresh checks raw relative descriptor against CWD and wrapper-menu enabled
state becomes stale. Both confirmed in source and need regressions.

Implement: per-EXE .NET long-path opt-in, preflight destination/link checks,
modal worker progress with stopped guest (no DoEvents/reentrant guest commands),
correct Refresh path/menu state, save-config-failure resumption. Fast untouched
FAT lookup exactly matches generated sectors; changed FAT/cycle/share checks stay.
Preserve guest changes on failure, rollback/PC conflict checks and user originals.

Tests: old v88 reproduction; real user tree full read-only-source save to private
Data, deep paths and byte checks; close Save/Cancel/error/internal remount;
UI menu state before/after mount/rebase/eject; progress worker completion/error;
old SD/portable/model/keyboard/audio/video suite. Report unperformed manual tests.
Package Full/Clean/source/histories/baseline-v88; no GitHub/D: replacement.
