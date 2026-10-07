# Audio research checkpoint — 2026-10-07

User authorized FIRST stage of the four-part audio plan: research only.
No production sound edits, no new release, no archived audio-WIP merge.
User also reports very quiet MIDI on MoonSound/other supporting boards.
Asked for one ordinary single-AY/YM PT3 and optional ZXArt link; pending.
Do not invent a matched musical comparison while these are missing.

Baseline c2abe9292d465ed86f84c6f33b10f9f0363222d0 (v81), Omni submodule
0794d8d0e050c57dc66bde926639b0dc88ea42d9, production src clean.
Four untracked engine-research files existed before this task: preserve them.
Backups copied to L:/Work_two/ZX/ZXMAK2-Audio-Research-v81-20261007:
baseline-v81/release, source-backup/source (combined source + two Git bundles),
source-backup/previous untracked research files. User D: untouched.

Source-confirmed:
- FrameSound averages every renderer buffer, including silent sources.
- Some multi-board internals count individually (TSFM has four buffers).
- source gains, optional final RejectDc and master-volume limiter already exist.
- SoundDeviceBase already has continuous two-sample/64-phase filtered DAC
  integration; do not claim no filtering/no anti-aliasing.
- native PCM (MoonSound, MIDI synth etc.) is fed through the same DAC stage.
- final HPF fixed 0.995/0.99 coefficients, enabled by ANY device opting in,
  hence also affects the other sources. Frequency response needs measurement.
- WinForms default DirectSound 44100Hz 16-bit stereo, four20ms buffers;
  copy path not an EQ. Existing startup clear/underrun-to-zero ramp retained.
- archived adaptive output is unfinished, not merged; no live underrun rate
  established. Native Windows output and XNA not yet measured.

ZXTune primary sources examined at immutable commit
5e33fb833741b3d1691625d8af1acbe17020afa5. AY/YM amplitude tables match the
corresponding ZXMAK2 tables; pan/gain/interpolation can differ. Its optional HQ
AY renderer filters chip-rate output with second-order IIR cutoff9500Hz;
this is NOT a universal post-effect that should be copied over all chips.
Read src/devices/details/renderers.h, aym/src/renderers.h, volume_table.h,
sound/impl/mixer_core.h, sound/impl/resampler.cpp, sound/lpfilter.h.
No claim about user's installed ZXTune interpolation settings yet.

COMPONENT RESEARCH COMPLETE: 43 checks PASS, components.txt. Mixer one source
plus silence attenuates6/12/18dB at2/4/8 sources. DC blocker50Hz -4.758dB at44.1k
and -10.185dB at96k. Same-rate native PCM through DAC gives -2.425dB at10k,
-6.358dB at15k: not automatically proof that physical filtering should vanish.
DirectSound actual copy method bit-exact; simulated underrun ramps to zero.
NO live host output or actual MIDI file/music reference comparison yet.
GM native synth works; warmed raw peak6906/DAC6895, not Moon OPL4 MIDI test.
Real CPU IO-driven Moon PCM waveform0 with Volume0/50/100 gives identical RMS
5745.125, peak8125 and hash9F3EE7BD381A9180: Volume not applied in Moon renderer.
Inventory current TSFM+Moon8, Moon-only music4, Max10, Omni11 sources (including
beeper/Covox/tape). These are sandbox profiles, not user's current installation.
All58 original runtime files still byte-identical to published v81 after probes.

Full report AUDIO_RESEARCH_V81.md and reusable RunAudioResearch.ps1 saved.
New local research commit/branch will contain only audio research docs/harness;
prior four untracked engine-research files remain preserved and uncommitted.
NEXT REQUIRED: user's ordinary single-AY PT3 and optional ZXArt link/settings;
for quiet MIDI, a MIDI file plus player/selected sound device. Do not claim a
matched musical diagnosis or implement the audio plan until context is supplied
and user approves/continues into implementation. Do not repeat component probes
without a reason. No production sound, timing, input, media, or new release changes.
