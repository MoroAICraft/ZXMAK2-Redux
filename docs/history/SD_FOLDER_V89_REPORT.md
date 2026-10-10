# v89 Portable SD Folder Fix — TEST, 2026-10-10

Пакет: `L:\Work_two\ZX\ZXMAK2-v89-Portable-SD-Folder-Fix-Test-20261010`.
Ветка: `codex/sd-folder-v89`; метка `zxmak2-v89-sd-folder-test`.
Основа v88: `ccb62c88e719882dd6637f070ab881008d97c4f3`.
OmniSound `756cb473b75390fe6fb7fa27504c8ca8c9886588` не менялся.
Контрольные сохранения кода: `2261a17`, `7419aac`; полная история в source bundles.
v88 и пользовательская `D:\release88_Portable_SoundCards` не перезаписывались.
Никаких отправок на GitHub. v89 станет рабочей только после подтверждения пользователя.

## Подтверждённая причина

Изучен журнал именно пользовательской v88; основные DLL/EXE совпали по SHA256
с опубликованной v88. В 19:13:28 журнал фиксирует `PathTooLongException` в
`PortablePaths.CopyDirectory → CommitExternalFolder → PrepareForMediaChange → PrepareClose`.
Сохранённая копия исходного журнала — `diagnostics/user-v88-log.txt`.

Внешняя папка `L:\Work_two\ZX\ZX_IMG\flash_pentevo` содержит 87549 файлов,
3546001188 байтов. По согласованной переносимой политике её первое явное Save
делает самостоятельную полную копию внутри эмулятора, а не меняет оригинал.
Префикс `Data/Saves/SD-Folders/<id>/flash_pentevo` удлинял пути; старое приложение
оставалось в режиме .NET MAX_PATH. Большой синхронный перенос без индикации
выглядел как зависание. Сам журнал не доказывает deadlock или отсутствие прав.

Для Refresh подтверждены две отдельные ошибки:

1. `Directory.Exists(configuredSourcePath)` проверял сырой относительный путь
   против CWD. После внутреннего сохранения/переноса он должен разрешаться от EXE.
2. Toolbar создавал command-wrapper с первоначальным Enabled=false и не обновлял
   его при смене носителя. Делегат CanExecute уже мог возвращать true, но пункт
   продолжал быть серым. Export обновлялся отдельно, Refresh — нет.

Также исправлена логическая ошибка закрытия: если носитель уже сохранён, но
затем сохранение VMZ падает, флаг accepted устанавливался слишком рано и VM
оставалась на паузе при отказе от выхода. Теперь accepted=true только после VMZ.

## Исправление

