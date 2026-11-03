// ZX OPL3 (UR4QBP): dedicated YMF262, NOT the YMF278B/OPL4 compatibility path.
// Uses the unchanged BSD-3-Clause ymfm sources in ../YmfmOpl4 (see LICENSE).
#include <cstdint>
#include <new>
#include "ymfm_opl.h"

#define OPL3_EXPORT extern "C" __declspec(dllexport)

namespace {
class opl3_bridge final : public ymfm::ymfm_interface
{
public:
    opl3_bridge() : m_chip(*this) { reset(); }
    void reset()
    {
        m_clock = 0;
        for (auto &timer : m_timer) { timer.end = 0; timer.active = false; }
        m_chip.reset();
        m_chip.write_address(0);
    }
    void advance(uint64_t clocks)
    {
        const uint64_t target = m_clock + clocks;
        for (;;)
        {
            unsigned next = 2;
            for (unsigned i = 0; i < 2; ++i)
                if (m_timer[i].active && m_timer[i].end <= target &&
                    (next == 2 || m_timer[i].end < m_timer[next].end)) next = i;
            if (next == 2) break;
            m_clock = m_timer[next].end;
            m_timer[next].active = false;
            m_engine->engine_timer_expired(next);
        }
        m_clock = target;
    }
    uint8_t read(uint32_t offset) { return m_chip.read(offset); }
    void write(uint32_t offset, uint8_t data) { m_chip.write(offset, data); }
    void generate(int16_t *out, uint32_t count)
    {
        // One sample per 288 input clocks. Timers are advanced by the bus
        // clock, not by host sample rate or by whether audio is muted.
        for (uint32_t i = 0; i < count; ++i)
        {
            ymfm::ymf262::output_data sample;
            m_chip.generate(&sample);
            out[2*i] = static_cast<int16_t>(sample.data[0]);
            out[2*i+1] = static_cast<int16_t>(sample.data[1]);
            // DOCD is unconnected on this board: do not fold C/D into A/B.
        }
    }
protected:
    void ymfm_set_timer(uint32_t number, int32_t duration) override
    {
        if (number >= 2) return;
        m_timer[number].active = duration >= 0;
        m_timer[number].end = m_clock + (duration < 0 ? 0 : duration);
    }
    // YMF262 has no readable BUSY flag. /IRQ is not wired to ZXBUS.
private:
    uint64_t m_clock = 0;
    struct timer { uint64_t end = 0; bool active = false; } m_timer[2];
    ymfm::ymf262 m_chip;
};
}

OPL3_EXPORT void *ymfm_opl3_create() { return new (std::nothrow) opl3_bridge; }
OPL3_EXPORT void ymfm_opl3_destroy(void *p) { delete static_cast<opl3_bridge *>(p); }
OPL3_EXPORT void ymfm_opl3_reset(void *p) { if (p) static_cast<opl3_bridge *>(p)->reset(); }
OPL3_EXPORT void ymfm_opl3_advance(void *p, uint64_t clocks)
{ if (p) static_cast<opl3_bridge *>(p)->advance(clocks); }
OPL3_EXPORT uint8_t ymfm_opl3_read(void *p, uint32_t offset)
{ return p ? static_cast<opl3_bridge *>(p)->read(offset) : 0xff; }
OPL3_EXPORT void ymfm_opl3_write(void *p, uint32_t offset, uint8_t value)
{ if (p) static_cast<opl3_bridge *>(p)->write(offset, value); }
OPL3_EXPORT void ymfm_opl3_generate(void *p, int16_t *output, uint32_t count)
{ if (p && output) static_cast<opl3_bridge *>(p)->generate(output, count); }
