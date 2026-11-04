# ZX OmniSound design and work log

## Agreed scope

One ZXBUS card in either slot: 2 x YM2203, SAA1099, SoundDrive, NeoGS and
YMF278B/OPL4. The existing machine Covox is not duplicated. The MultiSound
Max GS CPU and separate OPL3 renderer are not active inside OmniSound: NeoGS
owns #B3/#BB, and one OPL4 core owns both FM and PCM ports.

| Function | Host interface | Owner |
|---|---|---|
| AY/TSFM | AY-compatible #FFFD/#BFFD decode | MultiSound Max YM path |
| SAA1099 native | low byte #FF, A8 selects register/data | one SAA core |
| SAA1099 TSFM mode | TSFM selector then #FFFD/#BFFD | same SAA core |
| SoundDrive | #0F/#1F/#4F/#5F family | four DACs |
| GS/MOD/MP3 | #BB/#B3/#33 and NeoGS internals | NeoGS |
| OPL3 FM | #C4/#C5/#C6/#C7 | OPL4 FM half |
| OPL4 PCM | #7E/#7F | OPL4 PCM half |
| Pentagon Covox | #FB | machine's existing device, outside card |

These are interface families, not a claim that every alias has been tested.
The SAA dual-port mode is an OmniSound convenience, not an exact ZX-MultiSound
Rev.A2 hardware claim.

MIDI is assigned to MoonSound's YRW801-M General MIDI instrument bank. Playing
a MIDI file still requires a MIDI parser/driver that turns its events into
YMF278B register operations. This driver is planned and must be proven with
real files before MIDI support is advertised as working.

## Integration rules

* A ZXBUS selection consumes one slot regardless of the number of internal
  sound engines.
* The internal Music choice must be None when OmniSound's YM path is active.
* Separate NeoGS, MoonSound, MultiSound or MultiSound Max cards sharing these
  ports are rejected, even if one physically fits in the second slot.
* Existing standalone boards and known stable builds are never overwritten.
* The card's NeoGS SD media command and mount state must remain available.

## ROMs and native libraries

NeoGS ROM 1.11 is already embedded in the host emulator. MoonSound needs the
user-supplied 2 MiB YRW801-M ROM. The accepted SAASound.dll is a local x86
binary. Its exact provenance and redistribution rights must be verified before
any public GitHub release. No copyrighted ROM is committed here.

## Acceptance sequence

1. Build, slot 1/2, XML round-trip, reset and conflict checks.
2. SAA: arcane2.trd and 1099test.trd without a compatibility checkbox.
3. SoundDrive: Freestyle; AY/TSFM: trusted music with both YM chips and FM.
4. NeoGS: known GS/MOD/MP3 files and SD-image persistence.
5. MoonSound: OPL3 FM, OPL4 PCM and real MIDI via a driver.
6. Compare loudness with the accepted boards, then tune levels without clipping.

## 2026-09-26

Research and local repository established. Current integration is experimental;
this document distinguishes implemented paths from the remaining MIDI and
listening acceptance work.

### Integration checkpoint

The isolated emulator branch is `codex/zx-omnisound-integration`, based on
`c1baa59` (v59). The original checkout and stable portable releases are intact.
The local repository is linked as a submodule; its local URL is deliberately
not suitable for public distribution yet.

Implemented: one card in slot 1 or 2; automatic internal Music suppression
and restoration; persistent slot and NeoGS SD path; one SAA instance accepting
native and TSFM writes; no duplicate MultiSound GS RAM or separate OPL3 core.
Standalone NeoGS, MultiSound, Max, MoonSound and internal AY/TSFM conflicts
are rejected before this card subscribes to bus ports.

Automated x86 probes passed:

* OmniSound: independent YM D1/D2; native SAA peak 8163 and TSFM SAA peak
  5223 after separate resets; SoundDrive peak 16128; OPL FM peak 2880;
  OPL4 ROM PCM peak 8124; NeoGS command pending/consumed handshake.
* Configuration UI: both slots, moving the same card without losing its SD
  path, Music restoration, SD panel availability, and XML persistence.
* Regression probes: TSFM, MultiSound A2, MultiSound Max, MoonSound and
  Master Volume passed. Max needs its own external GS firmware for its test;
  OmniSound does not use that firmware.

These signal peaks prove activity, not equal loudness or listening quality.
The existing final mixer averages every configured renderer, including silent
ones. OmniSound currently exposes seven renderers (its DAC plus six additional
sources); loudness calibration remains pending. Do not blindly boost each
renderer: the legacy YM output probe can already reach the Int16 limits.
MIDI-to-OPL4 driver integration and real-file listening tests remain pending.
No stable release designation and no GitHub publication have been made.

