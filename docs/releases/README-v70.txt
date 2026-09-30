ZXMAK2 Fork — ZX Evo BaseConf v70 (local NedoOS folder test build)

This build follows v69 and adjusts the FAT32 card presented by a mounted
Windows folder to match ordinary writable SD images more closely.

- Simple lowercase 8.3 names such as bin/autoexec.bat now use the standard
  FAT case flags instead of unnecessary long-filename entries.
- Files in the mounted folder are no longer marked read-only on the virtual
  SD card. Guest changes still remain in a recoverable session until you
  confirm saving them to the original folder.
- The Beeper volume starts at the exact 50% slider midpoint. Existing saved
  machine settings retain their chosen volume.

Automated tests cover FAT metadata, NedoOS startup files, and saving deletion
of a pre-existing file. The actual NedoOS startup still needs a manual test.
Please test with a copy of the SD folder first. The accepted v68 and the
previous v69 package remain untouched. Nothing has been uploaded to GitHub.

Run Test.exe /hostfoldersd for writable SD regression checks, or run
Test.exe /hostfoldersd <folder> to also audit startup files in a NedoOS
folder. Run Test.exe without arguments for the engine benchmark.
