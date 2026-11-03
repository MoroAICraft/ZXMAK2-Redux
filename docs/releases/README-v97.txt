ZXMAK2 v97 TEST — ZX OPL3 (UR4QBP)
Base: accepted v96. Separate test release; not the accepted working base yet.

New board: ZX Evolution → Machine Settings (Alt+P) → Other / ZXBUS PentEvo
→ active Slot 1 or Slot 2 → ZX OPL3 (UR4QBP) → Apply.
The Full package starts with this board alone and without attached media.
The Clean archive omits the four active machine-state files.

For the tester's arrangement select ordinary ZX-MultiSound Rev.A2 in the
other slot. Both slot orders work. MoonSound, MultiSound Max and OmniSound
overlap OPL3 ports and cannot be paired with this board.

Implemented from UR4QBP's ZX OPL3 Sound Card V1.0 schematic in ZX_OPL3.zip:
YMF262 at 14.31818 MHz, YAC512 stereo A/B, ports #xxC4–#xxC7.
No hardware jumpers, DOS gate, firmware, PCM, sample ROM/RAM or MIDI UART.
MIDI requires an OPL3-capable guest player; this is not Yamaha OPL4 playback.
The guest must select an OPL3/FM output, not MoonSound/OPL4 or PCM ROM.
No claim of exact identity with the tester's photographed GAL-based board.

New library: Lib/Native/ymfm_opl3.dll (x86, ymfm YMF262, BSD-3-Clause).
Existing MoonSound/Max/OmniSound cores, master sound path, media handling,
hotkeys and CPU/video emulation are unchanged. Card selection follows the
existing cold hardware-change apply; changing volume alone does not reset.

Portable structure is preserved. Keep the complete folder together.
No ROM or sound bank is needed for this new board; other devices keep their
existing dependencies. Windows/.NET/DirectX prerequisites are unchanged.
Keep v96 separate for rollback; never overwrite an installed working copy.

See diagnostics/OPL3-V97-REPORT.txt in the outer package for executed tests,
measurements and limitations. User acceptance is required before adoption.
