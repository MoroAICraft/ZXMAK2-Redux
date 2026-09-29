ZXMAK2 Fork - ZX Evo BaseConf v64 local test

This build is based on the user-tested v62 sound configuration and the v63
packed-ROM/default-device work.

New in v64:
- Mount a local Windows folder or accessible UNC folder as the main SD card.
- The folder appears to ZX Evolution as a read-only FAT32 card.
- Files are read directly from the folder; no full temporary image is created.
- Long file names, nested folders and automatically generated 8.3 aliases are
  supported.
- Use the SD arrow menu:
    Mount folder as SD...
    Refresh mounted folder
    Eject SD
- Refresh rebuilds the directory snapshot after files are added or removed in
  Windows. Use the usual cold start/re-read workflow afterwards.
- Machine Settings -> SD Z-controller shows Image or Host Folder and its path.
- Covox volume is restored to the same default level as stable build v62.

Limitations of this first version:
- Host Folder SD is read-only.
- Changes are not monitored live; use Refresh mounted folder.
- A single file of 4 GiB or larger cannot be represented by FAT32 and is
  skipped with a warning.
- Reparse points, junctions and symbolic links are skipped.

Packed ROMs:
- ZX Evolution BaseConf
- Santaka 002
- MoonSound / OmniSound instrument ROM

This is a local test build. GitHub has not been changed.