### Audio audit and improvements

* OmniSound requests 140% output gain. The emulator applies it in wide
  accumulators before final conversion; unrelated devices retain 100%.
  A soft knee limits the mixed result, not individual component sources.
  Master Volume remains independent. Synthetic cancellation, signed-range
  and low-level linearity tests pass. This is electrical signal gain, not a
  promise of 40% greater perceived loudness on every track.
* SAA oversampling API correction, OmniSound only: `SetOversample(6)` gives
  64x. The historical value 64 is retained on accepted standalone boards.
  Upstream documents the exponent in `src/SAAFreq.cpp` and uses
  `1 << m_nOversample` in `src/SAAImpl.cpp`:
  https://github.com/stripwax/SAASound/blob/master/src/SAAFreq.cpp
  https://github.com/stripwax/SAASound/blob/master/src/SAAImpl.cpp
* OmniSound SAA data writes call `SAASNDWriteData`, preserving the separate
  address pulse. Previously repeating address/data could advance an externally
  clocked envelope twice. Full register helper writes retain address/data API.
* Measured SAA tone: 326.5 Hz vs 326.371 Hz theoretical at 8 MHz, octave 3,
  offset 128. Two seconds of captured samples; tolerance below 1 Hz.
* YM SSG: all 16 envelope shapes checked for 96 steps each, PASS. This is a
  regression/shape check, not an analog measurement of real silicon.
* Important remaining limitation: the inherited YM2203 FM engine is a
  simplified managed synthesizer, with approximate envelope rates, rather
  than a cycle-accurate OPN implementation. Keep this visible in acceptance.
* MoonSound's native output is fixed at 44.1 kHz. Non-44.1-kHz host output
  requires a separate resampling audit before being advertised as supported.
  No sample-rate or oscillator-clock UI option was added in this checkpoint.

Repeated OmniSound, standalone sound-board and configuration probes passed
after these changes. MIDI integration and subjective listening remain open.

### Follow-up: rate adapter and SAA regression

OmniSound's MoonSound component now enables a causal linear output adapter
when the host rate differs from the native 44.1 kHz. It preserves phase across
frame/register-write chunks and clears state on reset/reconnect. No future
native samples are generated across register writes. The standalone MoonSound
path remains unchanged. Native oscillator clocks are not altered.

The integrated probe passes at 44.1, 48 and 96 kHz: the OPL FM test tone remains
1233 Hz and SAA remains 326.5 Hz (two-second measurements), with audible PCM,
NeoGS handshake and conflict checks intact. Linear interpolation is not a
high-order band-limited converter; lower output rates have not been accepted.

External SAA envelope bus cycles are compared byte-for-byte with direct DLL
calls for 40 pulses. Corrected SetData passes; deliberately repeating the
address pulse fails that comparison. Internal DLL high-pass is disabled only
during this reference test to avoid its shared filter history affecting bytes;
normal playback parameters are restored afterwards.

**Required later migration:** apply the oversampling exponent and separated
SAA address/data corrections to TurboSound FM Pro, MultiSound Rev.A2 and
MultiSound Max, in a dedicated change with this regression probe and listening
checks using arcane2.trd / 1099test.trd. They have NOT been changed yet.

### Follow-up: isolated YM2203 FM upgrade

OmniSound alone now selects an Aaron Giles ymfm YM2203 OPN FM core. The exact
upstream commit and rebuild instructions are in `native/YmfmOpn/README.md`;
the selected upstream C++ sources are unmodified and keep their BSD license.
The Windows x86 DLL is included with the local integration. Both YM2203 chips
have separate native state, FM timers and registers. Their three-channel SSG
parts still use the accepted managed renderer and are not doubled in the mix.
Other TSFM and MultiSound cards deliberately retain their old FM engine.

The dedicated probe exercises both FM chips, timer overflow/clear and a
229 Hz note at 44.1, 48 and 96 kHz (the computed note is about 228.9 Hz).
It also exposed a YM2203 hardware nuance: the high frequency byte is latched
and becomes active on the *subsequent low-byte write*. The test now uses that
ordering. All previous OmniSound, TSFM, MultiSound A2/Max, MoonSound and
Master Volume probes pass after this substitution. These probes do not prove
all operator algorithms/envelopes against physical YM2203 captures, and the
small synthetic FM note is not a loudness or subjective listening test.

