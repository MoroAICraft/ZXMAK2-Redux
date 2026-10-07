ZXMAK2 v82 - Common Audio test - 2026-10-08

Based on validated v81; previous optimization, Moon ports, keyboard and SD
behavior retained. All files needed for music (native DLLs, ROMS.PAK and
GeneralUser-GS.sf2) and Test.exe remain included.

New shared output for every machine using the emulator sound bus:
- Silent/unused sound devices no longer reduce the volume of active devices.
- Wide stereo mixing with fixed output headroom, no automatic normalization.
- Sample-rate-aware 5 Hz DC removal preserves bass.
- Master volume and soft limiting applied once at final output.
- MoonSound volume slider now works; at 100% chip PCM is unchanged.

Settings: Machine Settings > ULA > Audio Output.
To compare with v81, enable "Legacy audio mix (v81)" and Apply.
This restores the old common mixer/filter, not the ignored Moon volume bug.
For a COMPLETE rollback unpack the full preserved v81 archive into a separate
folder; do not mix individual DLLs from v81 and v82.

Real NedoOS + unmodified OmniPlayer 1.0.41 tested with the supplied 10.MID
and Waiting 4 Robot PT3. Results/logs/PCM and source backups are beside release,
outside the runtime ZIP. MIDI author velocity/volume/controller dynamics are
not overwritten. This is not a claim of identical sound to ZXTune in all
formats, nor of physical-hardware/live-Windows-output listening verification.

Full ZIP retains the previous release's settings. Clean ZIP omits personal
VMZ/VMIDE/CMOS and the standalone user ROM, but includes runtime ROMS.PAK,
sound banks and all native music libraries. User D: installations unchanged.
