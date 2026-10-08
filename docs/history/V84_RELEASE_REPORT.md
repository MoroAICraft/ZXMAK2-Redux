# v84 — рабочая версия на базе v82 BC/NMI

Дата: 8 октября 2026. Пользователь выбрал v82 и отменил v83 из-за нагрузки
и заметной задержки звука. Требование — оставить принятую PrintScreen/NMI
правку для BC Commander, убрать Legacy audio mix (v81) из ULA и оформить v84.

## Точная основа

- Основная v82: commit 70f2e0cc55d5adf6fdc9255faf4a1dacc2a54e17,
  tag zxmak2-v82-common-audio.
- Принятая пользовательская копия: D:\release82_BC058_NMI, ранее проверена
  пользователем в работе BC Commander. Она не перезаписывалась.
- PrintScreen/NMI: исходный отдельный commit 654a891daa75b11c3336bafac4bed0e4aeb7f7c4.
  Он находился поверх v83, но не зависел от FIR/HQ. Новая ветка создана
  **от v82**, cherry-pick только этого commit: 6a88ec8.
- Рабочая ветка: codex/release-v84-bc-nmi; tag zxmak2-v84-bc-nmi.
  Сохранён checkpoint 28a5740 до финальных архивов.
- Пакет: L:\Work_two\ZX\ZXMAK2-v84-ZXEVO-BC-NMI-PrintScreen-20261008.

У принятой D: копии DLL Engine/Hardware/Host совпали с выпущенной v82:
Engine B68DE98D62D2CB4B95547FC21101DE1701D83D9C29142B20E98D2E3C4E93D4EE,
Hardware 6F5AF50A34BBE293C50D063D1C1333E505C3668B5AB486A599B9A0E3081A2E9F,
Host B57B397F9BE73D870C872B354FB654907B89CBD30278CE7E2CFC9F6689B3E739.
В v84 эти же байты оставлены без изменений. Native DLL/ROMS.PAK/GeneralUser
soundfont/EXE/Test.exe и остальные music/runtime dependencies прежние.

## Изменения

В runtime пересобран **только ZXMAK2.Host.WinForms.dll**, обновлены HOTKEYS.md
и README-v84.txt. PrintScreen вызывает тот же VM → NMI при отпускании;
WM_KEYUP-only поддерживается, нажатие/удержание не повторяют NMI. F10
остаётся клавишей гостя, F12 — обычный Warm Reset. Если resident BC не
установлен, это штатный NMI, а не встроенное в эмулятор окно BC Commander.
Репозиторий BC Commander, программы/игры и внешняя SD-папка не менялись.

Из ULA Audio Output удалён chkLegacyAudio и его bindings; master 0..200%
и ULAplus сохранены. При Apply выбирается принятый v82 mixer. Старый
legacyAudioMix XML остаётся читаемым backend для обратной совместимости
и старых diagnostic configs, но отдельного Legacy-переключателя в UI нет.
В принятом runtime-профиле legacy выключен. В v84 нет HQ/PSG FIR классов,
интерфейсов, вкладок или вычислений v83.

Изменённые production файлы относительно v82:
HostKeyboardRouter.cs, MainView.cs, CtlSettingsUla.cs и его Designer.cs.
Исходники Engine/CPU/Hardware/Circuits/Host/OmniSound точно не менялись.
Дополнены PrintScreen regression, UI/layout tests, RunRegressions и документы.
Исторический tools/build_v82_nmi.ps1 перенесён в tools/history; он не
используется для v84 и не должен подменять текущий source build.

## Проверки

- Full solution Release/AnyCPU собирается; обычные два missing-ruleset warnings
  сохранены от проекта, не новая ошибка. Runtime берётся из принятой v82,
  из build output заменён только соответствующий frontend.
- PrintScreenNmiProbe: KEYUP-only, down/up, hold, enabled/disabled NMI,
  подпись меню, F10 guest-only и F12 — PASS.
