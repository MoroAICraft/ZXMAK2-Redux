# ZX OPL3 (UR4QBP) native bridge

Standalone `ymfm::ymf262` at 14,318,180 Hz, native sample clock /288.
This does not replace or modify the MoonSound/Max/OmniSound native libraries.
Source dependencies are the unchanged Aaron Giles ymfm files in `../YmfmOpl4`;
their BSD-3-Clause license is `../YmfmOpl4/LICENSE`.

Build on Windows using `Build.ps1 -OutputDirectory <private build directory>
-Zig <path to zig.exe>`. The v97 reference toolchain is portable Zig 0.15.2,
target x86-windows-gnu, with static C++ support. Omitting `-Zig` selects an
alternative VS 2022 x86 C++ /MT build (not the v97 reference binary).
The bridge is x86 and imports the Windows Universal CRT and Kernel32.
No separately installed C++ runtime DLL is linked. Windows 10/11 include
the UCRT; older Windows installations are not certified by this release.
Bundled toolchain license notices are retained in `Licenses` and shipped
under `Docs/Licenses/OPL3-Toolchain`.

Board source: user-supplied ZX_OPL3.zip, schematic V1.0, 2026-03-18.
No sample ROM, bank file, GAL/CPLD firmware or Windows MIDI device is needed.
