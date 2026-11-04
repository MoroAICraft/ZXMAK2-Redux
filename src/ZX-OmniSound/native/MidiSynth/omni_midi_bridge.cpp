// ZX OmniSound MIDI synthesizer bridge.
// TinySoundFont is vendored under ../TinySoundFont (MIT license).
#define TSF_IMPLEMENTATION
#include "../TinySoundFont/tsf.h"

#if defined(_WIN32)
#define OMNI_EXPORT extern "C" __declspec(dllexport)
#else
#define OMNI_EXPORT extern "C"
#endif

struct omni_midi_synth
{
    tsf* font;
    unsigned char status;
    unsigned char first;
    unsigned char received;
    bool sysex;
};

OMNI_EXPORT omni_midi_synth* omni_midi_create(const unsigned char* bank,
    int length, int sample_rate)
{
    if (!bank || length <= 0 || sample_rate <= 0)
        return nullptr;
    tsf* font = tsf_load_memory(bank, length);
    if (!font)
        return nullptr;
    omni_midi_synth* synth = new omni_midi_synth{font, 0, 0, 0, false};
    tsf_set_output(font, TSF_STEREO_INTERLEAVED, sample_rate, -4.0f);
    tsf_set_max_voices(font, 256);
    for (int channel = 0; channel < 16; ++channel)
        tsf_channel_set_presetnumber(font, channel, 0, channel == 9);
    return synth;
}

OMNI_EXPORT void omni_midi_destroy(omni_midi_synth* synth)
{
    if (!synth)
        return;
    tsf_close(synth->font);
    delete synth;
}

OMNI_EXPORT void omni_midi_reset(omni_midi_synth* synth)
{
    if (!synth)
        return;
    tsf_reset(synth->font);
    synth->status = synth->first = synth->received = 0;
    synth->sysex = false;
    for (int channel = 0; channel < 16; ++channel)
        tsf_channel_set_presetnumber(synth->font, channel, 0, channel == 9);
}

OMNI_EXPORT void omni_midi_set_rate(omni_midi_synth* synth, int sample_rate)
{
    if (synth && sample_rate > 0)
        tsf_set_output(synth->font, TSF_STEREO_INTERLEAVED, sample_rate, -4.0f);
}

OMNI_EXPORT void omni_midi_byte(omni_midi_synth* synth, unsigned char value)
{
    if (!synth || value >= 0xF8) // MIDI real-time bytes do not alter running status.
        return;
    if (value & 0x80)
    {
        if (value == 0xF0)
            synth->sysex = true;
        else if (value == 0xF7)
            synth->sysex = false;
        else if (value >= 0xF0)
            synth->sysex = false;
        synth->status = value < 0xF0 ? value : 0;
        synth->received = 0;
        return;
    }
    if (synth->sysex || synth->status == 0)
        return;

    const int command = synth->status & 0xF0;
    const int channel = synth->status & 0x0F;
    if (command == 0xC0)
    {
        tsf_channel_set_presetnumber(synth->font, channel, value, channel == 9);
        return;
    }
    if (command == 0xD0)
        return;
    if (!synth->received)
    {
        synth->first = value;
        synth->received = 1;
        return;
    }
    synth->received = 0;
    switch (command)
    {
        case 0x80:
            tsf_channel_note_off(synth->font, channel, synth->first);
            break;
        case 0x90:
            if (value)
                tsf_channel_note_on(synth->font, channel, synth->first, value / 127.0f);
            else
                tsf_channel_note_off(synth->font, channel, synth->first);
            break;
        case 0xA0:
            break;
        case 0xB0:
            tsf_channel_midi_control(synth->font, channel, synth->first, value);
            break;
        case 0xE0:
            tsf_channel_set_pitchwheel(synth->font, channel,
                synth->first | (value << 7));
            break;
    }
}

OMNI_EXPORT void omni_midi_render(omni_midi_synth* synth, short* output,
    int samples)
{
    if (synth && output && samples > 0)
        tsf_render_short(synth->font, output, samples, 0);
}
