# Keyboard v76 regressions

Build ZXVM.sln Release/Any CPU, then compile KeyboardRoutingProbe.cs and
PentEvoProfileProbe.cs with .NET Framework csc /platform:x86, referencing
System.Windows.Forms.dll, System.Drawing.dll and all output ZXMAK2*.dll.
Use absolute source paths (PowerShell/native argument slash ambiguity).
Copy ZXMAK2.exe.config to each probe's .exe.config beside unity.config.
Use the user's locally packed v75 ROMS.PAK; do not commit third-party ROMs.
Run probes from the runtime directory. KeyboardRoutingProbe: 483 assertions.

DirectKeyboard's internal injectable constructor tests the actual acquisition,
focus, reset, handle and disposal path using deterministic device callbacks.
HostKeyboardRouter tests each key/chord and UI ownership independently of
physical scan timing. Existing profile probe exercises real settings controls.

KeyboardPhysicalMonitor.cs is a standalone manual diagnostic form, using the
actual DirectInput reader at a 1ms WinForms timer cadence. It never injects input,
interacts with other apps or records keys outside its own foreground window.
It is not distributed in release archives. Computer Use taps were not observed
by DirectInput during v76 verification; do not claim that as a physical-key test.
