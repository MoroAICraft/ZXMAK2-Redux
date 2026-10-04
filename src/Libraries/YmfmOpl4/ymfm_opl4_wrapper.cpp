// ZXMAK2 native bridge for Aaron Giles' BSD-licensed ymfm YMF278B core.
// The upstream sources are vendored beside this file without functional
// changes. This wrapper supplies memory (MX29F016 + SRAM), timers and C ABI.

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <new>
#include <vector>

#include "ymfm_opl.h"

#if defined(_WIN32)
#define YMFM_EXPORT extern "C" __declspec(dllexport)
#else
#define YMFM_EXPORT extern "C"
#endif

namespace
{
constexpr uint64_t CLOCKS_PER_SAMPLE = 768;

class opl4_bridge final : public ymfm::ymfm_interface
{
public:
    opl4_bridge(const uint8_t *rom, uint32_t rom_size, uint32_t ram_size) :
        m_rom(rom, rom + rom_size),
        m_ram(ram_size, 0),
        m_chip(*this)
    {
        reset();
    }

    void reset()
    {
        // Chip reset does not erase nonvolatile flash or SRAM.
        m_flash_state = 0;
        m_flash_id = false;
        m_flash_end = 0;
        m_flash_suspended = false;
        m_clock = 0;
        m_busy_end = 0;
        m_irq = false;
        for (auto &timer : m_timer)
        {
            timer.end = 0;
            timer.active = false;
        }
        m_chip.reset();
    }

    uint8_t read(uint32_t offset)
    {
        return m_chip.read(offset);
    }

    void write(uint32_t offset, uint8_t data)
    {
        m_chip.write(offset, data);
    }

    void generate(int16_t *output, uint32_t samples)
    {
        for (uint32_t index = 0; index < samples; ++index)
        {
            const uint64_t target = m_clock + CLOCKS_PER_SAMPLE;
            service_timers(target);
            ymfm::ymf278b::output_data sample;
            m_chip.generate(&sample);
            output[index * 2 + 0] = static_cast<int16_t>(sample.data[4]);
            output[index * 2 + 1] = static_cast<int16_t>(sample.data[5]);
            m_clock = target;
        }
    }

    bool irq() const { return m_irq; }
    bool flash_dirty() const { return m_flash_dirty; }
    void copy_flash(uint8_t *output, uint32_t size) const
    {
        std::memcpy(output, m_rom.data(), std::min<size_t>(size, m_rom.size()));
    }

protected:
    void ymfm_set_timer(uint32_t number, int32_t duration) override
    {
        if (number >= 2)
            return;
        if (duration < 0)
        {
            m_timer[number].active = false;
            return;
        }
        m_timer[number].end = m_clock + static_cast<uint32_t>(duration);
        m_timer[number].active = true;
    }

    void ymfm_set_busy_end(uint32_t clocks) override
    {
        m_busy_end = m_clock + clocks;
    }

    bool ymfm_is_busy() override
    {
        return m_clock < m_busy_end;
    }

    void ymfm_update_irq(bool asserted) override
    {
        m_irq = asserted;
    }

    uint8_t ymfm_external_read(ymfm::access_class type, uint32_t address) override
    {
        if (type != ymfm::ACCESS_PCM)
            return 0xff;
        if (address < m_rom.size())
            return flash_read(address);
        address -= static_cast<uint32_t>(m_rom.size());
        return address < m_ram.size() ? m_ram[address] : 0xff;
    }

    void ymfm_external_write(ymfm::access_class type, uint32_t address,
        uint8_t data) override
    {
        if (type != ymfm::ACCESS_PCM)
            return;
        if (address < m_rom.size())
        {
            flash_write(address, data);
            return;
        }
        address -= static_cast<uint32_t>(m_rom.size());
        if (address < m_ram.size())
            m_ram[address] = data;
    }

private:
    // MX29F016 byte commands, 32 x 64 KiB sectors. Unlock addresses only
    // decode A10..A0. This is not active for the ROM-less Max OPL3 bridge.
    uint8_t flash_read(uint32_t address)
    {
        if (m_rom.size() != 0x200000)
            return m_rom[address];
        if (m_flash_end > m_clock && !m_flash_suspended)
        {
            m_flash_toggle ^= 0x40;
            return uint8_t((m_flash_data ^ 0x80) & 0x80) | m_flash_toggle |
                (m_flash_erasing ? 0x08 : 0);
        }
        if (m_flash_id)
        {
            switch (address & 0xff)
            {
                case 0: return 0xc2;
                case 1: return 0xad;
                case 2: return 0; // all sector groups unprotected
                default: return 0xff;
            }
        }
        return m_rom[address];
    }

