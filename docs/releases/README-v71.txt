ZXMAK2 Fork — ZX Evo BaseConf v71 (local NedoOS folder capacity test)

This build follows v70. A mounted Windows folder is now exposed as the next
standard SD-card capacity instead of an arbitrary calculated size. The current
NedoOS collection is therefore a 4 GiB virtual SD card. No 4 GiB image is
created and source files remain in the original folder.

This avoids an unusual 2.2 GiB card geometry that may confuse older NedoOS
drivers. FAT32 entries, startup files, and writable SD sessions retain the
v70 fixes. The Beeper slider still starts at 50%.

Please mount the same folder and start sd_boot.$C. If the prompt still reports
"Unknown command", type these lines separately and note the first failure:

    wizcfg.com -S
    cd ../nedogame
    nv.com

This is a local test build. v68–v70 remain unchanged. Nothing has been
uploaded to GitHub.
