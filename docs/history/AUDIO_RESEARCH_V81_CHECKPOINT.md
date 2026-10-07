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
Continuation: user supplied exact PT3 and MIDI, and screenshots of BOTH outputs.
PT3 L:/Work_two/ZX/ZX_IMG/flash_pentevo/PT3/Lamer Pinky - Waiting 4 Robot (2023)
(FOReVER 2023 - ROBOT, 3).pt3 SHA EDA58C848BF383D4900196CBFC9D298232BEF45C2C6A2F9062D1C32F23A0E3EF.
MIDI .../MIDI_UNO/10.MID SHA E0E67D99AB2D1CC0749B6E403097EC2500CEBA53AEC7D3A329FDF7894C622C30.
User confirms current OmniPlayer1.0.41. gp.com copied from bin has this version.
Screenshots confirm UART AY2 on ORDINARY MultiSound Rev2 (NOT Max), AND MoonSound
/ OPL4. User says both are quiet. Do not replace Rev2 with Max in this test.

Media copied only bin/System/ini/root boot files, plus content-identical aliases
research.pt3/10.mid into researchRoot/media-copy; originals not altered.
ZXTune installed at L:/Work_two/ZX/ZX-Tune/zxtune123.exe r5100 May30 2025.
Reference WAVs generated with explicit AY1773400,44100,ABC,HQ; matched-pan
controlled-ay.conf uses ZXMAK2 coefficients100/10,66/66,10/100. No user config edit.

RealNedoProbe.cs DIAGNOSTIC ONLY extended with audio:name:frames: records all
raw source buffers and final mix, no production changes. Compiled in copied v81.
Test profiles real-player/moon.vmz, multisound-r2.vmz, ay.vmz, all guest sandbox.
Correct launch: no-trace key:D5 wait:300 key:F10 key:Return 'cd /'
'/bin/gp.com' wait:300 keys:DownArrow:7 key:Return audio:midi-moon:1500.
Root browser index7=10.mid, index8=research.pt3. Screenshot required to confirm.
Wrong initial key:S boots TR-DOS/Neo-DOS, NOT NedoOS; moon-start artifacts INVALID.
gp /10.mid in /bin opened browser, NOT music; moon-opl4 captures INVALID silence.
keys:Down invalid enum; moon-midi-valid failed. Use DownArrow. No source bug inferred.
moon-browser/root are navigation-only. Preserve/classify these, not music evidence.

CONFIRMED moon-midi-playing/13.png actual10.MID MoonSound/OPL4 playing, 1500frames
30sec recorded includes initial loading. Only Moon source audible, others silent:
raw AC RMS419.163 peak6580; final RMS101.259 peak1399 -> ~-12.34dB.
Cold real OS clock14MHz; no claim actual3.5/7MHz. media original preserved.
FINAL continuation: Rev2 actual UART AY2 confirmed r2-midi-playing/13.png,
9sources: MIDI AC1796.561 peak24935 -> mix194.591 peak2946, -19.307dB.
Two silent PSGs have -32768 DC, AC0. Do not sum/clamp before handling this DC.
Player SlowMidiUart changes actual CPU to3.5MHz, Moon test stays14MHz.
AY actual PT3 confirmed ay-pt3-playing/13.png,1500frames/30sec. Four sources.
Aligned28sec vs ZXTune: coarse20ms envelope offset2.600sec corr0.98603.
AC ZXraw9157.412, ZXmix1820.642, reference4596.125; ZXfinal8.044dB quieter.
Normalized FFT/Hann power <70Hz: raw26.26%, mix9.22%, ref25.73%. >15kHz
differences do NOT by themselves establish aliasing or subjective quality.
All details in AUDIO_RESEARCH_V81_REAL_PLAYER.md; generated WAVs/PNGs saved.
No live Windows audio/listening or production patch. Next: discuss calibrated
mix/DC fix and compatibility/rollback. Do not rerun completed cases without a
reason or implement fixes until authorized. Preserve original media and prior
four untracked engine-research files. Production src/submodule clean; all58
published v81 files in research copy still byte-identical after real captures.
Original music and gp.com hashes verified unchanged. Research-only source,
report, WAV/PNG/logs and local history export saved; no release built.
