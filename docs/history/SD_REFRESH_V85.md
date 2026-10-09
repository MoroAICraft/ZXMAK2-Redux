# v85 — Refresh Folder: save, reread, cold start

Date: 2026-10-09. Base: accepted v84, commit
`005c04db4dc6c35c838575ed895045b14544c9c7`, tag `zxmak2-v84-bc-nmi`.
Branch: `codex/sd-refresh-v85`; final tag: `zxmak2-v85-sd-refresh`.
v83 FIR/HQ is NOT included. No changes to CPU, renderer, sound generation,
mixing, keyboard routing, PrintScreen NMI, guest OS/player or native libraries.

## User behavior

- Only PC files changed: Refresh immediately builds a new card from the current
  folder. No question. The guest machine cold-starts after success.
- Guest files/tree changed: show `Refresh SD folder?` with three action buttons:
  `Refresh (Save changes)`, `Refresh (Without saving)`, `Cancel`.
- Save: apply guest-created/changed/deleted/renamed files and directories only,
  then reread/remount. Unrelated PC additions, replacements and deletions remain.
  This includes a changed TRD image, not just NedoOS configuration files.
- Without saving: build the current PC folder first; only after successful
  construction discard the old guest session. Never write guest data to the PC.
- Cancel, Escape and closing the question: keep the current card/session.
- Success uses the existing SD-image replacement power-cycle path. This is not
  Factory Reset. Previously running VM resumes; paused VM stays paused.
- Error or a conflicting same file: no reset, no completed refresh, current
  card and unsaved session retained. Cancel or Refresh (Without saving) permits
  choosing the PC version. Conflicting versions are never silently merged.

FSInfo-only writes, padding-only changes and file data written then reverted do
not trigger the Refresh question. Existing close/eject/replacement save prompts
remain; their shared folder-save implementation now permits unrelated PC edits.
Startup still reads current PC contents and ignores stale folder journals.
Save as Image remains an export of the current session, NOT an automatic Refresh.

## Implementation and safety

`ZsdPentEvo.RefreshMountedFolder` replaces the previous call to MountHostFolder.
It evaluates actual guest file/tree changes, asks the optional `IActionUserQuery`
and swaps cards only after a valid replacement is ready. Existing success command
notification and MainViewModel power-cycle behavior are reused unchanged.

The FAT folder commit checks only affected paths and source data dependencies,
not every unrelated PC file. The original file is copied lazily on its first
guest data write under portable `Data/SD-sessions/Originals`; untouched tail
sectors therefore cannot come from a later PC replacement. Metadata and SHA256
detect same-file conflicts for captured originals, even if the PC restores the
original timestamp/length. Only actually written files are copied, not the whole
folder or multi-gigabyte virtual card. A first write to a large file consequently
has a one-off file-copy cost and requires enough local disk space.

All changed guest files are staged before writing to the host. Original targets
are moved to a same-volume `.zxmak2-sd-backup-<GUID>` directory. Installed files
and originals are held read-only until the replacement snapshot is built. That
private backup is excluded from the new card. A build failure rolls back source
changes and keeps the old session. If rollback itself fails, original backups
are retained and their path is reported; no blind deletion of a newer PC file.
Normal successful completion removes owned staging/backup/snapshot data.

Strict reads are used for commit/export: inaccessible or changed source data
must cause an error rather than a silently zero-filled saved file/image.
ExportImage validates uncaptured inputs before publishing a replacement image.
No AppData/TEMP fallback, automatic stale-session recovery or host-file overwrite
on startup was introduced.

This is conflict-aware save, not a general concurrent filesystem merge engine.
Do not continuously edit the host folder during the transaction. Files never
captured by a guest data write use the existing length/timestamp baseline; a PC
tool that deliberately preserves both can evade metadata-only detection. Host
disk/process failure during the multi-file save cannot provide OS-wide atomicity;
the rollback backup is retained for recovery. These are not claims of immunity
to arbitrary concurrent processes, power loss or malicious filesystem changes.

## Changed production files