- KeyboardRouting: 565 проверок, включая F10/меню/курсоры и MultiSound Max — PASS.
- Семь golden renderer vectors, 46 contention/open-bus checks, 41 INT/NMI,
  197981 WAIT/AVR/COM/DOS, 12748 video timing — PASS.
- Tape clock, SD lifecycle/confirmations/export/close/relocation (22 checks),
  PentEvo profiles/slots/Music/Omni/Max, UI layout/navigation — PASS.
- UI: checkbox Legacy отсутствует (поле удалено), старый hidden legacy сбрасывается
  Apply, master 145% применяется и сохраняется, ULAplus не сломан. PNG панели
  284x332 сохранён и просмотрен; элементы не обрезаны.
- Test /hostfoldersd /moonsound /omnisound /multisoundmax /omni-opl
  /master-volume /evo-display, затем обычный Test/renderer/benchmark — PASS.
- AudioMixProbe: 583176 common audio checks; native Moon 0/50/100%,
  full-level raw hash 9F3EE7BD381A9180, бас/уровни/mute/DC/mix — PASS.
- Десять полных fingerprints RAM/CPU/I/O/tacts/video/PCM **совпали побитно**
  с принятой v82 BC/NMI: три снимка, Spectrum48/128, Evo 3,5/7/14 МГц,
  Evo→Pentagon→Evo. Логи state-baseline-v82-bc-nmi и state-release одинаковы.
- Контрольные 58 runtime файлов baseline-v82-bc-nmi совпали с D: копией.
  Runtime отличается только WinForms DLL/HOTKEYS и заменой version README.
  Fingerprints дополняют доказательство SHA256 неизменённого sound/engine.

Это **не новая оптимизация и не продолжение v83**. Тяжёлый FIR не запускается
и его нагрузка не добавлена. Общий sound backend и буферы не переделывались.
Повторное полноценное ручное прохождение BC/game/PrintScreen на новом v84
и прослушивание live Windows output здесь не проводились: перенесена точно
та же уже принятая пользователем клавишная правка; frontend и engine/NMI
проверены отдельно. Физические машины/все игры/новые TSConf/ATM не проверялись.
Старые ожидания физической VGA в существующих probes не расширены до обещания.

## Организация и откат

- release — 58 файлов для использования, без диагностических exe/Data.
- Full ZIP — 58 runtime файлов, настройки принятой BC/NMI копии сохранены,
  включая её существующий sdPath. Внешняя SD-папка не включена в архив.
- Clean ZIP — 55 файлов: без personal VMZ/VMIDE/CMOS; правило standalone user
  ROM исключения сохранено. Все нужные ROM/music/native/soundfont остаются.
- baseline-v82-bc-nmi — принятый контроль для полного отката. Не смешивать DLL.
- diagnostics — логи, UI PNG, SHA256, отдельно сохранённые probes/configs/Data.
- source — combined source ZIP с pinned OmniSound 0794d8d0e050c57dc66bde926639b0dc88ea42d9,
  полный main Git bundle и полный submodule history bundle, verified.
- docs/LOCAL_VERSIONS.md и L:\Work_two\ZX\ZXMAK2-RELEASES.md указывают v84
  как единственную рабочую основу. v83 отмечена **ОТМЕНЕНА**; её архивы,
  source и results не удалены, ветка сохранена в Git history.

Старые выпущенные пакеты, исследования, пользовательские образы/музыка и
D: установки не изменены. Четыре preexisting untracked v81 engine-research
файла сохранены; export разрешает только их явный список, tracked WIP запрещён.
Ничего на GitHub не публиковалось. Ненужные runtime diagnostic файлы перенесены
в diagnostics (восстанавливаемо), пользовательских данных не удалено.

Full/Clean архивы проверены по размеру/SHA256 каждого entry: 58/55 файлов.
ZIP SHA256: Full D07A2A664B51C262B1D12DBC4914E33629B7E49C7B51D6EA480AA66B6FEBB3D0;
Clean 33B528DB22BCAB8D22ED61251C36462F158BD9E830139FFF99250D1A54AA3A04.
Готовая WinForms DLL совпала с финальным source build по SHA256.
