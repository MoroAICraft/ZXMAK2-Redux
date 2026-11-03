# v98: sound-card gain and volume audit

Date: 2026-11-03. Accepted reference: `zxmak2-v97-working-base`.

Scope: card renderers and their volume propagation. The common mixer/PCM reconstruction audit is separate. No installed emulator or original musical media was changed. Synthetic renderer tests are not equivalent to launching OmniPlayer under NedoOS.

## Confirmed defects and narrow corrections

### 1. Composite board Volume had two unrelated states

`ZxMultiSoundCore` hid `SoundDeviceBase.Volume` with a private field. Assigning through `ISoundRenderer` changed only the base field. Its concrete setter enumerated only the base renderer array; OmniSound's additional MoonSound and NeoGS were omitted.

Before: interface Volume=50 produced concrete Volume=100, base=50, every child=100. Direct Omni Volume=0 still left MoonSound=100 and NeoGS=100. The same OPL tone at requested 100/50/0 had identical AC RMS 2029.030756 and SHA256 `761E8DB71C8643BB851CF6F0C9442C993AF4B3A46E849B81C1DA6EEA2A2B593D`.

Correction in `src/ZXMAK2.Hardware/Evo/ZxMultiSoundDevice.cs`: use base Volume as the sole state and propagate configuration changes through virtual `SoundRenderers`, including Omni children. Guard constructor-time null fields. XML serialization and public configuration shape are unchanged.

Volume-only test results, before the separate native PCM reconstruction correction:

| Host Volume | Omni OPL AC RMS | Result |
|---|---:|---|
| 100 | 2029.030756 | Exact accepted-v97 PCM SHA256 above |
| 50 | 1014.286164 | Every L/R sample equals integer half of 100% |
| 0 | 0 | Every sample zero |

Concrete/base/interface setters, clamping to 0..100, XML load/save, initialization and live updates were exercised on MultiSound A2, Max and Omni. Existing TSFM and Extreme child propagation passed. Muting/unmuting Omni did not freeze its native chip: final PCM equaled continuously running control.

### 2. NeoGS ignored inherited host Volume

`NeoGsDevice` never used inherited host Volume; its eight hardware channel volume registers are unrelated. A known DAC+MP3 waveform injected through actual `OutputMixed`/frame rendering gave the same AC RMS 6407.936021 at 100/50/0.

Correction in `src/ZXMAK2.Hardware/Evo/NeoGsDevice.cs`: scale the completed signed PCM frame by host Volume once. Bypass at 100%; emulated CPU, DAC, MP3 and reconstruction keep advancing while muted.

Final isolated-input test: 100% remains SHA256 `91C1EC51817901E09B03496746A157150995F34EC34EB59E5FA732895E327161`; 50% RMS 3203.767183 and every sample exact integer half; 0% zero. This injected-input test does not claim real MOD or native MP3 decoder validation. A separate live execution comparison reached identical NeoGS CPU tact 5452966, PC 0276 and final PCM hash with/without ten muted frames. Logs `neogs-isolated-baseline.txt` and `neogs-isolated-candidate.txt` compare original accepted v97 DLL directly against final candidate. The test deliberately advances/suppresses the card CPU and timed DAC when manually injecting known mixed samples, preventing their pending events from contaminating that unit input. Earlier nonisolated probe logs contained synthetic out-of-order DAC warnings and are superseded for these numeric measurements.

### 3. Max YMF262 inherited an OPL4-only FM attenuator

Primary source: user archive `K:\Download\USER_ZxMultiSoundMax.zip`, `Schematic_ZX_Multisound_Max.pdf`, sheet 1/1, UR4QBP / Alexander Karnaukh, revision V1.0 updated 2026-08-12. The schematic explicitly has U24 YMF262-M, U25 YAC512-M and X2 14.318 MHz. It does not have a YMF278B/OPL4 PCM mixer. CPLD source `cpld/fw_ms_opl3_cpld_saa_fix/rtl/top.v` decodes OPL ports C4-C7 only and routes A0/A1.

The schematic was rendered and visually inspected in full; read-only copy is retained at `D:\ZXMAK2_Work\audio_v98_20261103\card_levels\references\Schematic_ZX_Multisound_Max.pdf`.

