# v90 Portable Direct SD Folder — TEST, 2026-10-11

Пакет: `L:\Work_two\ZX\ZXMAK2-v90-Portable-Direct-SD-Folder-Test-20261011`.
Ветка `codex/sd-direct-v90`, метка `zxmak2-v90-direct-sd-test`.
Основа: v89 `539da34ef8959e79cc15c8baf60c94ac13e4d433`.
OmniSound `756cb473b75390fe6fb7fa27504c8ca8c9886588` не менялся.
Контрольная точка реализации: `8b414ed`.
Ранее принятый контроль v84 и предыдущие v88/v89 сохранены. Публикаций на GitHub нет.
**v90 не становится рабочей до явного подтверждения пользователя.**

## Согласованная логика

Пользователь отменил full-copy policy именно для подключаемых host SD-папок.
Переносимость самого эмулятора не требует переносить с ним всю музыку/SD.

- Выбранная папка постоянно остаётся источником. При Save сохраняются только
  созданные/изменённые/удалённые машиной элементы прямо в этой папке, даже если
  она вне каталога эмулятора. Другие PC-файлы не копируются и не переписываются.
- PC-only Refresh не спрашивает о сохранении. При изменениях файлов в guest —
  Refresh(Save changes), Refresh(Without saving), Cancel. Save применяет guest
  delta, затем перечитывается та же папка. Независимые PC-правки остаются.
- Один файл изменён обеими сторонами: отказ с объяснением, не автоматическая
  перезапись. Сессия остаётся до Cancel/Discard или разрешения пользователем.
- После успешного Refresh заменяется snapshot виртуальной SD и выполняется
  холодный старт машины с очисткой старого RAM media state. Закрывать ZXMAK2
  не требуется. Ошибка/Cancel не выполняют этот cold start.
- Exit/крестик/eject спрашивают только при настоящих file changes машины,
  не при PC-only изменениях или FSInfo-only writes. Yes сохраняет в выбранную
  папку; No отбрасывает; Cancel сохраняет текущую сессию открытой.
- При запуске текущая папка пересчитывается. Если путь отсутствует/недоступен,
  SD-слот пустой, без вопроса/создания папки/подстановки образа. Остальные
  настройки не сбрасываются. Прежний descriptor может храниться, чтобы папка
  снова нашлась при возвращении диска; пользователь может выбрать другой путь.

Автоматическая полная копия HostFolder и переключение SourceFolder удалены из
кода; поле источника readonly. `NewSavePath(..., "SD-Folders")` больше не
используется storage/device-кодом. Выбранная папка — точечное разрешённое место
записи, а не снятие защиты всех внешних путей. `PortablePaths.WriteAtomic`
продолжает запрещать внешние собственные сохранения эмулятора.

Настройки, журналы/sector overlays, frozen originals и staging остаются внутри
эмулятора. Для отката транзакции в выбранной папке временно переименовываются
только затронутые старые файлы в небольшой private rollback directory; после
успеха/полного отката он удаляется. Это не дубликат дерева. Изменение большого
отдельного файла может потребовать его snapshot/стейджинг и место под него;
обещания нулевых затрат для любого размера нет.

Для отдельно подключённых SD IMG, HDD и floppy сохранена v89 local-copy policy;
пользователь в этом этапе меняет только host folder. Экспорт folder→IMG только
по явной команде. Старые Data/Saves/SD-Folders без команды не удалялись.
Если старый VMZ уже указывает на такую копию, **нужно один раз выбрать исходную
папку через Mount folder as SD**; приложение не угадывает старый L: путь и не
мигрирует пользовательские файлы молча.

## Проверка переносимости / дополнительные ясные исправления

EXE root, не CWD/managed DLL folder, остаётся базой собственных относительных
путей. Internal sources хранятся относительно EXE, внешняя SD-папка — абсолютным
путём. Переезд на другой PC с недоступным внешним источником оставляет пустой
слот. Корневые boot.zip/ROMS.PAK/VMZ/VMIDE/CMOS/NVRAM не переносились заново.
Журналы не восстанавливают старый folder overlay поверх новой PC-папки.
Windows registry/AppData reads в проверенных host/sound компонентах остаются
только необязательным прежним импортом; новых системных записей не добавляли.

