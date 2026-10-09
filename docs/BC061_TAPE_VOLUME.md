# BC Commander REAL tape volume — release 85 transfer

Baseline: `d3c1c61` (`codex/sd-refresh-v85`). Only
`src/ZXMAK2.Hardware/Evo/BeeperPentEvo.cs` changes hardware behavior.

BC's identified REAL ROM edge bridge uses the existing TapeDevice.Volume.
Other FE writes keep the Beeper volume and CMOS mux. Recognition requires
PC=05FF plus the full edge routine, ROM jump and bridge signatures; PC alone
does not capture ordinary game sound. The existing beeper renderer supplies
one audio path, without adding a second TapeDevice waveform.

Volume changes refresh an already held high level; reset clears the route.
ROM completion, BREAK and loading errors leave the bridge through the native
low FE write. BusDisconnect removes the Tape ConfigChanged subscription.
BC does not detect the emulator and physical-machine sound is unchanged.

Build ZXMAK2.Hardware in Release, preserving the release-85 Engine/CPU
libraries. The transfer archive also includes the tested x86-compatible
Hardware.dll for that exact release. Do not use its binary with unrelated
Engine versions; apply the source patch and rebuild instead.

Validated using real ROM pulse decoding and rendered PCM: Tape 0 is silent,
Tape 20/60 changes AC-RMS proportionally, live slider refresh works, and
ordinary game Beeper remains independent. TAP, TZX, BREAK, checksum error,
F12 and reset routing are covered. Original D:/release85_BC stays unchanged;
the test copy is D:/release85_BC061_TapeVolume.