Max's managed adapter reuses the FM engine inside existing `ymfm_opl4.dll`, without changing the native library. Native `ymfm_pcm.cpp` reset sets F8=1B, which selects FM mix level 3 on each channel. `ymfm_opl.cpp` uses scale 0x2D2/2048 = 0.3525390625. Standalone YMF262 has no such register. This caused approximately -9.06 dB of unintended Max attenuation before the shared mixer.

Correction only in `src/ZXMAK2.Hardware/Evo/ZxMaxOpl3Renderer.cs`: after create/reset temporarily enable NEW2 via register105=02, set hidden PCM F8=00, restore105=00 and FM address00, consume the OPL4-only chip-ID read. No guest PCM ports are added, native cores remain unchanged, real MoonSound and Omni's real OPL4 mixer defaults are untouched. This is not a compensating PCM amplifier.

Four identical FM register vectors in the pre-PCM-path private runtime:

| Vector | Standalone OPL3 RMS | Previous Max RMS | Max with corrected initialization RMS |
|---|---:|---:|---:|
| 0 | 5756.313431 | 2029.060096 | 5738.842925 |
| 1 | 2910.708148 | 1026.211760 | 2902.427351 |
| 2 | 1438.411217 | 507.404139 | 1435.143930 |
| 3 | 745.248961 | 262.803684 | 743.287289 |

These are not asserted bit-identical to standalone: the reused OPL4 core has full-scale mix coefficient 0x7FA/2048 and a different sampling path. The remaining roughly 0.3% amplitude difference is consistent with that coefficient. Initial status00, timer A C0, timer clear00 and repeated reset00 passed for each. Initial test: 56 assertions. Later candidate verification adds production F8 readback and reset inside partial frames.

`src/Test/Program.cs` `/omni-opl` used to demand exact Max/Omni PCM equality. It now requires exact status equivalence and the expected stereo waveform ratio 722/2042, with two low-gain LSBs of quantization tolerance. Unmodified Max attenuation fails the new relation. Real OPL4 F8 remains guest-controlled.

## Gain map (before final common master/limiter)

| Route | Existing stages | Assessment |
|---|---|---|
| TSFM/Multi/Max/Extreme PSG | AY/YM table and stereo weights; host Volume once | No new board-specific gain added |
| YM2203 FM on these boards / Omni | Each native chip produces short PCM; sum two chips; host Volume; Clamp16 | Two-chip sum can saturate before common mixer; see below |
| SAA1099 | Native SAASound64x PCM; host Volume; existing 1.5 output scale | Extreme stress tested below; no arbitrary change |
| Standalone UR4QBP OPL3 | YMF262 core; host Volume once | No OPL4-only mixer |
| MultiSound Max OPL3 | Reused OPL4 FM core; corrected hidden F8=00; host Volume once | Model initialization defect fixed |
| MoonSound / Omni OPL4 | Hardware OPL4 FM+PCM mixer registers; host Volume once after complete frame | Defaults preserved, not normalized by file format |
| MultiSound MIDI UART | YM A2 at 31250 baud; virtual GM synth; native -4 dB; host Volume once | Different synthesizer/instruments from Yamaha ROM; not physical SAM2695 identity |
| Multi GS + SoundDrive | Shared four hardware DACs, `(sample-128)*(volume&63)*2`, two per side | Legal maximum magnitude ~32256, no full-range overrun |
| Extreme SoundDrive | Two unsigned samples per side, offset and scale64 | Different real board; not forced to equal Multi |
| NeoGS | Existing board DAC1.5 and MP3 1.4, mixing, host Volume after frame | Host slider fixed; board transfer not recalibrated |

The modern shared mixer has fixed headroom rather than division by renderer count. Omni's existing `OutputGainPercent=140` is an owner-level factor applied to its children as well. Neither choice was changed in this card-level audit. Native PCM reconstruction changes are described by the common-path audit; do not confuse them with these gain corrections.

UART MIDI uses `omni_midi.dll` / vendored TinySoundFont v0.9 (upstream commit `853a0a171759f1ddba0de1442133a75912bbeffa`) and `GeneralUser-GS.sf2`, with `tsf_set_output(...,-4.0f)`. MIDI velocity, CC7 and CC11 affect amplitude inside the synth. It is a virtual GM replacement, not an exact SAM2695 model. Quiet MoonSound MIDI versus UART MIDI alone does not prove a common-mixer attenuation bug: ROM patches, voice allocation and guest mixer settings differ.

