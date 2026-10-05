# Portable SD folder lifecycle — v78 checkpoint

Base: local commit d984e2c (v77). Published v77 Hardware.dll and
D:\release77_moonsound_fix\ZXMAK2.Hardware.dll have identical SHA256.
Do not modify D: runtime or the unrelated dirty primary checkout.

User policy: every new folder mount rebuilds current PC contents, including
after a crash. Never recover a folder overlay. Missing/unavailable configured
folders start silently with an empty slot; retain descriptor for later retries.
Unsaved guest folder changes after a crash may be lost (explicitly accepted).
Existing image recovery is unchanged apart from portable journal location.

Changes staged:
- Folder journals are per-mount GUIDs under Data/SD-sessions/HostFolders.
  Normal teardown removes only its own journal. Crashed/copied journals are
  ignored, never replayed; legacy AppData files are neither read nor deleted.
- Image journals are under Data/SD-sessions/Images. SD commit staging is under
  Data/SD-sessions/Staging, with no AppData/TEMP fallback.
- Internal configured SD paths are saved relatively to the emulator directory;
  external paths remain absolute. Relative paths do not depend on process CWD.
- Accepted close stays stopped after SD save/discard; cancelled/failed close
  resumes the VM. Stop precedes the dirty-state check to avoid late guest writes.
- PC/guest conflict checks and transactional source rollback are preserved.
  ExportImage remains a snapshot, not a folder save or automatic refresh.

First build and Test.exe /hostfoldersd passed (2026-10-05). Includes stale journal
with modified PC folder, folder save/delete/rename, PC conflict protection,
image recovery/discard, missing-folder retry and relative-path/CWD tests.
Remaining: lifecycle confirmation/export/close probes, sound regressions,
package verification and final report.

Output: L:\Work_two\ZX\ZXMAK2-v78-ZXEVO-BC-Portable-SD-Folder-Test-20261005.
Its rollback-v77 copy preserves the original published v77 runtime.
