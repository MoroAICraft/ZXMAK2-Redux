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

Next: bounded repeatable benchmark + deterministic state/video/PCM/I/O traces;
evaluate caching invariant interface references in BusManager. No timing/event
skipping, CPU opcode edits, audio algorithm changes or mutable clock-value caching.