## Findings not indiscriminately fixed

### Two-YM2203 pre-mixer saturation

The current signed16 renderer contract clamps the sum of the two physical YM2203 PCM streams before the common wide mixer. This is distinct from the physical per-chip digital saturation in the native core.

Synthetic maximum-level algorithm7 tests at 44100 Hz:

- One chip, one channel, TL0: chip peak32704, no two-chip sum clipping.
- Two chips, one channel each, TL0: sum peak65408; 5856/8820 samples exceed signed16.
- Two chips, three channels each, TL16: sum peak49024; 4697/8820 exceed signed16.
- Quieter TL40 vectors do not clip and produce the expected summation.

Bounded register replay of four existing user fixtures (not full guest playback):

| Fixture | Samples | Per-chip peak | Sum peak | Per-chip rail / sum clamp |
|---|---:|---:|---:|---:|
| Taotao10.tfd | 2032128 | 16896 | 30824 | 0 / 0 |
| Psycho_Fox_level1_v7.tfd | 3161088 | 15680 | 16656 | 0 / 0 |
| Taotao10.tfc | 2033892 | 16981 | 30836 | 0 / 0 |
| sonic_title.tfc | 552132 | 10464 | 10464 | 0 / 0 |

All 7,779,240 samples stayed in range. Existing validated TFD/TFC decoders from `tools/SoundCards/MusicFixtures.cs` were reused; original files were read-only.

Safe architectural options for a later dedicated task: expose each chip as an independent signed16 renderer and sum only in the common wide mixer, or introduce a wide internal PCM contract. Halving before short conversion and boosting the mixer source later adds quantization and requires careful gain ownership; it is not automatically transparent. Removing Clamp16 without changing the contract wraps samples and is wrong. Globally lowering the card makes it quieter and is not a correction of the mixing boundary. No YM2203 core or summation architecture was changed here.

### SAA gain alone is not evidence of clipping

Six maximum-amplitude SAA tones gave native peak17347, scaled output peak26010, zero rail samples. One/three-channel peaks were2920/8703 before scale,4377/13048 after. The existing1.5 multiplier has not been removed merely because a Clamp16 helper exists. These stimuli do not establish the absolute analog voltage of a physical board.

### Held MultiSound DAC host Volume

A targeted port test on A2/Max/Omni found that raw-DAC change caching ignored host Volume changes: port0F=C0 gave peak8064, remained8064 after host Volume0 and after a repeated C0 write; changing the DAC byte to C1 finally produced zero. This is a pre-mixer held level (DC), not a claim about loud music; shared DC rejection reduces steady DC. It nevertheless shows stale host-mute behavior.

Corrected in `ZxMultiSoundDevice.cs`: cache the host Volume as well as the raw DAC value; when Volume differs at the next frame boundary, feed the retained hardware sample through the existing pre-DAC gain and reconstruction path. Do not change hardware sample registers or move the gain after clipping. With unchanged Volume no extra transition is added.

Candidate results on all three boards: 100% peak8064, muted0, repeating the same guest value while muted0, 50% peak4032. Writing C1 while muted still changes the hardware state; unmute produces8190. A timed real-Z80 OUT(0F),A loop at100% remains bit-identical before/after this correction: SHA256 `68886C686FB5C009E22D665274335AEFD6ED90E42EFB60C416F4A3E37E5B1F74`, AC RMS9186.366609. Held-DAC suite:15 assertions PASS.

## Reproducibility and boundaries

Diagnostics: `tools/AudioV98/CardLevelProbe.cs`, `tools/AudioV98/OpnMixAudit.cs`. Build x86 with `src/PortableBootstrap.cs` and accepted portable managed dependencies. `OpnMixAudit` also links `tools/SoundCards/SoundCardsProbe.cs` and `MusicFixtures.cs`, selecting its own entry point; it does not execute the benchmark main.

