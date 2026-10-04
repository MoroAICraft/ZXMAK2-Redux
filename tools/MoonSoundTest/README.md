# MoonSound Rev.01 / CPLD 1.01 checks

From the 32-bit Release output with the native DLL and the locally supplied
instrument ROM, run `Test.exe /moonsound`, `/multisoundmax`, `/omnisound` and
`/omni-opl`. `/moonsound` includes 44.1/48/96 kHz, the NedoOS timer detector,
low-byte port decode, C6-read-only PCM remapping, flash IDs, SRAM and a synthetic
flash atomic-save/backup/reload fixture. Benchmark switches are unchanged.

`Test.exe /moonservice <MoonService.bin>` executes the unchanged initialization,
screen-address and flash-identification routines of the original Micklab v0.3a
binary. It does **not** claim to test the full keyboard menu, ROM update or MIDI
players. Obtain that binary from the author's service-program archive.

Build the standalone synthetic native probe (no user ROM/file writes):

```powershell
zig c++ -target x86-windows-gnu -std=c++14 -O2 -I../../src/Libraries/YmfmOpl4 flash_probe.cpp ../../src/Libraries/YmfmOpl4/ymfm_opl.cpp ../../src/Libraries/YmfmOpl4/ymfm_pcm.cpp ../../src/Libraries/YmfmOpl4/ymfm_adpcm.cpp -o flash_probe.exe
.\flash_probe.exe
```

The local v75 ROMS.PAK includes the user's Max GS ROM as
`ZX-MultiSound-Max-GS.bin` from `USER_ZxMultiSoundMax.zip`:
`GS_ROM/AM29F040B@PLCC32.BIN` (524288 bytes). A clean build needs that same
local packaging step, or the file beside the application/in `roms`.
The user-supplied ROM is not committed to Git.

References: [Micklab board/service page](http://micklab.ru/My%20Soundcard/ZXMMoonSound.htm),
NedoOS `svn://zxevo.ru/nedoos/nedoos/src/gp/common/moonsound.asm`, MX29F016
manufacturer datasheet PM0590 Rev.1.4. The user-provided firmware 1.01 archive
contains a CPLD `.pof`, not the YRW801 instrument ROM. The PCM latch behavior is
confirmed by NedoOS's `switch_to_pcm_ports_c2_c3` macro; the public 1.00 RTL
documents the original decoder/ENDOS gate.

## v77 real NedoOS / ENDOS regression

`RealNedoProbe.cs` runs the original ROM, SD boot, NedoOS and an unmodified
OmniPlayer in the emulator engine. It injects only guest keyboard states and
logs the original port handlers, not substitute timer routines. Compile x86
against the runtime DLLs including ZX-OmniSound; place its executable/config
beside those DLLs and run only on a **copied** SD folder:

```
RealNedoProbe.exe <profile.vmz> <copied-SD-folder> <trace-folder> key:D5 wait:300 key:F10 key:Return gp key:Return key:Escape key:Y gp
```

Add `board:OmniSound` to test the shared PCM block. Actual clock values are
recorded; a requested speed is not proof of that speed surviving NedoOS.
`/moonsound` now tests physical IODOS-low release and open JP1/ROMPG1 instead
of the incorrect assumption that MemoryPentEvo.DOSEN disables the board.
See `docs/history/MOONSOUND_NEDOOS_ENDOS_V77.md` for traces and limitations.

Flash model limits: program/erase contents update immediately, polling waits
the modelled completion time. No electrical failures/wear, program of other
sectors during erase suspend, or multiple-sector erase queue is modelled.
