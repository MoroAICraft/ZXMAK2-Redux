ZXMAK2 Fork — ZX Evo BaseConf v67 (local test build)

This build fixes Host Folder SD startup of NedoOS: ordinary lowercase 8.3
host names now keep their correct uppercase FAT short names. BIN,
AUTOEXEC.BAT, WIZCFG.COM and NV.COM are readable under those names.

For a fresh BaseConf profile, Internal Music and Beeper start at 50% on
their normal 0–100% sliders. The BaseConf Covox slider remains centred at
100% on its 0–200% scale; its output calibration is 45% lower. Existing
saved slider positions are preserved. Other machines' Covox calibration
is unchanged.

Cursor arrows used in an open host menu are kept from the emulated machine.
After closing the menu, they become available again once released.

Host Folder SD is read-only. Refresh mounted folder updates its file list.
Media changes and ejection still perform the normal cold power cycle.

This is a local test build. NedoOS startup and audio quality need a real
user run. Nothing was published to GitHub.