Private runtime and logs: `D:\ZXMAK2_Work\audio_v98_20261103\card_levels`. Key logs: `baseline-detail.txt`, `candidate-volume-progression.txt`, `max-model-before.txt`, `opn-music-levels.txt`, `held-dac-before.txt`. Volume-only suite: 88,256 assertions passed before final native PCM-path changes. Its 100% baseline hashes intentionally apply to volume-only isolation; the common PCM reconstruction fix legitimately changes native PCM hashes.

Final candidate Hardware SHA256 tested here: `EF1D071E4444B45299887310FE610C0530F80A5B1ABA39A21D09E2E88E7A8951`.

Candidate invocation: `CardLevelProbe.exe --expect-fix --skip-v97-hash` passed88,258 assertions; model tests `--max-model --expect-max-fix` passed96; held-DAC `--held-dac --expect-held-fix` passed15. Final logs are `candidate-final.txt`, `max-model-final.txt`, `held-dac-candidate.txt`. Standalone OPL3 produced exact identical PCM with and without an actively rendering MultiSound; the latter's Volume did not alter the former and OPL timer status remained correctly routed.

## Relocation / other-machine smoke

An independent copy was made under `card_levels\smoke\Moved # Кириллица v98`, with final Hardware/Host, no original user media and no inherited Data directory. Tests ran with current directory `C:\Windows`, not the emulator directory. `tools/AudioV98/PortableV98Entry.cs` adds the normal portable bootstrap to the existing `tools/Portable/PortableProbe.cs` and a bounded longer smoke mode.

- All22 machine templates plus4 repeated model switches passed78 ROM/ULA/frame checks.
- Spectrum48, Spectrum128 and Pentagon128 each executed200 ROM frames plus20 frames of a real Z80 beeper loop; final mixed stereo buffer length882 and nonzero swing on all three.
- Renamed Unicode/# path, internal relative write, configuration/resource lookup, settings round-trip and quietly missing media passed15 checks.
- `Test.exe /moonsound` (44100/48000/96000, RAM, ports, synthetic flash persistence) and `/midisynth` (A2/Max/Omni UART) passed from the foreign current directory after applying the established package step `CorFlags /32BIT+` to the private test EXE. Raw AnyCPU build output is not a ready portable Test.exe; without this step it fails with BadImageFormatException, as expected with the x86 dependencies.
- Final packaged `Test.exe` SHA256 `CFCE6B7A9BC814F8E9A5EEBA6DD3166574E8E7E3DE056476556619D159BF581F`: `/omni-opl` passed in both DOSEN states, with status `00-00-FF-FF-00-00-00-C0-00` and calibrated stereo waveform relation722/2042. Log `portable-omni-opl-final.txt` supersedes the initial test-implementation error in coefficient direction; production code was unchanged for that correction.
- Bounded real TRD boot/audio smoke was subsequently run using the existing `tools/MoonSoundTest/RealNedoProbe.cs` `disk:` option, an isolated final runtime and Extreme with its existing Legacy SAA compatibility enabled. Only private copies were mounted, read-only. An initial1099test attempt stayed at the ERS `No Progs` selector because virtual driveA masked physicalA; an arcane2 attempt reached the actual TRD program selector. Neither is counted as playback or as a sound-chip failure. A final known sequence `key:D1 key:Y key:Z key:Return wait:600 audio:music:250` loaded Arcane2's animated demo from TRD (screenshots10/11) and captured5seconds of nonzero mixed audio: AC RMS2027.433, peak13285, I/O count6399, hash7936A5151BD8630A. The active source in this captured section was PSG (AC RMS5255.509); SAA and FM source buffers were zero. This proves real TRD loading and the shared output path, but does **not** close SAA demo playback or MoonSound disk-player listening. No extra attempt or production change was made. Logs/screenshots/WAVs are in `card_levels\trd\arcane-playback`; mixed WAV SHA256 `C42D712547C9E6465D46BF7B1CA98D291CD2F8C54740A46907964EF9CA30D5B3`. TRD copy SHA256:1099test `D0D1F0D06C9F6434B5A90E7057A9296986F4614A54C19F8E5E269E2168ACDB0D`; arcane2 `56FE811B5697C44A0DF272C4167A73CDB6ED6F690D29CD68779FEC44A042EEF8`.

No synthetic suite is presented as proof that every native file format, player and physical sound card has been listened to. Real NedoOS/OmniPlayer and final performance/packaging acceptance are separate parent-task checks.
