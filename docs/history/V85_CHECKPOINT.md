# v85 SD Refresh checkpoint — 2026-10-09

Base: v84 / 005c04db4dc6c35c838575ed895045b14544c9c7. Branch codex/sd-refresh-v85.
User approved Refresh: PC-only changes without a question; guest changes ask
Refresh (Save changes) / Refresh (Without saving) / Cancel. Successful refresh
uses the existing same cold machine start as changing an SD image; never
Factory Reset, never reset on cancel/failure. No audio/NMI/CPU changes.

Implemented selective commit validation instead of global folder rejection,
first-write snapshots/hash only for guest-touched original files, strict reads
when staging, transaction callback that builds/validates new SD before clearing
the old journal (rollback if build fails). PC-only additions/edits/deletions stay.
Same-file/structural conflicts fail safely, no automatic winner chosen.
No-save also builds replacement before disposing current session. Harmless
FSInfo-only and reverted data writes do not prompt. Invalid guest FAT still
allows explicit discard. Custom action query is optional; WinForms/proxy support
exact agreed labels, no API break of existing IUserQuery implementations.

New RefreshProbe passed 137 assertions: PC-only, guest save and external other
file, TRD partial write/tail, discard, cancel/close, preserved-timestamp collision,
create/rename/delete/directories, directory deletion vs PC addition, injected
build rollback/retry, failed no-save build retains session, malformed boot
discard, actual MainViewModel command cold start only on success, UI labels/layout.
Updated Test /hostfoldersd passes. Existing LifecycleProbe passes with actual
same-file conflict rather than unrelated addition. Full regressions to repeat
on final DLLs; finish permissions/race safety and actual NedoOS refresh check,
then final package/source/history. Runtime staging:
L:\Work_two\ZX\ZXMAK2-v85-ZXEVO-BC-SD-Refresh-Test-20261009.
Control retained in baseline-v84. User D: runtimes/media untouched; four old
untracked engine-research files preserved. Do not publish unfinished staging.
