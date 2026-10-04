# v77: MoonSound / NedoOS / ENDOS, 04.10.2026

## Итог

Пользовательская ошибка воспроизведена на неизменённых DLL v76 с настоящим
SD Boot, NedoOS и OmniPlayer 1.0.32. После исправления того же аппаратного
компонента плеер обнаруживает OPL3/OPL4, читает Yamaha ROM и записывает RAM.
NedoOS и плеер не исправлялись. Исходная SD-папка не менялась.

## Состояние исходников и откат

`D:\release76_moon_omni_max_key` побайтно совпала по всем EXE/DLL/ROMS.PAK с
`L:\Work_two\ZX\ZXMAK2-v76-ZXEVO-BC-OmniSound-Keyboard-Test-20261004\release`.
Базовый commit: `b24c714`, ветка `codex/zx-omnisound-integration`.
Соответствующий checkout:
`C:\Users\Moro\.codex\worktrees\omnisound-integration\ZXMAK2-Fork`.
Основной `L:\Work_two\ZX\ZXMAK2-Fork` с посторонними изменениями не тронут.

Входящие SHA256:

- Hardware.dll: `A250A39081AA608AC606629A5F858382607E1C1C216290C6DDB46D32947CF21B`.
- ymfm_opl4.dll: `9E19FB66EEAC3BE4866559BB1EADD0004007334716CDE7940FD8A7EB1E9308A8`.

Новый пакет:
`L:\Work_two\ZX\ZXMAK2-v77-ZXEVO-BC-MoonSound-NedoOS-ENDOS-Test-20261004`.
Заменены только Hardware.dll и Test.exe; остальные исполняемые компоненты,
ROMS.PAK, инструменты/SF2 сохранены побайтно. Добавлена README-v77.txt вместо
README-v76.txt. Full сохраняет текущую конфигурацию; Clean исключает только
vmz/vmide/cmos и внешний дубликат Yamaha ROM. Yamaha и Max GS остаются в PAK.
Откат: запуск прежней папки v76; переписывать её не требуется.
Локальный diff к b24c714 находится в source/. На GitHub ничего не отправлено.

## Причина: внутренний DOSEN не является входом ENDOS платы

В `MemoryPentEvo` чтение #0CBE действительно отдаёт DOSEN в бите 4.
CPM удерживает внутренний DOSEN=true: это правильное состояние NedoOS,
а не свидетельство отключения звуковой карты. Этот latch не изменён.

В публичном CPLD 1.00 автора:

```
ENDOS = (!C_DOS & C_PEVO) # !C_IODOS;
ENIO = C_IORQ # ENDOS # !C_M1 # !C_MREQ;
```

На схеме MoonSound Rev.01 JP1 «PentEvo» замыкает C_PEVO на землю.
Вход IODOS/ подтянут R5. На схеме ZX-Evo Rev.C контакты XS1:B20 и XS2:B20
не подключены: IODOS не следует подменять сигналом выбора VG93.
При штатной замкнутой JP1 C_PEVO=0, C_IODOS=1, значит ENDOS=0 независимо
от внутреннего DOSEN. Именно поэтому `!Memory.DOSEN` ошибочно отключал
все порты MoonSound под NedoOS.

Дополнительная сверка BaseConf r1364:
`top.v: assign dos_n = rompg[1]`, `zmem.v: win=za[15:14]`,
`rompg=page[4:0]`. Внешний /DOS определяется битом физической страницы,
выбранной адресом I/O; внутренний `zdos.dos` используется отдельно.
Локальный reference commit: `24ad52f7a15642c2b71f979c895d5de191a933a8`.

Это сверка схемы Rev.01 и публичного RTL 1.00, а не заявление о наличии
исходного RTL закрытой прошивки 1.01. Уже работающие latch/flash-изменения
v75/v76 и native ymfm DLL не менялись. Реальный запуск подтверждает выбранное
состояние аппаратных входов для PentEvo.

## Короткая трасса

Захват сделан на границе обработчика платы: полный адрес, PC, DOSEN,
доступность gate, handled до/после, возвращаемый байт; отказ помечен GATE.
Инструмент вызывает все прежние обработчики в прежнем порядке.

| Сборка | Порт | PC | DOSEN | Доступен | handled до/после | Байты |
|---|---|---|---|---|---|---|
| v76 | #0CBE | #8BA8 | true | n/a | false/true | #3E |
| v76 | #00C6 | #96E1 | true | false | false/false | #FF, GATE |
| v76 | #FFC4 | #96E6 | true | false | false/false | #FF, GATE |
| v77 | #0CBE | #8BA8 | true | n/a | false/true | #3E |
| v77 | #00C6 | #96E1 | true | true | false/true | #06 |
| v77 | #06C4 | #96E6 | true | true | false/true | #00 |
| v77 | #00C4 | #96F2 | true | true | false/true | #00 после очистки |
| v77 | #37C4 | #9710 | true | true | false/true | #A0 после ожидания |

Разница высокого байта #FFC4/#06C4 ожидаема: `IN A,(n)` использует прежний
A в старших адресных линиях; предыдущий IN #C6 вернул разные значения.
Это не исправление маски портов.

