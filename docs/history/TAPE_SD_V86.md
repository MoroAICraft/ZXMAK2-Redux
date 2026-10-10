# v86 — BC REAL Tape volume and SD SPI write responses

Date: 2026-10-10. Package:
`L:\Work_two\ZX\ZXMAK2-v86-ZXEVO-BC-Tape-SD-Test-20261010`.
Branch: `codex/release-v86-tape-sd`; tag: `zxmak2-v86-tape-sd`.
The two requested patches were ALREADY present in the current checkout, whose
starting HEAD was `f68d667c6e7323a344e7b7a356fa299d76582717`. No rollback to
the older v85 sources, whole-file replacement or binary patch copy was used.

## Provenance and changes

Input: `E:\ZXMAK2_TAPE_SD_PATCHES.md`. Both supplied patch files from
`BC-Commander/Build/probe/ZXMAK2-BC-TapeVolume-SDWrite-Patches` pass
`git apply --reverse --check` against current source. They are not applied again.

- `cea0ac77789491aeba7884bd5fa15fe3f566151c`:
  `src/ZXMAK2.Hardware/Evo/BeeperPentEvo.cs` routes only the fully identified BC
  REAL edge routine through TapeDevice.Volume. Detection requires PC=05FF plus
  ROM jump and complete edge/bridge signatures; PC alone is insufficient.
  Tape ConfigChanged refreshes a held level; disconnect removes subscription;
  ordinary game Beeper and reset retain their normal behavior.
- `f68d667c6e7323a344e7b7a356fa299d76582717`:
  `src/ZXMAK2.Hardware.Circuits/SecureDigital/SdCard.cs` gives CMD24/CMD25 R1=00
  before the data token. Data response 05/0D follows the two CRC bytes. CMD25
  responds after each block and waits for FC or stop FD. CRC validation is still
  disabled as before; no new full SD hardware/CRC emulation is claimed.

New regression integration includes `SdWriteProtocolProbe.cs` in the common
`tools/Optimization/RunRegressions.ps1` suite. `tools/BCTransferTest/RunChecks.ps1`
runs native BC tape/write tests ONLY against a private fixture within package
diagnostics. Copies of the current BC binaries/symbols/probe/generator sources
are retained there. Python generators are provided BC scripts, not replacement
emulator code. BC checkout and original media are read-only during preparation;
actual writes target disposable generated FAT32 images exclusively.

Runtime starts with the published v85 files (tag `zxmak2-v85-sd-refresh`,
`d3c1c616d5fd4ca6a507fa75d37feca80d31bf59`), then substitutes ONLY freshly built
Hardware.dll and Hardware.Circuits.dll. README is updated for v86. All other
runtime files including Test.exe, CPU/Engine, frontend, native audio, ROMS.PAK,
SF2 and personal VMZ/VMIDE/CMOS remain byte-identical to that v85 runtime.
The full current solution builds successfully; existing missing-rule-set warnings
remain. Build output files unrelated to the patches are not substituted.

New DLL SHA256:
- Hardware: `85B65D9EA1235F26C48E8B2EA97A5909326F0DBC22E8D274F6EB78BD1D503701`.
- Circuits: `152D471323C9904AFB730F06B98DF76403BD48C1C858688C9AA9C84D17C360D5`.
These need not match historical test DLL hashes: source fixes were rebuilt.

## Verification

- Selected NEW Circuits.dll: **6184 SD SPI assertions pass**. CMD24, CMD25 two
  blocks/FD, subsequent write, CMD17 readback, exact response/CRC clock order,
  read-only rejection and media bounds. The unchanged published v85 DLL fails
  the same test at `CMD24 R1=FF expected=00`, reproducing the requested defect.
- Native original CPU/ROM/BaseConf/SD execution: **29 BC scenarios**. The main
  suite passes 27 cases (9 Tape/FAST + 18 writes), with two additional TZX volume
  runs saved as bc-tzx-20.txt / bc-tzx-60.txt. The final repeatable script includes
  those two cases and therefore runs all 29 together on a future rerun.
