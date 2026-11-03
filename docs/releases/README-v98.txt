ZXMAK2 v98 TEST — audio output audit
Based on user-accepted v97 (ZX OPL3 UR4QBP).

This is a separate test build. Keep v97 for rollback.
Native sound cores, ROMs, banks and hardware port/timer logic are unchanged.
Host-rate PCM avoids a redundant DAC reconstruction stage. Timed PSG/beeper/
Covox/GS DAC output retains its original reconstruction. Card volume handling
and DC history recovery are corrected; no AGC, EQ or heavy HQ filter is added.
MultiSound Max no longer inherits the unrelated OPL4 FM attenuation from the
reused core. Start at moderate volume: its OPL3 output is now about 9 dB louder.
MoonSound's real OPL4 mixer defaults and guest register semantics are retained.

No user media is included. The Full default profile has standalone ZX OPL3.
Clean has the same runtime dependencies without the active root state files.
See the accompanying audit report for measurements and remaining limitations.
Removing the redundant filter is not a new anti-alias resampler. Use matched
A/B excerpts to judge timbre; raw excerpts document original output levels.
Acceptance requires user listening and explicit confirmation.
