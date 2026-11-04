# ZX OmniSound

Included source snapshot for the ZX Evolution ZXBUS audio card. The card
combines the emulator's tested MultiSound Max YM/TSFM, SAA1099 and SoundDrive
paths with NeoGS (GS/MOD/MP3) and MoonSound (OPL2/3/4). It occupies one of the
two equivalent ZXBUS slots. The machine's existing Covox remains independent.
MIDI sent by ZX software over YM/AY port A is decoded by a bundled virtual
General MIDI synthesizer. This is not a bit-identical SAM2695 emulation and
does not make an OPL4 register interface into a MIDI input.

This snapshot is distributed with ZXMAK2-Redux 0.98. Original commit:
`08ce287380b29acf8abc1eebeeff6ffdc924e876`.

The integration source is compiled into ZXMAK2.Hardware by the ZXMAK2-Redux
integration branch. See [DESIGN.md](DESIGN.md) for port ownership, remaining
work, media/ROM policy and acceptance tests.
