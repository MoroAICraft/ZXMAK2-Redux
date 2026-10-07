# v82 audio checkpoint

Base: v81 production tag `zxmak2-v81-optimized`, research HEAD `960c3e4`.
Work branch: `codex/audio-v82`. Previous release and user D: installs untouched.
Candidate package: `L:\Work_two\ZX\ZXMAK2-v82-ZXEVO-BC-Common-Audio-Test-20261008`.

Implemented: final wide stereo sum, fixed 0.5 output headroom independent of
installed renderer count, sample-rate-aware 5 Hz DC blocker before limiting,
single final master/soft limiter; UI/config legacy v81 mixer option.
MoonSound's previously ignored Volume scales completed PCM, 100% unchanged.
No chip/port/timing/native library/SD/keyboard modifications.

Build completed; component regressions and original NedoOS/OmniPlayer 1.0.41
playback, package/export/commit are still pending at this checkpoint.
The four preexisting untracked engine research files remain untouched.
