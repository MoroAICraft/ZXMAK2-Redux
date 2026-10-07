# Safe optimization workflow

Run from the primary checkout, `codex/optimization-v80`, on Windows/x86 .NET.
Preserved control: `zxmak2-v79-control`; final runtime tag: `zxmak2-v80-optimized`.
Read `docs/LOCAL_VERSIONS.md` and `docs/history/OPTIMIZATION_V80_REPORT.md` first.

Scripts default to the separate v80 package on L:; they never target user D:
installations. Repeated exports/ZIP writes intentionally fail if output exists.
`PrepareRepository.ps1` and `ArchiveRedundantExtractions.ps1` were one-time
archive/organization steps, not everyday build commands; do not rerun blindly.

Build Release with VS BuildTools MSBuild (`src/ZXVM.sln`, Any CPU, /m:1).
The distribution preserves all v79 runtime files except rebuilt Engine DLL.
Use a fresh copy of the v79 runtime as `baseline-v79`, candidate as `release`.
Only copied/synthetic media may be used for diagnostics.

- `RunRegressions.ps1`: compile and run existing probes and bounded Test modes.
- `MeasureFinal.ps1`: compile equal harnesses, preserve prior paired logs, run
  reversed pairs pinned to affinity 1 in **child test processes only**, summarize.
  Do not run other builds/probes/profilers at the same time. Normal Test /perf
  and /cpu are infinite loops and use realtime priority; avoid them here.
- `OptimizationProbe.exe state <repo/src/Test> <candidate/ZXMAK2.vmz>`:
  compare all ten SHA256+I/O/tact fingerprints in baseline and candidate.
  Can compile/copy the bounded probe from MeasureFinal; `state` never uses
  the optional benchmark affinity. Production RAM randomness is unchanged.
- `tools/MoonSoundTest/RealNedoProbe.cs`: genuine ROM/NedoOS/player regression
  with emulated guest keys, on a **copied SD only**. Its README lists commands.
- `Package-v80.ps1`: file list from control, no helper binaries/diagnostics in
  ZIP, no personal VMZ/VMIDE/CMOS/user ROM in Clean; verify every file hash.
- `ExportSources.ps1`: committed source with pinned OmniSound contents,
  local history bundles for parent/submodule, verification and hash manifest.

Timing figures are workload-specific, not universal rendering/audio speed or
proof of all-machine compatibility. Diagnostic probes moved into the package's
diagnostics folder are archival binaries; recompile beside runtime DLLs to rerun.
Do not mix old/new individual device DLLs when rolling back: unpack full v79.
