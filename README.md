# ZXMAK2-Redux 0.98

ZXMAK2-Redux is a Windows emulator focused on ZX Evolution BaseConf. It models
the machine as a configurable virtual system: processor, memory, firmware,
storage, display modes and expansion hardware can be changed without leaving
the emulator.

The implementation follows published ZX Evolution, NedoPC and chip-maker
documentation. Registers, I/O ports, timing modes, board revisions and
conflict rules are implemented from those specifications. The maintained
configuration uses current compatible BIOS and ROM images.

[Download 0.98](https://github.com/MoroAICraft/ZXMAK2-Redux/releases/tag/v0.98) ·
[Build from source](docs/BUILDING.md) · [Release notes](docs/releases/0.98.md) ·
[Русское описание](#русское-описание)

Maintained by **MoroAICraft**. Version 0.98 publishes the user-tested and accepted
v98 runtime. The public version number is 0.98; historical build notes may call it v98.

## Screenshots / Скриншоты

<table>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0001.png" alt="ZXMAK2 screenshot 0001" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0006.png" alt="ZXMAK2 screenshot 0006" width="480"></td>
</tr>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0007.png" alt="ZXMAK2 screenshot 0007" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0008.png" alt="ZXMAK2 screenshot 0008" width="480"></td>
</tr>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0010.png" alt="ZXMAK2 screenshot 0010" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0011.png" alt="ZXMAK2 screenshot 0011" width="480"></td>
</tr>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0012.png" alt="ZXMAK2 screenshot 0012" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0015.png" alt="ZXMAK2 screenshot 0015" width="480"></td>
</tr>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0017.png" alt="ZXMAK2 screenshot 0017" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0019.png" alt="ZXMAK2 screenshot 0019" width="480"></td>
</tr>
<tr>
<td><img src="docs/images/screenshots/Screenshot_0021.png" alt="ZXMAK2 screenshot 0021" width="480"></td>
<td><img src="docs/images/screenshots/Screenshot_0023.png" alt="ZXMAK2 screenshot 0023" width="480"></td>
</tr>
</table>

## ZX Evolution BaseConf capabilities

- 3.5, 7 and 14 MHz CPU modes; configurable memory, TR-DOS, keyboard, mouse,
  joystick, tape and reset behaviour.
- Optional **ULAplus** compatibility. When it is enabled in ULA settings,
  supporting software selects the 64-colour palette automatically; disabling
  it retains standard Spectrum video.
- Two equivalent ZXBUS slots. Boards can be placed in either slot, while
  duplicate and incompatible combinations are rejected.
- Four floppy drives, IDE/HDD, Z-controller SD, independent NeoGS microSD,
  CD-ROM access through Windows optical drives, TAP playback and mounting or
  ejecting media from the toolbar.
- ZXNetUSB Rev.C with WIZnet W5300, TCP, UDP, DHCP and DNS through regular
  Windows networking; no TAP driver, network-adapter capture or administrator
  rights are required.
- Full Screen (`Alt+Enter`), border/no-border display, scaling modes, video
  filters, antialiasing, Master Volume, configurable PC joystick mapping,
  Warm Reset, CMOS Reset and Factory Reset.

- File/Open, **drag and drop into the emulator window**, and Windows **Open with**.
- **SD cards and folders are easy to mount from the toolbar.** A PC folder can be
  mounted as a virtual SD card, with **read and write support** for SD cards and mounted folders.

## Sound hardware

| Board | Facilities |
|---|---|
| Neo General Sound (NeoGS C-VS) | General Sound, MOD/MP3, VS1011 and independent microSD. |
| TurboSound FM Pro Rev.C | Two YM2203 and SAA1099. |
| ZX-MultiSound Rev.A2 | Two YM2203, SAA1099, General Sound, SoundDrive and MIDI interface. |
| ZX-MultiSound Max | MultiSound facilities, 2 MB General Sound and OPL2/OPL3 playback, including VGM. |
| ZXM-MoonSound Rev.01 | YMF278B OPL4: FM, PCM/wavetable, sample RAM and external instrument ROM. |
| ZXM-SoundCard Extreme Rev.03 | Two YM2203, SAA1099 and TLC7226 SoundDrive; board clock and port options. |
| ZX OPL3 (UR4QBP) | Standalone YMF262/YAC512 OPL3 board; no sample ROM required. |
| ZX OmniSound Virtual | Combined NeoGS/MP3, YM/TSFM, SAA, SoundDrive, MIDI, Max-compatible OPL2/OPL3/VGM and MoonSound OPL4 in one slot. |

The emulator models board interfaces and prevents hardware-port conflicts.
Sound is mixed with per-board level calibration and a Master Volume control.

Select boards in Machine Settings (`Alt+P`) → Other / ZXBUS PentEvo.
Only compatible combinations can share the two slots. The virtual MIDI synthesizer
is not a bit-exact model of a physical SAM2695. MoonSound PCM requires a separately
obtained YRW801-M ROM; that instrument ROM is not included.

## SD cards and folders from the toolbar

Use the SD controls on the toolbar to open an SD card image or choose
**Mount folder as SD...** and select a folder on your computer. Its files become
available to the emulated machine on a virtual FAT32 SD card through Z-controller.
Both SD card images and folders mounted as SD cards support **reading and writing**.

Changes to a mounted folder can be saved back to that folder when you confirm
the save prompt. **Refresh mounted folder** rereads the PC folder; if there are
pending changes, the emulator offers to save or discard them first.
**Save SD session as image...** exports the virtual card to an image file.
**Eject SD Card** unmounts the card, with a prompt for unsaved changes.

## Download and run

1. Download **Clean** to start with default settings.
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

**ZXMAK2-Redux 0.98** — эмулятор для Windows, ориентированный на ZX Evolution BaseConf.
Машина моделируется как настраиваемая виртуальная система: процессор, память,
прошивки, накопители, видеорежимы и платы расширения меняются прямо в
эмуляторе.

Реализация опирается на опубликованную документацию ZX Evolution, NedoPC и
производителей микросхем. Регистры, порты, режимы, ревизии плат и правила
конфликтов воспроизводятся по этой документации. В поддерживаемой конфигурации
используются совместимые актуальные BIOS и ROM.

Разработка: **MoroAICraft**. Выпуск основан на проверенной и принятой пользователем сборке v98.

### Возможности ZX Evolution BaseConf

- Режимы процессора 3,5 / 7 / 14 МГц, настраиваемые память, TR-DOS,
  клавиатура, мышь, джойстик, лента и сброс машины.
- Опциональная совместимость с **ULAplus**. При включении в настройках ULA
  программа сама выбирает 64-цветную палитру; в выключенном состоянии работает
  стандартное Spectrum-видео.
- Два равноправных слота ZXBUS. Плату можно ставить в любой слот, а дублирующие
  и несовместимые комбинации не допускаются.
- Четыре FDD-дисковода, IDE/HDD, SD Z-controller, независимая microSD NeoGS,
  CD-ROM через оптические приводы Windows, TAP и загрузка/извлечение носителей
  с панели инструментов.
- ZXNetUSB Rev.C с WIZnet W5300, TCP, UDP, DHCP и DNS через обычную сеть
  Windows без TAP-драйверов, захвата сетевого адаптера и прав администратора.
- Полноэкранный режим (`Alt+Enter`), рамка/без рамки, масштабирование,
  видеофильтры, антиалиасинг, Master Volume, настройка PC-джойстика, Warm
  Reset, CMOS Reset и Factory Reset.

- Меню «Открыть», **перетаскивание файлов в окно** и Windows **«Открыть с помощью»**.
- **SD-карты и папки легко монтируются через панель инструментов.** Папку компьютера
  можно подключить как виртуальную SD-карту. Поддерживается **чтение и запись**
  на SD-карты и в папки, смонтированные как карты.

### Звуковые платы

| Плата | Возможности |
|---|---|
| Neo General Sound (NeoGS C-VS) | General Sound, MOD/MP3, VS1011, собственная microSD. |
| TurboSound FM Pro Rev.C | Два YM2203 и SAA1099. |
| ZX-MultiSound Rev.A2 | Два YM2203, SAA1099, General Sound, SoundDrive и MIDI-интерфейс. |
| ZX-MultiSound Max | Возможности MultiSound, General Sound 2 МБ и OPL2/OPL3, включая VGM. |
| ZXM-MoonSound Rev.01 | YMF278B OPL4: FM, PCM/wavetable, sample RAM и внешний инструментальный ROM. |
| ZXM-SoundCard Extreme Rev.03 | Два YM2203, SAA1099, TLC7226 SoundDrive, настройки частоты и портов платы. |
| ZX OPL3 (UR4QBP) | Отдельная плата YMF262/YAC512; инструментальный ROM не нужен. |
| ZX OmniSound Virtual | NeoGS/MP3, YM/TSFM, SAA, SoundDrive, MIDI, OPL2/OPL3/VGM как у Max и OPL4 MoonSound в одном слоте. |

Эмулятор моделирует интерфейсы плат и предотвращает конфликты аппаратных портов.
Звук смешивается с калибровкой уровней отдельных плат и общей регулировкой Master Volume.

Выбор: настройки машины (`Alt+P`) → Other / ZXBUS PentEvo. Одновременно доступны
совместимые сочетания плат. Виртуальный MIDI-синтезатор не является побитовой
моделью физического SAM2695. Для PCM MoonSound нужен отдельно полученный
YRW801-M ROM; в комплект он не входит.

### SD-карты и папки через панель инструментов

**SD-карты и папки легко монтируются через панель инструментов.** В элементах
управления SD можно открыть образ карты или выбрать **Mount folder as SD...**
и указать папку компьютера. Её файлы станут доступны эмулируемой машине
через Z-controller на виртуальной SD-карте FAT32.
Поддерживается **чтение и запись на SD-карты, а также в папки, смонтированные как карты**.

Изменения в смонтированной папке можно сохранить обратно в эту папку после
подтверждения запроса. **Refresh mounted folder** перечитывает папку компьютера;
при наличии изменений эмулятор сначала предлагает сохранить или отменить их.
**Save SD session as image...** сохраняет виртуальную карту в файл образа.
**Eject SD Card** отключает карту с запросом о несохранённых изменениях.

### Начало работы

Скачайте **Clean** для запуска с исходными настройками. Распакуйте целую папку в доступное для записи место и
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

## History

The project was previously hosted on CodePlex:
https://archive.codeplex.com/?p=zxmak2

ZXMAK2 discussions:

- English: https://www.worldofspectrum.org/forums/discussion/39647/
- Russian: http://zx-pk.ru/threads/16830-zxmak2-virtualnaya-mashina-zx-spectrum.html

Earlier emulator history:

- ZXMAK.NET (2005–2008): https://sourceforge.net/projects/zxmak-dotnet/files/zxmak-dotnet/
- ZXMAK (2001–2003): http://zxmak.narod.ru/
