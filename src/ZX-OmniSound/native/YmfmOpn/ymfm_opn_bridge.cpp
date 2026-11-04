// ZX OmniSound's C ABI for the unmodified, BSD-licensed ymfm YM2203 core.
// Only FM output is returned: the emulator already renders each chip's SSG.
#include <algorithm>
#include <cstdint>
#include <new>

#include "ymfm_opn.h"

#if defined(_WIN32)
#define OMNI_EXPORT extern "C" __declspec(dllexport)
#else
#define OMNI_EXPORT extern "C"
#endif

namespace {
constexpr uint32_t CHIP_CLOCK = 3500000;
constexpr uint32_t CLOCKS_PER_NATIVE_SAMPLE = 24;

class opn_bridge final : public ymfm::ymfm_interface {
public:
    opn_bridge() : m_chip(*this) {
        m_chip.set_fidelity(ymfm::OPN_FIDELITY_MIN);
        reset();
    }

    void reset() {
        m_clock = 0;
        m_busy_end = 0;
        m_phase = 0.0;
        m_last_fm = 0;
        m_irq = false;
        for (auto &timer : m_timer) {
            timer.end = 0;
            timer.active = false;
        }
        m_chip.reset();
    }

    void address(uint8_t value) { m_chip.write_address(value); }
    void data(uint8_t value) { m_chip.write_data(value); }
    uint8_t status() { return m_chip.read_status(); }
    uint8_t irq() const { return m_irq ? 1 : 0; }
    void set_clock(uint32_t clock) {
        // Change only the external oscillator. Registers, divider phase and
        // pending timer deadlines (in chip clocks) survive a board clock switch.
        if (clock >= 1000000 && clock <= 8000000) m_input_clock = clock;
    }

    void generate(int16_t *output, uint32_t count, uint32_t host_rate) {
        if (host_rate == 0) return;
        const double step = double(m_input_clock) /
            (double(CLOCKS_PER_NATIVE_SAMPLE) * double(host_rate));
        for (uint32_t index = 0; index < count; ++index) {
            m_phase += step;
            int64_t sum = 0;
            int samples = 0;
            while (m_phase >= 1.0) {
                m_phase -= 1.0;
                const uint64_t target = m_clock + CLOCKS_PER_NATIVE_SAMPLE;
                service_timers(target);
                ymfm::ym2203::output_data sample;
                m_chip.generate(&sample);
                m_last_fm = sample.data[0];
                m_clock = target;
                sum += m_last_fm;
                ++samples;
            }
            const int32_t value = samples == 0 ? m_last_fm :
                static_cast<int32_t>(sum / samples);
            output[index] = static_cast<int16_t>(
                std::max(-32768, std::min(32767, value)));
        }
    }

protected:
    void ymfm_set_timer(uint32_t number, int32_t duration) override {
        if (number >= 2) return;
        if (duration < 0) {
            m_timer[number].active = false;
            return;
        }
        m_timer[number].end = m_clock + static_cast<uint32_t>(duration);
        m_timer[number].active = true;
    }

    void ymfm_set_busy_end(uint32_t clocks) override {
        m_busy_end = m_clock + clocks;
    }

    bool ymfm_is_busy() override { return m_clock < m_busy_end; }
    void ymfm_update_irq(bool asserted) override { m_irq = asserted; }

private:
    struct timer_state {
        uint64_t end = 0;
        bool active = false;
    };

    void service_timers(uint64_t target) {
        for (uint32_t number = 0; number < 2; ++number) {
            int guard = 32;
            while (m_timer[number].active && m_timer[number].end <= target &&
                   guard-- > 0) {
                m_clock = m_timer[number].end;
                m_timer[number].active = false;
                m_engine->engine_timer_expired(number);
            }
        }
    }

    ymfm::ym2203 m_chip;
    uint32_t m_input_clock = CHIP_CLOCK;
    timer_state m_timer[2];
    uint64_t m_clock = 0;
    uint64_t m_busy_end = 0;
    double m_phase = 0.0;
    int32_t m_last_fm = 0;
    bool m_irq = false;
};
}

OMNI_EXPORT void *omni_opn_create() {
    try { return new opn_bridge(); }
    catch (...) { return nullptr; }
}
OMNI_EXPORT void omni_opn_destroy(void *instance) {
    delete static_cast<opn_bridge *>(instance);
}
OMNI_EXPORT void omni_opn_reset(void *instance) {
    if (instance) static_cast<opn_bridge *>(instance)->reset();
}
OMNI_EXPORT void omni_opn_address(void *instance, uint8_t value) {
    if (instance) static_cast<opn_bridge *>(instance)->address(value);
}
OMNI_EXPORT void omni_opn_data(void *instance, uint8_t value) {
    if (instance) static_cast<opn_bridge *>(instance)->data(value);
}
OMNI_EXPORT uint8_t omni_opn_status(void *instance) {
    return instance ? static_cast<opn_bridge *>(instance)->status() : 0xff;
}
OMNI_EXPORT uint8_t omni_opn_irq(void *instance) {
    return instance ? static_cast<opn_bridge *>(instance)->irq() : 0;
}
OMNI_EXPORT void omni_opn_generate(void *instance, int16_t *output,
    uint32_t count, uint32_t host_rate) {
    if (instance && output) static_cast<opn_bridge *>(instance)->generate(
        output, count, host_rate);
}
OMNI_EXPORT void omni_opn_set_clock(void *instance, uint32_t clock) {
    if (instance) static_cast<opn_bridge *>(instance)->set_clock(clock);
}
