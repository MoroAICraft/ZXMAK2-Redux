ZXMAK2 Fork — ZX Evo BaseConf v69 (local writable SD test build)

This build adds writable sessions for the Z-controller SD card. Changes made
by Spectrum software remain in a recoverable session until you confirm them.
This applies to both a mounted Windows folder and an SD image.

- Mounting a host folder now presents a writable FAT32 card to the machine.
- On eject, replacement, folder refresh, or emulator exit, choose Yes to save
  changes to the original folder/image, No to discard them, or Cancel to keep
  the card mounted.
- A cold reset keeps the current SD session. An interrupted session is offered
  for recovery at the next start.
- The SD menu marks unsaved changes. A folder session can be exported as a
  complete SD image if its guest filesystem cannot be saved as Windows files.
- Windows folder files are checked for external changes before saving. If a
  conflict or invalid guest filesystem is found, the source stays untouched
  and the session remains available.

This is a local test build. Please check NedoOS boot and ordinary file saves
on a copy of your SD folder first. The accepted v68 build is kept separately.
Run Test.exe /hostfoldersd for writable SD checks or Test.exe for the engine
benchmark. Nothing has been uploaded to GitHub.
