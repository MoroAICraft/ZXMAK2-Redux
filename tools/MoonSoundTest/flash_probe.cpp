// No user ROM or file writes: exercise the native bridge with a synthetic ROM.
#include <cassert>
#include <cstdio>
#include <vector>
#include "../../src/Libraries/YmfmOpl4/ymfm_opl4_wrapper.cpp"

static void reg(void *chip, unsigned index, unsigned data)
{
    ymfm_opl4_write(chip, 4, index);
    ymfm_opl4_write(chip, 5, data);
}
static void address(void *chip, unsigned addr)
{
    reg(chip, 3, addr >> 16); reg(chip, 4, addr >> 8); reg(chip, 5, addr);
    ymfm_opl4_write(chip, 4, 6);
}
static void write(void *chip, unsigned addr, unsigned data)
{
    address(chip, addr); ymfm_opl4_write(chip, 5, data);
}
static unsigned read(void *chip, unsigned addr)
{
    address(chip, addr); return ymfm_opl4_read(chip, 5);
}
static void unlock(void *chip, unsigned command)
{
    write(chip, 0x555, 0xaa); write(chip, 0x2aa, 0x55);
    write(chip, 0x555, command);
}
static void tick(void *chip, unsigned samples)
{
    std::vector<int16_t> buffer(samples * 2);
    ymfm_opl4_generate(chip, buffer.data(), samples);
}
int main()
{
    std::vector<uint8_t> rom(0x200000, 0xff);
    void *chip = ymfm_opl4_create(rom.data(), unsigned(rom.size()), 0x100000);
    assert(chip);
    ymfm_opl4_write(chip, 2, 5); ymfm_opl4_write(chip, 3, 3);
    reg(chip, 2, 0x11);
    unlock(chip, 0x90);
    assert(read(chip, 0) == 0xc2 && read(chip, 1) == 0xad);
    assert(read(chip, 2) == 0 && !ymfm_opl4_flash_dirty(chip));
    write(chip, 0, 0xf0);
    write(chip, 0x1234, 0); // a plain store must never program flash
    assert(read(chip, 0x1234) == 0xff);
    unlock(chip, 0xa0); write(chip, 0x1234, 0x5a);
    unsigned status1 = read(chip, 0x1234), status2 = read(chip, 0x1234);
    assert((status1 ^ status2) & 0x40);
    tick(chip, 2);
    assert(read(chip, 0x1234) == 0x5a && ymfm_opl4_flash_dirty(chip));
    unlock(chip, 0xa0); write(chip, 0x1234, 0xff); tick(chip, 2);
    assert(read(chip, 0x1234) == 0x5a); // programming cannot set zero bits
    unlock(chip, 0xa0); write(chip, 0x2345, 0xf0); tick(chip, 2);
    assert(read(chip, 0x2345) == 0xf0); // F0 in program-data state is data
    unlock(chip, 0xa0); write(chip, 0x11234, 0xa5); tick(chip, 2);
    write(chip, 0x200000, 0x55); write(chip, 0x2fffff, 0xaa);
    assert(read(chip, 0x200000) == 0x55 && read(chip, 0x2fffff) == 0xaa);
    assert(read(chip, 0x300000) == 0xff);
    unlock(chip, 0x80); write(chip, 0x555, 0xaa); write(chip, 0x2aa, 0x55);
    write(chip, 0x1234, 0x30);
    write(chip, 0x1234, 0xb0); // suspend sector erase
    tick(chip, 176402); // original deadline passes while suspended
    write(chip, 0x1234, 0x30); // resume must still be accepted
    unsigned eraseStatus = read(chip, 0x1234);
    assert((eraseStatus & 0x88) == 0x08);
    tick(chip, 176402);
    assert(read(chip, 0x1234) == 0xff && read(chip, 0x2345) == 0xff);
    assert(read(chip, 0x11234) == 0xa5); // erase confined to one 64 KiB sector
    ymfm_opl4_copy_flash(chip, rom.data(), unsigned(rom.size()));
    assert(rom[0x11234] == 0xa5);
    ymfm_opl4_destroy(chip);
    chip = ymfm_opl4_create(rom.data(), unsigned(rom.size()), 0x100000);
    ymfm_opl4_write(chip, 2, 5); ymfm_opl4_write(chip, 3, 3); reg(chip, 2, 0x11);
    assert(read(chip, 0x11234) == 0xa5 && !ymfm_opl4_flash_dirty(chip));
    ymfm_opl4_destroy(chip);
    puts("MX29F016: ID, unlock, data polling, 1->0 program, sector erase/suspend/resume, SRAM and flash snapshot: PASS");
}