В путях подтверждён filename-component дефект: даже extended full paths не
разрешают имя >255 символов. Старый temp suffix добавлялся прямо к длинному
имени SD-файла/IMG и atomic-save target. Теперь temporary sibling имеет короткое
GUID-имя. Для atomic backup имени >251 используется SHA256 basename, чтобы
`.bak` тоже помещался. Проверено на SD имени 244 и atomic имени 255 символов.
Обычные backup paths и ограничения конфликта/RequireInternal сохранены.

Source D: установки и реальная L: коллекция во время разработки не менялись.
Старый v89 probe mode `real`, который рассчитывал на копирование, отключён:
после смены policy он мог бы записать в настоящую пользовательскую папку.
Все write-проверки v90 используют только специально созданные fixtures.

## Выполненные проверки

- Full solution Release/Any CPU; x86 runtime. Только старые warnings ruleset.
- **64 targeted checks:** direct Save/Refresh в том же внешнем fixture path,
  PC-only без prompt; независимые PC edits/additions; same-file collision;
  Save/No/Cancel/Exit/restart; missing quiet empty; FSInfo-only close; zero clone;
  relative/CWD/menu states, malformed optional descriptor, config-write-error
  resume/retry, progress worker/error/Alt+F4 structural checks; deep folders,
  long IMG unchanged-policy, 244-char SD name и 255-char atomic replace/backup.
- **9 large checks:** 10002 файлов при initial mount, затем PC addition,
  неизменённый payload 3 GiB в disposable folder. Он закрыт эксклюзивной lease,
  не может читаться/копироваться. Реальный MainViewModel.PrepareClose с запущенной
  VirtualMachine и worker успешно сохраняет только изменённый TEST.TRD, PC.CFG
  остаётся, source path тот же и SD-Folders copies не появляются. Время от mount
  до проверки close ~0,670 с, не весь setup 10000 файлов. Это синтетический
  filesystem/lifecycle test, не запуск реального NedoOS/OmniPlayer.
- Прежние lifecycle 22, Refresh 157, SPI 6184; portable core 64 (обновлённый
  folder contract), templates/frame/switch 78, relocation 15 — PASS.
- Keyboard 565, PrintScreen/F10/F12, renderer 7, contention 46, INT/NMI 41,
  WAIT 197981, video 12748, tape clock, machine-settings round-trip — PASS.
- Moon/flash, OmniSound/OPN, Max/OPL3, master volume/EVO display и полный Test
  проходят. Синтез/звук, CPU/video/keyboard/NMI/SD SPI не переделаны.
- 583176 common audio checks; прежний Moon PCM hash 9F3EE7BD381A9180 сохранён.
  Десять CPU/RAM/video/PCM/I/O fingerprints совпадают с v89 при одном VMZ,
  включая 3,5/7/14 МГц и переключение машин. CPU/Disk/Tape DLL и все native,
  boot.zip/ROMS.PAK/SF2 совпадают с v89 по SHA256.

Логи: package/diagnostics и diagnostics/verification/diagnostics. Fixtures
лежат только в diagnostics; в release/ZIP их нет. Никаких больших SD-коллекций
или BC Commander в runtime не добавлено.

## Комплект / ограничения

Изменения production: HostFolderFat32Stream Commit/readonly source, ZsdPentEvo
prompt/file-dirty/quiet restore, UserQuery progress note, PortablePaths atomic
temporary/backup names. Остальное — tests/help/README/catalogue.

Full/Clean ZIP, release, source с pinned OmniSound + оба verified Git bundles,
точный baseline-v89, checkpoints, diagnostics и отчёт. Clean убирает только
активные root sidecars. Native libraries, ROM/bank/boot dependencies сохранены.
Для отката взять целиком предыдущую версию; изменения в выбранной SD-папке
уже являются пользовательскими данными и не откатываются заменой EXE.

Не проведены ручной GUI выход из настоящего OmniPlayer/NedoOS, видимое окно
progress/4K, физический другой PC/флешка/FAT/exFAT, UNC/offline latency и все ACL/
disk-full/антивирус сценарии. Реальная коллекция не использовалась для write-теста.
Windows/.NET 4.6.2+ (4.8 рекомендован), x86 native/optional DirectX/XNA зависимости
остаются. Нельзя обещать отсутствие всех системных следов Windows/драйверов или
100% переносимость. Известный CPU NMI prefix-boundary дефект вне этого этапа.
При неполном rollback private backup сохраняется с сообщением о его месте;
чужие PC-версии во время транзакции не удаляются молча.
