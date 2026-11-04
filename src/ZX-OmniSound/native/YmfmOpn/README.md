# YM2203 FM bridge

The `ymfm*` source files are unmodified selections from Aaron Giles'
[ymfm](https://github.com/aaronsgiles/ymfm), commit
`81aec25ccbb98f4873a255f7551ac4dadac59b4a`, under the included BSD-3-Clause
license. `ymfm_opn_bridge.cpp` is ZX OmniSound-specific glue. The checked-in
`omni_opn.dll` is a 32-bit Windows build; it is used only by ZX OmniSound.

Rebuild with a C++14-capable Windows cross-compiler. For example, from the
ZX-OmniSound repository root with Zig 0.15.2:

```powershell
zig c++ -target x86-windows-gnu -shared -O2 -std=c++14 `
    -o native/YmfmOpn/omni_opn.dll `
    native/YmfmOpn/ymfm_opn_bridge.cpp `
    native/YmfmOpn/ymfm_opn.cpp `
    native/YmfmOpn/ymfm_ssg.cpp `
    native/YmfmOpn/ymfm_adpcm.cpp
```

The bridge returns only the FM DAC. The existing managed YM2203 SSG renderers
remain responsible for both AY-compatible three-channel sections. Native FM
timers and register state advance at 3.5 MHz; the FM output is averaged down
from the core's 3.5 MHz / 24 generation rate to the emulator's host rate.
