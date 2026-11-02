# NMI v94 checkpoint — 2026-11-02

Authorized: fix CPU NMI prefix boundary on consolidated v93. PrintScreen stays
unchanged. Separate TEST package; v93 remains accepted working base until user
confirms v94. No installed emulator, media or BC Commander edits; no GitHub push.

Base: 7a7aadf97b9dd27acd851024be03280f2b5f5a7c, tag zxmak2-v93-working-base.
Branch: codex/nmi-prefix-v94. Package:
L:\Work_two\ZX\ZXMAK2-v94-NMI-Prefix-Test-20261102.
Private tests: D:\ZXMAK2_Work\nmi_v94_20261102 (baseline, candidate).
Complete v93 runtime ZIP saved in package/rollback. Four preexisting untracked
research files copied to checkpoints, excluded from changes.

Findings: Z80Cpu.ProcessSignals accepts NMI with FX/XFX active. EventManager
normally emits a one-cycle request, clearing its queue before acknowledgement;
PentEvo additionally holds its hardware request until ACK. A boundary guard alone
would lose the generic host request. Need a CPU pending latch, cleared by ACK
and RESET; do not gate on BINT (EI inhibits INT, not this NMI path).

Reference: Zilog UM008011-0816, page 6 (PDF page 19), NMI instruction boundary:
https://www.zilog.com/docs/z80/um0080.pdf . Existing EI behavior is preserved;
no wider interrupt/timing remodel is included.

Implemented in Z80Cpu.cs only: private _nmiPending plus prefix boundary guard,
reset/ack clearing. PrintScreen routing, EventManager and board logic unchanged.

Results so far:
- Untouched v93 fails all 261 NmiPrefixProbe cases. Corrected core passes
  261 cases / 12497 assertions, including actual EventManager one-cycle pulse.
- RunRegressions passes completely; ten no-NMI machine state/PCM/I/O hashes
  match v93 including 48/128/Pentagon/Evo 3.5/7/14 MHz and machine switching.
- Native archived BC v0.63 binary: all six prefix families at all three clocks
  pass menu entry and Escape resume with exact registers/eight RAM banks.
  Four ordinary/48K/interrupt-enabled cases pass on both baseline and candidate.
  Baseline intentionally fails native ED test: ACK at A0FF with XFX=Ed;
  candidate ACK at A100 with FX/XFX=None. Only the private diagnostic harness
  is instrumented, not Commander binaries/source checkout.
- Public CPU API unchanged. Raw IL hashes also differ where metadata tokens
  shifted after adding a private field; do not interpret these as extra logic edits.

Final verification: paired bounded speed checks show median changes
-1.77..+0.70%, CPU-only +0.21%; no material slowdown in these workloads.
The packaged native fixture and fresh-copy runner also pass their smoke test.
Product commit dd06c55. Final report: docs/releases/NMI_PREFIX_V94_REPORT.md.
Full/Clean packaging and source/history export use the established verified
scripts. Runtime differs from v93 only in CPU DLL and release documentation.
User desktop acceptance remains pending; do not promote over v93 automatically.
