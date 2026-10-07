# v80 performance checkpoint — 2026-10-07

Part 1: primary checkout fast-forwarded from aaf78b7 to v79 (1993dd0).
Unpublished adaptive DirectSound edits preserved as cdb6fca on their own branch
and in L:/Work_two/ZX/ZXMAK2-Local-Archive-20261007, never applied to optimization.
Control tag/source archives/Git bundle preserved; five missing-worktree
registrations pruned, no real worktree/source/release/media removed.

Working root: L:/Work_two/ZX/ZXMAK2-Fork, branch codex/optimization-v80.
Release area: L:/Work_two/ZX/ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007.
baseline-v79 is a copied, immutable control runtime; release is the candidate.

WPR CPU profile could not start (0xc5585011: performance-profile policy privilege).
No elevation/bypass or system-policy change. Use bounded benchmarks and focused
diagnostics. Test /perf is an infinite loop; its isolated diagnostic PID 22592
was stopped deliberately after obtaining warm-up data (not a regression failure).
Early warm zexall 500-frame values ~343–360ms on this PC, not controlled paired
before/after measurements yet. Do not claim an improvement from these alone.

Part 2: only BusManager interface references/capabilities are cached. Rates,
mapping, time and hardware results remain live. Invalidation at add/remove/clear
and clock connect/disconnect preserves profile switching.
First bounded tests: ~12% gain in full/light Pentagon, CPU-only control unchanged.
Initial fingerprint differences were caused by production randomized power-on
RAM, present even with two identical v79 DLLs. Only disposable probe RAM is now
seeded deterministically; production power-on behavior is NOT changed.
All 10 state/video/PCM/I-O/tact fingerprints now match the v79 control, including
Spectrum48/128, Pentagon snapshots and same-bus Evo 3.5/7/14/switch-away/back.
No timing/event skipping, CPU opcode edits, audio algorithm changes or mutable
clock-value caching. Pinned OmniSound submodule initialized locally; full build
works from the now-current primary checkout.
Next: repeat paired performance, Evo CPU+audio workloads and complete regressions;
safe cleanup/archive of redundant extraction folders, final packaging.

Continuation: all 9 external regressions and all 7 built-in device tests PASS;
ordinary Test.exe/ULA/benchmark PASS. Tape mock updated for current ITapeDevice,
VideoTiming reflection setup mirrors Add's new cache (including legacy switch).
No production workaround for a failing test. Build PASS; candidate Engine DLL
equals rebuild SHA256 530D4D088E511710168F033B8F4EAF82D14E9A89EFB51EA2EB61FE6D7E9D434E.
SndOut struct experiment REJECTED: no stable benefit, Evo1 slowdown. Hardware
source restored; candidate Hardware DLL is byte-for-byte v79. Only Engine changes.
Five redundant verification extractions archived/verified then removed, recoverable
in archive/redundant-extractions; other backup/tmp/media/releases preserved.
Initial unpinned paired results varied strongly on hybrid i7-14700KF; do not use
as final claims. Prepare harness-only CPU affinity 1 and repeat after all other
CPU work finishes. Production emulator affinity/priority/settings remain unchanged.
