ZXMAK2 v88 - Portable SoundCards TEST

Built from v87 portable, preserving root boot.zip, ROMS.PAK and machine sidecars.
This is a separate test release. It becomes the working base only after the
user confirms that it works. Previous v87 and accepted v84 remain available.

YM2203 FM in TurboSound FM Pro, MultiSound A2, MultiSound Max and OmniSound
uses the same ymfm OPN core, with independent state per board/chip. The previous
approximate FM renderer is no longer selected by these cards. Hardware port
decoders, SSG, SAA, GS, SoundDrive and MIDI ownership remain distinct.
YM data-cycle/BUSY includes SSG writes; FM write-only registers do not read
back software shadow values. Prescaler address commands update SSG and FM.
Max OPL3 converts its fixed 44.1 kHz chip output to the host sample rate.
The accepted MoonSound core/ROM/flash model is not changed.

Legacy IDE controllers retain the internal saved-media copy path and close
their image on disconnect. A profile without a CMOS sidecar no longer attempts
to open a null filename. External original media remain protected.

Keep the complete release folder together, including Lib, Config, Resources,
Docs, boot.zip and ROMS.PAK. Copy/rename the whole folder for portable use.
Own settings/saves/sessions stay here. Missing optional external media leave
empty slots; they are not bundled. No SD music collection or BC Commander added.
See Docs/Help/index.html for layout, keys, reset and portable media rules.

Do not mix DLLs with earlier releases. To roll back, use the complete earlier
release folder/archive. Windows/.NET/native dependencies and detailed test
coverage/limitations are listed in the package V88_RELEASE_REPORT.md.