The three YM2203 prescaler address commands now update OmniSound's managed
SSG clock too: #2D restores 1.75 MHz, #2E selects 3.5 MHz when the previous
prescale is six, and #2F selects 5.25 MHz. The two chips keep independent
prescale states. Native ymfm already applies the FM-side prescaler. A separate
probe verifies the SSG clock change and reset without changing legacy cards.

Remaining: MIDI-to-OPL4 driver, real-file listening/level calibration, old
card SAA migration, and packaging/public remote preparation.

### MIDI boundary and driver source

The YRW801-M contains General MIDI tone data, but Yamaha explicitly notes
that it **needs control software**. The ROM and YMF278B do not decode a `.mid`
file by themselves. ZXM-MoonSound Rev.01 exposes YMF278B register ports, not
a MIDI file/byte-stream interface. Therefore merely adding the chip and ROM
cannot make MIDI files playable from BaseConf. No undocumented MIDI support is
claimed for the current card.

References:

* [Yamaha YRW801-M datasheet](https://www.opl3.com/wp-content/uploads/2024/02/YRW801-M.pdf)
* [Yamaha YMF278B datasheet](https://map.grauw.nl/resources/sound/yamaha_ymf278b.pdf)
* [Linux YRW801 tone-region mapping](https://github.com/torvalds/linux/blob/master/sound/drivers/opl4/yrw801.c)

The Linux mapping provides one possible openly licensed reference for GM
program-to-ROM-wave regions; its header offers a permissive redistribution
option with attribution. A driver still has to parse MIDI timing, allocate
at most 24 PCM voices, apply per-channel program/volume/pan/sustain and write
the documented MoonSound registers. The input path belongs in ZX-side player
software (e.g. a future commander), unless the user explicitly chooses a
host-side file loader. Do not invent a new virtual UART/port just for this.

### 27 September 2026: MIDI via existing YM serial output

The [ZX MIDI Player](https://github.com/UzixLS/zx-midiplayer) source provides
the missing software-side evidence: its AY output driver writes a 31,250-baud
serial stream to YM/AY register 14, port A bit 2, selecting chip 1 or 2 via
`#FFFD` and sending data via `#BFFD`. MultiSound's original external MIDI
synthesizer receives this line; it is not the MoonSound OPL4 register path.

The emulator now decodes that existing line for MultiSound Rev.A2, Max and
OmniSound. A separately licensed GeneralUser-GS SoundFont and vendored
TinySoundFont core render General MIDI through the normal sound mixer. This
does not introduce a new ZX-visible port and does not claim exact SAM2695
timbres. Both YM serial outputs are tested independently with a synthetic
status/note-on stream; all three boards produce PCM. Real ZX player listening
and volume matching remain user acceptance work.

The inherited Max OPL3 and OmniSound OPL4 direct register probes both produce
FM audio; standalone MoonSound also passes its FM/PCM probe. One NedoOS VGM
player reports no MoonSound even for the working standalone card, so its
device-detection message is not a reliable OPL regression test. OPL playback
from a known compatible real player/file is still pending. Rev.A2 has no OPL
chip to detect. No speculative OPL port/firmware identification was added.

SAA1099 oversampling exponent 6 (64x), and separate address/data writes for
externally clocked envelopes, have now also been applied to TurboSound FM Pro
and both MultiSound variants. Their direct and envelope regression probes
pass; subjective listening remains pending.

### OPL3 compatibility with the accepted MultiSound Max

User listening confirmed that v61 MultiSound Max plays VGM through NedoOS,
while OmniSound does not. The FM port implementations differ in two ways:
Max decodes #C4-#C7 regardless of DOSEN and consumes the YMF278B-only initial
status ID at creation/reset. OmniSound inherited the physical MoonSound
decoder with DOSEN gating and the unconsumed initial ID.

OmniSound now selects Max-compatible FM decoding and reset status on its
shared YMF278B core. Max and MoonSound use the same four FM ports and the same
native FM engine; a single core also preserves register #105 enabling OPL4
and the MoonSound PCM/ROM/RAM interface. This keeps one owner per FM port and
voice. Standalone MoonSound retains its original firmware behavior.

The OPL renderer is already included in OmniSound's mixer-level gain of 140%
(a 40% increase); no second gain stage is added. The comparison probe runs
the same OPL detection and two-bank stereo note stream on Max and OmniSound
at 44.1 kHz, including DOSEN false/true, and compares status and PCM exactly.
Real-file NedoOS playback remains the final user acceptance test.
