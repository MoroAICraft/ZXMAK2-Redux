# OmniSound MIDI synthesizer bridge

`omni_midi_bridge.cpp` wraps the vendored TinySoundFont v0.9 at upstream commit
`853a0a171759f1ddba0de1442133a75912bbeffa` (MIT license in
`../TinySoundFont/LICENSE`). It accepts MIDI bytes from a separately emulated
31,250-baud AY port A UART and renders stereo PCM into the emulator mixer.

Build the Windows x86 DLL with a C++ compiler, for example:

```
zig c++ -O2 -shared -target x86-windows-gnu -o omni_midi.dll omni_midi_bridge.cpp
```

The bank is a separate `GeneralUser-GS.sf2` file; its license is kept beside it.
It provides General MIDI playback but does not claim bit-identical SAM2695
timbres. MIDI and OPL are independent interfaces.
