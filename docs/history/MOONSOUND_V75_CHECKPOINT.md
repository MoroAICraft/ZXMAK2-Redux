# MoonSound / MultiSound Max — локальная работа v75

Исходная точка: `1f56943`, рабочее дерево `codex/zx-omnisound-integration`.
Пользовательская v74: `D:\release74_write_folder_06`; её не заменять.
Не публиковать на GitHub. Посторонние изменения в основном checkout не трогать.

Задача: Rev.01 CPLD 1.01 (legacy #7E/#7F, чтение #C6 переводит PCM на
#C2/#C3 до reset), совместимое определение OPL3/OPL4, flash MX29F016 и
исправление добавления MultiSound Max. MIDI — программа управления OPL4,
отдельного MIDI-UART на физической MoonSound нет.

Проверенные источники:
- NedoOS SVN `src/gp/common/moonsound.asm`: macro
  `switch_to_pcm_ports_c2_c3` = `in a,(MOON_REG2)` (#C6).
- Micklab Rev.01 CPLD 1.00 `dd2.tdf`: legacy decode, ENDOS gate.
- Micklab MoonService v0.3a `flash.asm`: AA/55/90 unlock, IDs C2/AD;
  память через wave reg02=11h, reg03..06. Исходники скачаны в
  `C:\Users\Moro\AppData\Local\Temp\zx-moonsound-rev01`.
- MX29F016 datasheet PM0590 rev1.4 (manufacturer document).

MultiSound Max: лог `C:\Logs\ZXMAK2.log` содержит FileNotFoundException
из LoadGsRom. GS ROM 512 KiB есть в пользовательском
`K:\Download\USER_ZxMultiSoundMax.zip`, `GS_ROM/AM29F040B@PLCC32.BIN`.
Загрузчик ищет только внешний bin; начиная v63 ROMS.PAK его потерял.
Исправить loader + локальный пакет, не коммитить чужую прошивку.

Реализовано: переключаемые порты CPLD 1.01; определение OPL4/таймеров;
MX29F016 C2/AD, unlock/program/erase/data-polling; сохранение flash отдельно
от исходного Yamaha ROM; fallback GS ROM из ROMS.PAK для MultiSound Max.
Новая папка сборки: D:\release75_moonsound_01. v74 не изменена.

PASS: /moonsound (44100/48000/96000), /multisoundmax, /multisound, /tsfm,
/omnisound (44100/48000/96000), /omni-opl, /hostfoldersd, /master-volume,
native flash_probe (включая длинный suspend/resume), synthetic atomic flash
save/backup/reload, исходные процедуры MoonService v0.3a (YMF278B, C2/AD).
MSBuild Release успешен, предупреждения только о ранее отсутствовавших ruleset.
Проверены /moonsound и /multisoundmax из самой папки новой сборки.
Сохранён пользовательский WinForms/DirectSound DLL из v74 (SHA256 совпадает).
Test.exe и benchmark не удалены. README-v75.txt рядом с новой сборкой.

Остались ручные проверки: полное меню MoonService/Test RAM/Test ROM/update,
пользовательские MIDI-плееры, Apply MultiSound Max в GUI. Пробный GUI стартует,
но Computer Use неустойчиво работает с его дочерними modal/combo окнами;
не считать неудачные клики подтверждением Apply. Изменений v74 нет.
Функциональная flash: результат program/erase меняется сразу, polling завершения
задержан. Нет износа/электрических ошибок, multi-sector erase queue и program
других секторов при suspend. Не заявлять абсолютную точность этих режимов.

Сборка DLL: Zig 0.15.2 x86-windows-gnu (путь в tools/MoonSoundTest/README.md).
MSBuild: VS2022 BuildTools. После сборки CorFlags Test.exe/ZXMAK2.exe /32BIT+.
GS ROM добавлен только в локальный output/runtime ROMS.PAK, не в tracked
src/ZXMAK2/ROMS.PAK. Чистая пересборка требует повторной локальной упаковки
пользовательского GS ROM (описано в tools/MoonSoundTest/README.md).
