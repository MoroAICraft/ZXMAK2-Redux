# MoonSound недоступна OmniPlayer под NedoOS — диагностика 2026-10-04

Задача пользователя: реальное воспроизведение OmniPlayer 1.0.32 под NedoOS,
короткая трасса полных портов/PC/DOSEN/gate/handled/value, сверка ENDOS,
минимальное доказанное исправление и отдельный откатный пакет. Не публиковать.

Исходная установленная сборка: `D:\release76_moon_omni_max_key`.
Все присутствующие EXE/DLL/ROMS.PAK побайтно совпадают с
`L:\Work_two\ZX\ZXMAK2-v76-ZXEVO-BC-OmniSound-Keyboard-Test-20261004\release`.
Рабочие исходники: `codex/zx-omnisound-integration`, `b24c714`;
входящая аппаратная реализация MoonSound из v75 сохранена в v76.
Основной checkout имеет посторонние изменения, не трогать.

SHA256 Hardware.dll: A250A39081AA608AC606629A5F858382607E1C1C216290C6DDB46D32947CF21B
SHA256 ymfm_opl4.dll: 9E19FB66EEAC3BE4866559BB1EADD0004007334716CDE7940FD8A7EB1E9308A8

Установленная vmz: только Slot1 MoonSound, Internal music None;
SD HostFolder = `L:\Work_two\ZX\ZX_IMG\flash_pentevo`.
В корне есть `moonservice_v03a.trd`, в bin есть штатный `moon.com`/`gp.com`;
OmniPlayer 1.0.32 по именам файлов не найден. Запрошен только путь к плееру.
Не подменять его штатным GP/синтетическим таймерным тестом.

Уже установлено по исходникам, НЕ по новой реальной трассе:
- MemoryPentEvo чтение #0CBE bit4 = DOSEN, setter DOSEN = value | CPM.
- Standalone ZxmMoonSoundDevice gate = m_hostMemory == null || !DOSEN.
- Публичный Micklab CPLD 1.00 dd2.tdf:
  ENDOS = (!C_DOS & C_PEVO) # !C_IODOS; ENIO включает ENDOS.
- BaseConf r1364 top.v: внешний dos_n = rompg[1] (aka ROMPG1);
  внутренний dos идёт отдельно из zdos в pager и zports. Их тождественность
  не установлена. Нужны схема платы/распиновка и реальная последовательность.
- zports.v BD_PXX77 отдаёт внутренний dos в bit4.
Публичные RTL доступны в локальном `_reference_zx-evo-r1364` через git show;
CPLD в Temp/zx-moonsound-rev01/cpld/ZXM-MoonSound rev01 V0100/dd2.tdf.
Сайт Micklab не открылся через web; это не доказательство содержания схемы.

## Продолжение: причина доказана, исправление реализовано

Получен пользовательский `E:\OmniPlayer_v1.0.32_MoonSound_Diagnostic.zip`.
В игнорируемом `tools/MoonSoundTest/build/nedo-v76` созданы отдельные runtime,
runtime-fixed и копия bin/System/ini/корневых файлов SD. Только в этой копии
установлены неизменённые bin из диагностического ZIP. Исходная SD не менялась.
RealNedoProbe.cs запускает настоящий ROM/SD Boot/NedoOS и gp, подавая клавиши
в эмулируемую матрицу и AVR/CMOS PS/2; это не синтетический тест таймера.

До исправления: gp читает #0CBE при PC #8BA8 -> #3E, DOSEN=true.
#00C6 при PC #96E1: available=false, handled=false, #FF (GATE).
#FFC4 при PC #96E6: аналогично #FF. Скриншот trace-before-final/07.png
точно воспроизводит пользовательскую ошибку, ROM/RAM читаются #FF.

После исправления: #0CBE остаётся #3E; #00C6 -> #06 handled=true;
#06C4 -> #00 handled=true; cleared #00C4 -> #00, timer #37C4 -> #A0.
ROM = 43 6F 70 79 72 69 67 68 74 (Copyright), RAM = 00 FF FF.
repeat-cold-after/12.png показывает повторное успешное обнаружение после
выхода из плеера в той же OS. Первый запуск после очистки RAM/холодного сброса.
Фактическая частота OS 14 МГц (clock=4): попытка понизить скорость была
перезаписана штатным переключением контекста NedoOS. Не заявлять 3,5/7 МГц.

Схемы полностью просмотрены на нужных листах. MoonSound JP1 замыкает PEVO
на землю; R5 подтягивает IODOS. ZX-Evo Rev.C XS1/XS2:B20 НЕ подключён.
Поэтому ENDOS=0 с установленным JP1 даже при внутреннем DOSEN=true.
Нельзя связывать IODOS с VG93 chip select: на этой схеме такой связи нет.
BaseConf dos_n=rompg[1], а не внутренний dos. Исправлен gate физических
входов; внутренний DOSEN и NedoOS/плеер не изменялись.

Аппаратные файлы ZxmMoonSoundDevice.cs и MemoryPentEvo.cs исправлены;
Test.exe проверяет низкий IODOS и открытый JP1 отдельно. Предыдущие прогоны
/moonsound и /omnisound на 44100/48000/96000, /multisoundmax и /omni-opl PASS.
После расширения тестов нужно пересобрать и повторить. Полный MoonService
через TRD пока не запущен (ERS сообщает No Progs): не выдавать это за PASS.
Следующие шаги: закрыть регрессию, документировать непроверенное, создать
локальную точку сохранения и отдельный v77 пакет в L:\Work_two\ZX.
Ничего не публиковать и не изменять исходную сборку/SD/основной checkout.

## Финальная контрольная точка

Повторная сборка и расширенные pin-tests PASS; /moonsound, /omnisound,
/multisoundmax, /omni-opl, original MoonService chip/flash routines PASS.
Обычный Test.exe sanity/benchmark PASS. Реальный gp с OmniSound также читает
Copyright и успешно инвертирует RAM. Full MoonService через BaseConf дошла
до MAXBOOT, но menu RAM/ROM и дисковая музыка не подтверждены этим прогоном.
Полный отчёт: MOONSOUND_NEDOOS_ENDOS_V77.md, там явный список непроверенного.
Создан отдельный v77 runtime в L:\Work_two\ZX по прежней структуре;
исходная v76 не тронута. Пакетировщик Package-v77.ps1 проверяет SHA256 архивов.
Диагностические материалы находятся вне runtime в diagnostics/, diff в source/.
