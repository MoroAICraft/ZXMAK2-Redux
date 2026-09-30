ZXMAK2 Fork — ZX Evo BaseConf v68 (local test build)

Corrected v67 package. The emulator executable is marked 32-bit as required
by its native dependencies, and the separate Test.exe benchmark is included
with its configuration file. The source changes from v67 are retained:

- Host Folder SD exposes ordinary lowercase Windows 8.3 names to NedoOS
  as BIN, AUTOEXEC.BAT, WIZCFG.COM and NV.COM.
- Fresh BaseConf profiles start Internal Music and Beeper at 50%.
- The BaseConf Covox output calibration is reduced by 45%; its slider remains
  centred at 100% on the 0–200% scale.
- Cursor arrows used in an open host menu no longer reach the machine.

Run Test.exe for the engine benchmark, or Test.exe /hostfoldersd for the
virtual FAT32 check. Host Folder SD is read-only. This build is local only.
