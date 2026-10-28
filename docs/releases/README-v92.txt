ZXMAK2 v92 — BaseConf Prefinal TEST — 2026-10-29
Not an accepted replacement until user verification.

Ctrl+F toggles No Flick without changing the guest machine.
BaseConf File/Open, drag/drop and command-line/Open With accept TRD, SCL,
TAP and supported 48K/128K SNA. Other machines retain their own loaders.
Disk A is used if empty and not overridden by a virtual drive. Otherwise
select a physical drive. Dirty replacement offers Save & Replace / Discard
& Replace / Cancel. Saves preserve tracks in an internal UDI copy.
SCL opening inserts a separate disk, never appends to the mounted disk.
TAP feeds the tape input. In EVO Service use T. Tape load, with L. Emu tape
load OFF (ON selects files on the guest SD, not the host tape player).
SNA is restored without a subsequent reset.

Sound hardware changes Apply with an in-process machine power cycle (RAM
cleared; CMOS/media retained). Pure volume Apply leaves the machine running
from the same state. Failed live configuration reload attempts rollback.

Open With registration uses HKCU only; no default file association changes
or administrator rights. Each installation has its own path-derived entry.
Moving it requires one launch from the new path. Old entries can remain in
Windows; an unavailable old installation is not deleted automatically.
Open With forwards to the same running installation and queues during dialogs.

Own files stay portable. Windows registry/Open With is intentionally local
to the current PC/user. External SD-folder direct-save policy is unchanged.
No sound-core replacement, resampling filter or AGC is added by this build.
See Docs/V92_RELEASE_REPORT.txt for actual tests and limitations.
