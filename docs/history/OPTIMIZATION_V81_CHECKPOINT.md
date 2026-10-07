# v81 checkpoint — 2026-10-07

Authorized: second common-engine optimization pass based on v80; NOT audio quality.
Primary branch codex/optimization-v81, starting bbf8e62 / zxmak2-v80-optimized.
User test installation D:/release80_sound_test matched all v80 DLL/EXE/ROMS.PAK;
only user's configuration differs. User D: and its SD folder remain untouched.
Isolated package: L:/Work_two/ZX/ZXMAK2-v81-ZXEVO-BC-Engine-Optimization-Test-20261007
with baseline-v80/release/diagnostics. Prior v80 source+submodule+bundles and runtime
are already preserved in the v80 package. Worktree began clean.

First diagnostic: PhaseCostProbe wraps only frame-boundary callbacks for timings;
memory/clock counts measured separately to avoid per-access Stopwatch distortion.
Pure identity A/B experiment removes timing hooks only for a test machine with
no ICpuClock controller. This is not a shipped fix or replacement real workload.
Real NedoOS/player and active chip workload to follow.

Measurements: phase-v80.txt, pure identity experiment ~309-316ms to292-294ms.
Pentagon 20 frames: sync472898/refresh177861/memory391125 calls into noops.
Evo's live callbacks MUST remain regardless of current multiplier.
Boundary timing has native chip cost, not just drawing; no filter/chip edits.
setup-v80: 20 same-profile reloads ~558-560ms, gc0=70/gc2=35.
Candidates: fixed-capability timing hooks (BusManager), reuse fully cleared
private 64K routing arrays (EventManager). Only engine source edited.
Need build, differential and explicit stale-handler/gate tests before accepting.

Build and existing 9 external+7 device+normal Test regressions PASS.
RoutingSafetyProbe 458800 checks PASS, including max-rate then lower rate,
legacy provider, clearing all maps, ordering/open bus. Candidate setup20x
465-510ms; gc0 42-45/gc2 12-16 (initial, repeat reverse order still needed).
MeasureFinal now running with BaselineName=baseline-v80; no concurrent probes.
Generalized packaging/measurement scripts keep v80 defaults for reproducibility.
RealNedoProbe adds no-trace bounded frame+mix benchmark; only copied SD may be
used. Next: final differential, true OS/player/tune and packaging. No audio edits.

Final: 10 fingerprints exact v80 match. Paired builtin full/light ~5.8–8.5%
less time. No stable Evo speedup (small negatives up to1.6%, CPU-only control
also shifted1%). setup median578->486.5ms (~16%), gc2 35->14.
Genuine NedoOS+OmniPlayer example.pt3 playing: peak606/hashA6C7A1A0BB664D0D
and final PNG identical v80/v81; benchmark6074->5998ms, not confident speedup.
Quiescent redraw/copy proxies measured; no rendering/audio production change.
Omni harness invalidly retained internal TSFM; baseline v80 failed likewise.
Fixed harness only to mirror UI internalNone/removal; valid Omni cold/repeat
now detects OPL3/OPL4/ROM/RAM. Invalid logs retained and clearly classified.
Engine SHA89C07730F1E54E8A6058F730E6E3566DD070925704ECB20517ACE14B980E02A2;
only engine changed in runtime. Next: verified ZIP, packaged smoke, source/tag
export. Do NOT rerun profiling/timing or start audio-quality work.
Full/Clean ZIP 58/55 entries hash-verified, extracted Clean smoke SD/Moon/
Omni/Max/Evo-display PASS. Working D: Engine hash still exactly v80 control.
Implementation and validation complete; final commit/tag/source bundles export
only remains. Do not redo experiments or merge archived audio WIP.