- REAL TAP and TZX at Tape=0/20/60, live slider at held high, normal game Beeper
  gain, PC-only false identification, reset; BREAK, checksum error and native F12
  during playback. FAST TAP/TZX and REAL still wait for manual Enter, then load
  and return through F12. Native F1 after an idle pause also passes.
- Actual rendered REAL AC-RMS: TAP 0=0.00, 20=6242.37, 60=18727.11; TZX
  0=0.00, 20=6242.27, 60=18726.81. Successful captures: 386 frames and 12074
  identified writes. Pilot trace stays at the existing model value ~0.6390 ms
  versus nominal 0.6194 ms; these two patches do not change pulse timing.
- Native SD write cases: closed, sector, range, cancel, readonly, files, active
  FAT, full, partition, invalid, geometry, source, SDSC bounds, reject, busy,
  mismatch, SDHC, collision. Each passes an independent Python FAT/content audit
  after the stream closes; injected failures are bounded and preserve fixtures.
- All common probes pass: keyboard routing, PrintScreen KEYUP/down/hold, F10,
  F12, IRQ/NMI, WAIT, video timing/golden renderer/contention/floating bus,
  Tape clock, PentEvo profile, full Test/ULA/benchmark and SD host-folder tests.
- v85 **157 Refresh assertions** and **22 SD lifecycle assertions** pass again:
  selective guest save/unrelated PC edits, discard/Cancel, error rollback,
  restart only on success, running/paused VM, close/save/discard/cancel/error.
- MoonSound, OmniSound, MultiSound Max, Omni/Max OPL, master volume and display
  tests pass. Common AudioMix: **583176 assertions**, native Moon PCM hash
  `9F3EE7BD381A9180`. No common audio quality/DSP changes are introduced.
- All **10 CPU/RAM/video/PCM/I/O fingerprints match v85** including Spectrum
  48/128, snapshots, PentEvo at 3.5/7/14 MHz and machine-switch cases.
- Full/Clean ZIP verification checks entry count, length and SHA256. Source ZIP
  includes pinned OmniSound; both complete local Git bundles are verified.

Logs, source probes, input patches, original failure, fixture and hashes:
package `diagnostics`. Real BC CPU/ROM execution here is scripted diagnostic
input, not manual clicking in a desktop UI. No physical ZX Evolution, actual
SD card, power-loss/OS crash injection or automatic recovery was tested.
No new NedoOS player/game-specific save-screen test was performed in v86;
shared SD lifecycle, native BC disk-write and existing host-folder tests cover
the affected layers. The v85 real NedoOS Refresh evidence remains in v85.

## Separate CPU NMI issue — deliberately NOT included

The supplied transfer document reports ProcessSignals accepting NMI between
an instruction prefix and its continuation. These two tape/SD patches do NOT
fix that issue. CPU and PrintScreen routing are unchanged; passing ordinary
NMI/PrintScreen tests does not prove prefix-timed NMI safety. It needs a separate
minimal CPU change and tests for ED/DD/FD/CB/DD-CB/FD-CB, full-instruction
boundary, ordinary NMI, HALT and EI. Reported candidate: defer NMI while
FX/XFX are active. Do not use BINT as a blanket NMI gate, since it also represents
EI's maskable IRQ inhibition. No BC/player workaround is silently applied.

## Distribution and rollback

`release` contains only runtime; Full/Clean ZIP sit in package root. Full keeps
the published v85 settings, Clean omits VMZ/VMIDE/CMOS. Music/native/ROM
dependencies and Test.exe remain. `baseline-v85` is the untouched published
58-file v85 runtime; rollback by unpacking it separately, never mix DLL versions.
`source` contains source-with-OmniSound ZIP and both local-history bundles.
Input user markdown/patches are preserved under diagnostics/transfer-input.
Four preexisting untracked engine-research files remain unchanged, excluded
from the committed export. Prior packages, user D: emulator copies and real
SD/music folders are not modified. Nothing is pushed to GitHub.