- EXE/Test app.config включает современную обработку путей на .NET 4.6.2+;
  для длинных файловых операций portable/SD используются явные Windows extended
  IO paths. Это IO-деталь: `\\?\` не сохраняется в дескрипторы. Для UNC есть
  отдельная корректная форма. Проверки внутреннего пути/reparse points сохранены.
  Настройки Windows/реестра не изменялись. На машине проверки LongPathsEnabled
  уже был равен 1; работа на машине с другим значением отдельно не испытывалась.
  [Документация Microsoft по opt-in .NET](https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/retargeting/4.6.x).
- До большого копирования проверяются пути назначения и linked directories.
  Сохранение остаётся copy-on-Save; исходная внешняя папка/IMG не переписывается.
- Во время folder Save/Save+Refresh показывается модальное окно с живой индикацией
  стадии, количества скопированных файлов и МБ. Файловая операция работает в
  отдельном потоке; guest заранее остановлен. Parent disabled, без Application.DoEvents
  и повторного входа в команды VM; Alt+F4 не бросает транзакцию посередине.
  По завершении ошибка возвращается на UI-поток и действующая логика отменяет
  выход/возобновляет ранее работающую машину. Прогресс нельзя прервать кнопкой:
  это не новый протокол отмены файловой транзакции. Force-kill по-прежнему авария.
- Refresh использует разрешённый путь и обновляет состояние меню; malformed
  optional descriptor не должен выбрасывать исключение из проверки меню.
- Чтение нетронутой FAT при проверке изменений берёт то же generated значение
  напрямую, вместо заполнения 128 записей для каждого кластера. Изменённые
  сектора FAT имеют приоритет; проверки циклов/shared chains/invalid clusters
  не убраны. Это ускоряет проверку большого дерева, не меняет данные карты.
- Проверены/адаптированы IO paths папки, копии, исходных snapshots, staging,
  journal, rollback/commit, повторного монтажа, экспорта IMG и image session.
  Время и протокол SD SPI, выбор FAT32/размеры/порядок BIN не изменены.

Согласованная логика сохранена: PC-only Refresh без вопроса; при guest changes —
Refresh(Save changes)/Refresh(Without saving)/Cancel. После успешного Refresh
штатный холодный старт как при смене SD. Exit/крестик/eject спрашивают о dirty
session; Cancel и ошибки не принимают выход. Внешний оригинал остаётся прежним;
после Save рабочим источником становится внутренняя копия. Поэтому дальнейшие
PC-правки для Refresh надо делать уже в этой копии, а не в прежнем L: оригинале.

## Проверки

- Вся solution Release/Any CPU; runtime EXE x86. Только прежние warnings ruleset.
- Новый PortableFolderProbe: **29 checks** — относительный descriptor при чужом
  CWD, enable/disable menu до/после изменения availability, invalid descriptor,
  VMZ-write error/resume/retry close, глубокая копия >260, сохранение/remount без
  skipped files, IMG mount/local save/remount, worker/report/error/completion и
  невозможность закрыть progress до окончания. UI-controls тестировались без
  показа окна, это не пользовательская проверка отображения окна на 4K.
- **Настоящая 87549-файловая папка:** оригинал только читается, одна виртуальная
  запись в TRD сессии, сохранение в disposable diagnostics/Data/Saves. Все файлы
  скопированы; изменённый TRD в копии содержит guest byte, оригинал прежний;
  самый глубокий файл (saved path **346 символов**) и дополнительный контрольный
  файл побайтно совпали. Полный хеш каждого музыкального файла не считался.
  Полная копия занимает ~89–91 с на этой машине/дисках, проверка SD changes ~1 с.
- После сохранения большой папки выполнен её Refresh при CWD=C:\Windows,
  relative XML descriptor и реальный `MainViewModel.PrepareClose` с работающей
  VirtualMachine, stopped guest и background transaction. Повторный Save,
  запись VMZ и принятие выхода прошли; **21 assertions**. Это headless-сценарий
  настоящего дерева, не воспроизведение реального OmniPlayer/NedoOS в GUI.
- Старые SD lifecycle 22, Refresh 157, SPI 6184, portable core 64, модели 78,
  relocation 15 проходят. Скопированный/переименованный Unicode/# runtime,
  foreign CWD, native sound и MIDI bank проверены.
- Keyboard 565, PrintScreen/F10/F12, renderer 7, contention 46, INT/NMI 41,
  WAIT 197981, video 12748, tape clock, machine settings — PASS.
- MoonSound/flash, OmniSound/OPN, Max/OPL3, TSFM, A2, master-volume, EVO display
  и полный Test — PASS. **583176** common audio assertions, прежний Moon PCM
  `9F3EE7BD381A9180` не изменился. v88 sound исправления не откатывались.
- CPU/Disk/Tape DLL, boot.zip, ROMS.PAK, SF2 и все native DLL побайтно прежние.
- Десять CPU/RAM/video/PCM/I/O fingerprints совпали с изолированной v88 при
  одном входном VMZ: 3,5/7/14 МГц и переключение машин включены.

Протоколы: `diagnostics`, в частности real-folder-save-final.txt,
portable-folder-core.txt, verification/diagnostics. Generated media (~7 ГБ за
два полных прохода) остаются в diagnostics, НЕ включаются в release/архивы.

## Файлы/пакет/ограничения

Production: `PortablePaths.cs`, HostFolderFat32Stream partials, SdCard,
SdImageSessionStream, ZsdPentEvo, MainView/MainViewModel, IUserQuery/UserQueryProxy/
WinForms.UserQuery, EXE configs. PrepareRuntime теперь также включает обновлённый
Host.dll с optional progress interface; Unity mappings не надо переделывать.

Пакет: release, Full/Clean ZIP, точный baseline-v88, исходники вместе с pinned
OmniSound и две local Git history, отчёт, диагностика/checkpoints. Clean убирает
только root active sidecars, сохраняет весь звук/ROM/Test. Большая SD-коллекция
и BC Commander не входят. Пользовательские D: файлы и прежние архивы не изменены.

Не проведены ручной exit именно из OmniPlayer/NedoOS, физический второй ПК,
реальное окно progress/4K/Alt+F4, UNC server/offline, FAT/exFAT USB, полная запись
диска до заполнения и все варианты ACL/antivirus. Требуется .NET 4.6.2+ для
длинных путей, рекомендуется 4.8; старый .NET не получает эту поддержку автоматически.
Остаются пределы Windows компонентов/имён, допустимости guest имён, места на
диске и выбранного frontend. Не обещается 100% переносимость/невозможность аварий.

Если копирование оборвётся из-за места/прав/аварии, возможна неиспользованная
частичная копия в Data/Saves; её путь виден в журнале. Ничего из пользовательской
D: папки, её старых частичных копий или источника L: автоматически не удалялось.
Пользовательские изменения между Save и проверкой того же файла по-прежнему
не перезаписываются молча. Старые незавершённые folder journals при startup не
восстанавливаются: согласованное правило пересчитать текущую папку сохранено.

Для отката взять целиком прежнюю v88, не смешивать DLL. Собственные Data/Saves
сохранить отдельно. v89 — TEST до явного пользовательского подтверждения.