Первая диагностика после исправления: `3E 06 00 00 A0`.
ROM #C2/#C3: `43 6F 70 79 72 69 67 68 74` («Copyright»).
RAM: исходный #00, инверсия #FF, результат #FF - запись/чтение совпали.
Неактивная #7E/#7F после переключения #C6 не обязана отдавать ROM: latch
прошивки 1.01 выбирает #C2/#C3; результат запасной пары не означает отказ RAM.
Повторный запуск в той же NedoOS снова обнаружил устройство и память.

OmniSound также проверена реальным запуском: OPL3/OPL4 найдены, та же строка
ROM и успешная RAM-инверсия. Общий MoonSound-обработчик отдаёт правильные байты.

## Изменённые файлы

- `src/ZXMAK2.Hardware/Evo/ZxmMoonSoundDevice.cs`: модель JP1/подтяжки IODOS,
  gate физических входов вместо `!DOSEN`, адрес I/O передаётся в gate.
- `src/ZXMAK2.Hardware/Evo/MemoryPentEvo.cs`: только helper внешнего /DOS
  по выбранной странице; внутренняя логика DOSEN/CPM не менялась.
- `src/Test/Program.cs`: тест низкого IODOS для чтения/записи всех 8 портов,
  тест открытой JP1 на RAM/ROM-страницах; прежние тесты/benchmark сохранены.
- `tools/MoonSoundTest/RealNedoProbe.cs`: изолированное реальное воспроизведение
  с клавиатурой гостя, короткими CSV-трассами и снимками видеобуфера.
- `tools/MoonSoundTest/Package-v77.ps1`: Full/Clean и проверка SHA256 содержимого.
- Документы отчёта/контрольной точки. Никаких несвязанных правок.

## Выполненные проверки

- Release solution собран; только прежние warnings об отсутствующих ruleset.
- RealNedoProbe: реальный ROM/SD Boot/NedoOS/gp из ZIP пользователя,
  на тех же аппаратных DLL и той же конфигурации. Без оконного интерфейса:
  это запуск движка эмулятора, не синтетическая замена OS/плеера.
- v76: получена та же ошибка/все FF. v77: успешное обнаружение после очистки
  RAM/холодного сброса и после выхода/повторного запуска gp в той же OS.
- Реальный NedoOS/gp с OmniSound: обнаружение/ROM/RAM успешны.
- /moonsound, /omnisound: 44.1/48/96 kHz PASS; FM/PCM ненулевые,
  SRAM, CPLD 1.01, MX29F016, timer, atomic flash fixture PASS.
- /multisoundmax PASS; /omni-opl DOSEN=0 и 1 PASS, PCM идентичен.
- Аппаратный ENDOS gate сохранён: при низком IODOS все FM/PCM чтения и записи
  отпущены, с открытой JP1 и ROMPG1=0 заблокирован, с ROMPG1=1 доступен.
- Неизменённые MoonService v0.3a routines init/chip/flash ID PASS
  (YMF ID #20, MX29F016 #C2/#AD). Это не полный RAM/ROM тест меню.
- Обычный Test.exe: ULA sanity и встроенный benchmark PASS.
- Фактическая частота реальной NedoOS в трассах: 14 МГц.

## Непроведённые / неполные проверки

- Полный MoonService из TRD с RAM/ROM menu tests и прослушивание дискового
  проигрывателя в оконном эмуляторе. Через BaseConf TRD загружается в RAM-диск,
  но автоматизация остановилась в MAXBOOT: не выдавать это за успешный тест.
  Эти функции ранее подтвердил пользователь; в этом прогоне проверены только
  vendor routines и цифровой FM/PCM автоматических тестов.
- Реальный OmniPlayer 1.0.31 (предоставлен только диагностический 1.0.32).
- Полный реальный gp на 3,5/7 МГц: NedoOS восстанавливает 14 МГц при смене
  контекста; попытку смены скорости не считать проведённым тестом.
- Полная функциональная проверка других программ/форматов и новая проверка
  клавиатуры/фокуса Windows: эти компоненты не менялись.

## Материалы и воспроизведение

diagnostics/ пакета содержит before.csv, after.csv, repeat.csv, omni.csv,
снимки диагностики, regression.txt, benchmark.txt и этот отчёт.
Полные рабочие данные остаются в игнорируемом build/nedo-v76 рабочего checkout.
Устройство тестировалось с отдельной копией SD (bin/System/ini/корень),
не с пользовательской исходной папкой; diagnostic ZIP распакован только туда.

После компиляции RealNedoProbe для x86 рядом с runtime DLL:

```
RealNedoProbe.exe <исходная.vmz> <копия-SD-папки> <папка-трассы> key:D5 wait:300 key:F10 key:Return gp key:Return key:Escape key:Y gp
```

Для OmniSound добавить `board:OmniSound`; это меняет только конфигурацию
экземпляра test harness. Сброс `cold` очищает RAM и аппаратно сбрасывает VM.
Нельзя подставлять реальную пользовательскую SD: mount может создать обычное
состояние записываемой сессии, несмотря на read-only характер текущего gp.

Первичные источники: [Micklab](http://micklab.ru/My%20Soundcard/ZXMMoonSound.htm),
[схема MoonSound Rev.01](http://micklab.ru/file/zxm_moonsound/zxm_moonsound_01.pdf),
ZX-Evo `pentevo/docs/revC/zxevo_sch_revc.pdf` и `pentevo/fpga/base`
локального reference checkout. Схемы сохранены в diagnostics/reference/.
