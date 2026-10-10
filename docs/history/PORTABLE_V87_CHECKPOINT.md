# Portable v87 — checkpoint, 2026-10-10

Status: work in progress, NOT accepted, NOT the main user build.
Source baseline: c57bf2b6744be4f00c75908ed4cce9311a7bb8b7 (v86).
Branch: codex/portable-v87. Previous accepted control remains v84;
published v85/v86 and user D: installations are not overwritten.

User acceptance is mandatory before using this release as the next work base.

## Agreed boundaries

- Keep boot.zip, ROMS.PAK, ZXMAK2.vmz/.vmide/.cmos/.nvram at EXE root.
- Preserve hardware logic, CPU, timing, audio, keyboard routing and NMI.
- Root comes from entry EXE, never CWD or the relocated Engine DLL.
- Internal media paths are root-relative. Missing optional media means empty slot.
- Mandatory libraries/ROM failures must not masquerade as a usable machine.
- Own configs/logs/cache/temp/flash/session writes remain inside the install.
- External source media is not modified; no automatic full copy on mount.
- Refresh save/discard/cancel and close/eject prompts must stay functional.
- A saved external folder result needs a separate local destination; format
  choice requested from user, no external overwrite fallback.
- Firmware flash key is ROM content identity, not install path/user identity.
- Help describes general commands separately from guest/model-specific keys.
- No automatic deletion of old builds or sources; no GitHub publication.

## Risks and corrections to the discussion plan

- Four root state files are an active configuration, not a new per-model profile
  manager. Do not promise independent saved profiles on model switching.
- Read-only installs cannot meet writable portable persistence. Report the issue,
  do not silently use AppData/TEMP.
- CHM paths containing '#' have a Windows limitation. Local HTML is the portable
  primary help; retain historical CHM as documentation, not a system TEMP copy.
- Windows/framework/driver traces are outside the emulator's control.
- A journal requiring an external base is not a standalone portable saved image.
- Existing CPU NMI prefix-boundary issue is outside this work.

## Resume

1. Implement and verify common paths/bootstrap, runtime layout and local settings.
2. Verify media lifecycle/external source protection and missing references.
3. Update help, regression tools, packaging; preserve full baseline and sources.
4. Publish a TEST package only after automated checks, enumerate manual checks.
