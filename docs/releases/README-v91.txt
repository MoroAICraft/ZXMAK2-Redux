ZXMAK2 v91 - ZXM-SoundCard Extreme Rev.03 TEST (2026-10-28)

Built on v90. New separate ZXBUS board for ZX Evolution / BaseConf.
Alt+P -> Other / ZXBUS PentEvo -> active slot -> ZXM Extreme Rev.03 -> Apply.
Full package already selects Extreme in slot 1, with no mounted media.
Factory/Clean still uses the previous default: select Extreme explicitly.
No user's media or BC Commander collection is included.

Hardware: 2 x YM2203, SAA1099, TLC7226 SounDrive. NOT MultiSound/Max and no
GS, MIDI UART, OPL3/OPL4 or sample ROM. Rev.03 changes mounting, not Rev.02 logic.
JP2 PentEvo on; JP1 Disable YM off; Legacy SAA ports off (JP3 closed).
Legacy off = address #05FF / data #04FF; on = #01FF / #00FF with hardware aliases.
No fictitious TSFM AY-to-SAA redirection. Software #FFFC selects 3.5/2/4 MHz.
Both PSGs use A-left/B-centre/C-right board resistor routing. FM feeds both sides.
SAA/DAC bus gating follows CPLD; SAA and DAC registers survive a CPU reset.
MultiSound/Max/OmniSound cannot share ports with Extreme. MoonSound/NeoGS/ZXNetUSB
can occupy the other slot. Internal AY/YM is disabled while Extreme owns YM.
JP1 can release YM ports. Internal TSFM's optional MultiSound SAA compatibility
must be off if it is used with JP1 closed.

See Docs/Help/index.html for controls, portable layout, media and reset behavior.
v90 direct host-SD Save/Refresh policy, keyboard and accepted audio path retained.
No HQ v83 processing was added. Shared native OPN defaults stay at 3.5 MHz.
Install the complete folder, NOT only the EXE: new native omni_opn.dll is required.
Windows + .NET Framework 4.6.2+ (4.8 recommended), x86 native dependencies remain.

Source, Git checkpoints/bundles, diagnostics and release report are in the
parent v91 package. Roll back by using the complete archived v90, not by mixing
DLLs/configurations. No previous release or user D: installation was overwritten.
This remains a TEST release until the user explicitly accepts it.
