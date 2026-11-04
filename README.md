# ZXMAK2-Redux 0.98

**Windows emulator of ZX Evolution BaseConf with configurable ZXBUS hardware and expanded sound support.**

[Download 0.98](https://github.com/MoroAICraft/ZXMAK2-Redux/releases/tag/v0.98) ·
[Build from source](docs/BUILDING.md) · [Release notes](docs/releases/0.98.md) ·
[Русское описание](#русское-описание)

Maintained by **MoroAICraft**. Version 0.98 publishes the user-tested and accepted
v98 runtime. The public version number is 0.98; historical build notes may call it v98.

## Features

- ZX Evolution BaseConf: 3.5 / 7 / 14 MHz CPU modes, configurable memory, firmware,
  TR-DOS, keyboard, mouse, PC joystick mapping and optional ULAplus compatibility.
- Two interchangeable ZXBUS slots with checks for duplicate devices and port conflicts.
- Four floppy drives, IDE/HDD, Z-controller SD, host-folder SD support,
  NeoGS microSD, tape playback and mounting/ejecting media from the toolbar.
- ZXNetUSB Rev.C / W5300 with TCP, UDP, DHCP and DNS through Windows networking.
- Full screen (`Alt+Enter`), display scaling, filters, Master Volume and separate reset modes.
- File/Open, **drag and drop into the emulator window**, and Windows **Open with**.

## Sound hardware

| Board | Facilities |
|---|---|
| Neo General Sound (NeoGS C-VS) | General Sound, MOD/MP3, VS1011 and independent microSD. |
| TurboSound FM Pro Rev.C | Two YM2203 and SAA1099. |
| ZX-MultiSound Rev.A2 | Two YM2203, SAA1099, General Sound, SoundDrive and MIDI interface. |
| ZX-MultiSound Max | MultiSound facilities, 2 MB General Sound and OPL2/OPL3. |
| ZXM-MoonSound Rev.01 | YMF278B OPL4: FM, PCM/wavetable, sample RAM and external instrument ROM. |
| ZXM-SoundCard Extreme Rev.03 | Two YM2203, SAA1099 and TLC7226 SoundDrive; board clock and port options. |
| ZX OPL3 (UR4QBP) | Standalone YMF262/YAC512 OPL3 board; no sample ROM required. |
| ZX OmniSound Virtual | Combined GS/MP3, YM/TSFM, SAA, SoundDrive, MIDI, OPL3 and OPL4 facilities in one slot. |

Select boards in Machine Settings (`Alt+P`) → Other / ZXBUS PentEvo.
Only compatible combinations can share the two slots. The virtual MIDI synthesizer
is not a bit-exact model of a physical SAM2695. MoonSound PCM requires a separately
obtained YRW801-M ROM; that instrument ROM is not included.

## Download and run

1. Download **Clean** for default settings, or **Full** for the prepared ZX OPL3 profile.
2. Extract the entire folder to a writable location. Keep EXE, DLL, Config and Resources together.
3. Run `ZXMAK2.exe`. Windows and .NET Framework 4.6.2 or newer are required;
   .NET Framework 4.8 is recommended. Legacy display backends may require their matching DirectX/XNA runtime.
4. Open your own media through File/Open or drag a supported file into the window.

Windows **Open with → ZXMAK2** supports TRD, SCL, TAP and SNA. Start the extracted
copy manually once to register its location. With one running compatible instance,
the file goes to that instance; with several, select the window by installation path.
Drag and drop and File/Open operate on the window you use. After moving a folder,
start that copy manually again. Existing Windows default-app choices are not forcibly changed.

User disks, music and saved sessions are not bundled. Keep an existing installation
separate for rollback; do not mix DLLs from different versions. See the release notes
for the audio changes and remaining limitations.

## Source and project history

The complete ZX-OmniSound source snapshot is included under `src/ZX-OmniSound`.
No private repository or developer-specific drive path is needed for a normal build.
Use [`tools/Build.ps1`](tools/Build.ps1); details are in [BUILDING.md](docs/BUILDING.md).

ZXMAK2-Redux continues the original ZXMAK2 project. Original authorship and commit
history are preserved. The project is distributed under [GNU GPL v3](LICENSE.txt);
bundled third-party components retain their own licenses and notices.
Earlier engineering records remain available in `docs/history` and Git history.

---

## Русское описание

**ZXMAK2-Redux 0.98** — эмулятор ZX Evolution BaseConf для Windows с настраиваемыми
платами ZXBUS и расширенной поддержкой звука. Разработка: **MoroAICraft**.
Выпуск основан на проверенной и принятой пользователем сборке v98.

### Возможности

- Частоты 3,5 / 7 / 14 МГц, память, прошивки, TR-DOS, клавиатура, мышь,
  PC-джойстик и опциональная совместимость ULAplus.
- Два равноправных слота ZXBUS с проверкой конфликтов устройств и портов.
- Четыре дисковода, IDE/HDD, SD Z-controller, папка Windows в режиме SD,
  отдельная microSD NeoGS, лента и управление носителями с панели инструментов.
- ZXNetUSB Rev.C / W5300: TCP, UDP, DHCP и DNS через сеть Windows.
- Полноэкранный режим (`Alt+Enter`), масштабирование, фильтры, общая громкость
  и отдельные режимы сброса.
- Меню «Открыть», **перетаскивание файлов в окно** и Windows **«Открыть с помощью»**.

### Звуковые платы

| Плата | Возможности |
|---|---|
| Neo General Sound (NeoGS C-VS) | General Sound, MOD/MP3, VS1011, собственная microSD. |
| TurboSound FM Pro Rev.C | Два YM2203 и SAA1099. |
| ZX-MultiSound Rev.A2 | Два YM2203, SAA1099, General Sound, SoundDrive и MIDI-интерфейс. |
| ZX-MultiSound Max | Возможности MultiSound, General Sound 2 МБ и OPL2/OPL3. |
| ZXM-MoonSound Rev.01 | YMF278B OPL4: FM, PCM/wavetable, sample RAM и внешний инструментальный ROM. |
| ZXM-SoundCard Extreme Rev.03 | Два YM2203, SAA1099, TLC7226 SoundDrive, настройки частоты и портов платы. |
| ZX OPL3 (UR4QBP) | Отдельная плата YMF262/YAC512; инструментальный ROM не нужен. |
| ZX OmniSound Virtual | GS/MP3, YM/TSFM, SAA, SoundDrive, MIDI, OPL3 и OPL4 в одном слоте. |

Выбор: настройки машины (`Alt+P`) → Other / ZXBUS PentEvo. Одновременно доступны
совместимые сочетания плат. Виртуальный MIDI-синтезатор не является побитовой
моделью физического SAM2695. Для PCM MoonSound нужен отдельно полученный
YRW801-M ROM; в комплект он не входит.

### Начало работы

Скачайте **Clean** для запуска с исходными настройками или **Full** с подготовленной
конфигурацией ZX OPL3. Распакуйте целую папку в доступное для записи место и
запустите `ZXMAK2.exe`. Нужна Windows и .NET Framework 4.6.2 или новее;
рекомендуется 4.8. Для старых графических бэкендов могут потребоваться соответствующие DirectX/XNA.

TRD/SCL/TAP/SNA открываются через Windows **«Открыть с помощью → ZXMAK2»**.
После распаковки или переноса сначала один раз запустите эту копию вручную.
При одном запущенном совместимом экземпляре файл передаётся ему; при нескольких
появляется выбор окна по пути установки. Меню «Открыть» и перетаскивание работают
с выбранным окном. Выбор приложения по умолчанию Windows принудительно не меняется.

Рабочие диски, музыка и сохранённые сеансы пользователя не включены. Предыдущую
установку сохраняйте отдельно для отката; не смешивайте DLL разных версий.
Изменения звука и ограничения: [примечания к 0.98](docs/releases/0.98.md).
Исходники и инструкции сборки: [BUILDING.md](docs/BUILDING.md).