    void flash_write(uint32_t address, uint8_t data)
    {
        if (m_rom.size() != 0x200000)
            return;
        // Suspended erase remains pending even if more than its original
        // completion time has elapsed. CPU/audio clocks continue running.
        if (m_flash_end > m_clock || m_flash_suspended)
        {
            if (m_flash_erasing && data == 0xb0 && !m_flash_suspended)
            {
                m_flash_remaining = m_flash_end - m_clock;
                m_flash_suspended = true;
            }
            else if (m_flash_suspended && data == 0x30)
            {
                m_flash_end = m_clock + m_flash_remaining;
                m_flash_suspended = false;
            }
            return;
        }
        // After A0 the next byte is data, including F0 (not a reset command).
        if (m_flash_state == 3)
        {
            uint8_t programmed = m_rom[address] & data;
            if (m_rom[address] != programmed)
                m_flash_dirty = true;
            m_rom[address] = programmed;
            m_flash_data = data;
            m_flash_erasing = false;
            m_flash_end = m_clock + 238; // 7 us typical @ 33.8688 MHz
            m_flash_state = 0;
            return;
        }
        if (data == 0xf0)
        {
            m_flash_id = false;
            m_flash_state = 0;
            return;
        }
        uint32_t unlock = address & 0x7ff;
        switch (m_flash_state)
        {
            case 0:
                m_flash_state = unlock == 0x555 && data == 0xaa ? 1 : 0;
                break;
            case 1:
                m_flash_state = unlock == 0x2aa && data == 0x55 ? 2 : 0;
                break;
            case 2:
                m_flash_state = 0;
                if (unlock != 0x555)
                    break;
                if (data == 0x90)
                    m_flash_id = true;
                else if (data == 0xa0)
                    m_flash_state = 3;
                else if (data == 0x80)
                    m_flash_state = 4;
                break;
            case 4:
                m_flash_state = unlock == 0x555 && data == 0xaa ? 5 : 0;
                break;
            case 5:
                m_flash_state = unlock == 0x2aa && data == 0x55 ? 6 : 0;
                break;
            case 6:
                m_flash_state = 0;
                if (data == 0x30 || (unlock == 0x555 && data == 0x10))
                {
                    uint32_t first = data == 0x10 ? 0 : address & 0x1f0000;
                    uint32_t count = data == 0x10 ? 0x200000 : 0x10000;
                    std::fill(m_rom.begin() + first, m_rom.begin() + first + count, 0xff);
                    m_flash_dirty = true;
                    m_flash_data = 0xff;
                    m_flash_erasing = true;
                    m_flash_suspended = false;
                    // Datasheet typical times: 4 s sector / 32 s chip.
                    m_flash_end = m_clock + uint64_t(33868800) * (data == 0x10 ? 32 : 4);
                }
                break;
        }
    }

    struct timer_state
    {
        uint64_t end = 0;
        bool active = false;
    };

    void service_timers(uint64_t target)
    {
        // The engine callback can immediately arm the timer again.  Keep
        // servicing it until its next edge falls outside this sample period.
        for (uint32_t number = 0; number < 2; ++number)
        {
            int guard = 8;
            while (m_timer[number].active && m_timer[number].end <= target &&
                guard-- > 0)
            {
                m_clock = m_timer[number].end;
                m_timer[number].active = false;
                m_engine->engine_timer_expired(number);
            }
        }
    }

    std::vector<uint8_t> m_rom;
    std::vector<uint8_t> m_ram;
    ymfm::ymf278b m_chip;
    timer_state m_timer[2];
    uint64_t m_clock = 0;
    uint64_t m_busy_end = 0;
    bool m_irq = false;
    uint8_t m_flash_state = 0, m_flash_data = 0xff, m_flash_toggle = 0;
    bool m_flash_id = false, m_flash_dirty = false, m_flash_erasing = false;
    bool m_flash_suspended = false;
    uint64_t m_flash_end = 0, m_flash_remaining = 0;
};
}

YMFM_EXPORT void *ymfm_opl4_create(const uint8_t *rom, uint32_t rom_size,
    uint32_t ram_size)
{
    if (rom == nullptr || rom_size == 0)
        return nullptr;
    try
    {
        return new opl4_bridge(rom, rom_size, ram_size);
    }
    catch (...)
    {
        return nullptr;
    }
}

YMFM_EXPORT void ymfm_opl4_destroy(void *instance)
{
    delete static_cast<opl4_bridge *>(instance);
}

YMFM_EXPORT void ymfm_opl4_reset(void *instance)
{
    if (instance != nullptr)
        static_cast<opl4_bridge *>(instance)->reset();
}

YMFM_EXPORT uint8_t ymfm_opl4_read(void *instance, uint32_t offset)
{
    return instance == nullptr ? 0xff :
        static_cast<opl4_bridge *>(instance)->read(offset);
}

YMFM_EXPORT void ymfm_opl4_write(void *instance, uint32_t offset, uint8_t data)
{
    if (instance != nullptr)
        static_cast<opl4_bridge *>(instance)->write(offset, data);
}

YMFM_EXPORT void ymfm_opl4_generate(void *instance, int16_t *output,
    uint32_t samples)
{
    if (instance != nullptr && output != nullptr)
        static_cast<opl4_bridge *>(instance)->generate(output, samples);
}

YMFM_EXPORT uint8_t ymfm_opl4_irq(void *instance)
{
    return instance != nullptr && static_cast<opl4_bridge *>(instance)->irq();
}

YMFM_EXPORT bool ymfm_opl4_flash_dirty(void *instance)
{
    return instance != nullptr && static_cast<opl4_bridge *>(instance)->flash_dirty();
}

YMFM_EXPORT void ymfm_opl4_copy_flash(void *instance, uint8_t *output, uint32_t size)
{
    if (instance != nullptr && output != nullptr)
        static_cast<opl4_bridge *>(instance)->copy_flash(output, size);
}