- `src/ZXMAK2.Host/Interfaces/IUserQuery.cs`: optional action-labelled query API.
- `src/ZXMAK2.Host/Services/UserQueryProxy.cs`: forward optional query to frontend.
- `src/ZXMAK2.Host.WinForms/Services/UserQuery.cs`: English save/discard/cancel dialog.
- `src/ZXMAK2.Hardware/Evo/ZsdPentEvo.cs`: transactional Refresh choice/card swap.
- `src/ZXMAK2.Hardware.Circuits/SecureDigital/SdCard.cs`: excluded-directory overload.
- `HostFolderFat32Stream.cs`, `.Commit.cs`, `.Journal.cs`, new `.Merge.cs` in that
  SecureDigital directory: file-level merge, snapshots, rollback, strict export.
- `src/ZXMAK2.Hardware.Circuits/ZXMAK2.Hardware.Circuits.csproj`: include new partial.

Tests: `src/Test/Program.cs` and `tools/SdSessionTest/LifecycleProbe.cs` change an
obsolete expected rejection of unrelated PC edits. New `RefreshProbe.cs` and
`tools/Optimization/RunRegressions.ps1` cover the new lifecycle. Test.exe was
rebuilt and marked 32-bit; its rendering/CPU benchmark algorithms are unchanged.
The runtime substitutes only Host, Host.WinForms, Hardware, Hardware.Circuits
DLLs, Test.exe and release documentation. Every other v84 runtime file is
preserved byte-for-byte, including Engine/CPU, native audio, ROMS.PAK and SF2.

## Checks performed

- Release solution build succeeds; existing missing .ruleset warnings remain.
- RefreshProbe: **157 assertions**, including PC-only replace/add, guest partial
  TRD write/tail preservation, selective save, discard, Cancel/None, same-file
  hash conflict, rename/create/delete/directory operations, external directory
  child collision, forced replacement-build failure/rollback/retry, write lease,
  damaged FAT discard, safe image-export failure, real MainViewModel power cycle,
  running/paused VM and English dialog layout/Enter/Escape settings.
- Real unmodified NedoOS in an isolated media copy: cold SD boot, console
  `cd /`, `md session`, PC adds PCDATA.TXT, Refresh (Save changes), cold SD boot
  again, `dir /`: **session and pcdata.txt both visible**. Trace reports dirty
  before=True, after=False, guestDir=True. Clock 14 MHz during NedoOS.
  An earlier harness assertion looked for root/session while the console was in
  bin; bin/session had correctly been saved. Corrected root run succeeds.
- Keyboard routing, PrintScreen NMI, golden renderer, contention/floating bus,
  IRQ/NMI, WAIT ports, video timing, tape clock, SD lifecycle, PentEvo profile:
  all automated probes pass. Full Test/ULA/benchmark passes.
- Test hostfoldersd, MoonSound, OmniSound, MultiSound Max, Omni/Max OPL, master
  volume and display modes pass. No sound-card implementation changed.
- AudioMixProbe: **583176 assertions**, Moon native PCM hash
  `9F3EE7BD381A9180`, 0/50/100% gain and common output pass unchanged.
- All **10 CPU/RAM/video/PCM/I/O fingerprints match v84 exactly**: Spectrum
  48/128, snapshot cases, PentEvo 3.5/7/14 MHz and machine-switch cases.
- Full/Clean ZIP entries are checked by size and SHA256. Full source includes
  pinned OmniSound and verified Git history bundles for both repositories.

Not performed: manual clicking of the production desktop menu/dialog, individual
game save screens, continuous external-editor stress, disk-full/OS crash/power
loss injection, every filesystem permission/network-share configuration. The
real NedoOS run uses diagnostic keyboard input and screenshots, not a visible
desktop app. Synthetic partial TRD writes cover byte preservation, not a specific
game's own save routine. Performance benchmarking is not an optimization claim.

## Packaging and rollback

`L:\Work_two\ZX\ZXMAK2-v85-ZXEVO-BC-SD-Refresh-Test-20261009`:
`release`, Full/Clean ZIP, `baseline-v84`, `diagnostics`, `source`.
Clean omits personal VMZ/VMIDE/CMOS; music/native/ROM dependencies and Test remain.
Unpack v84 or baseline-v84 separately for full rollback; do not mix DLL versions.
Source tag/history preserve v85. Accepted v84 remains intact. User D: installation
and original SD/music folders are untouched; only isolated test media were used.
Four preexisting untracked engine-research files remain unchanged/excluded from
the committed source export. No GitHub publication or unrelated source cleanup.
