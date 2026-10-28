# Локальные версии — единая точка входа

Актуальные исходники: `L:\Work_two\ZX\ZXMAK2-Fork`.
Новая сборка для проверки: **v92 BaseConf Prefinal**, ветка `codex/prefinal-v92`.
Принятая контрольная версия и полный откат: **v84**.
Основа — принятая v82 с BC Commander / PrintScreen → NMI, НЕ v83.
Не путать с историческими рабочими копиями в `.codex/worktrees`.

| Версия | Исходники | Пакет в L:\Work_two\ZX | Назначение |
| --- | --- | --- | --- |
| v79 | `1993dd0`, метка `zxmak2-v79-control` | ZXMAK2-v79-ZXEVO-BC-F10-GuestOnly-Test-20261007 | Контроль до оптимизации: F10 только машине в главном окне |
| v78 | `29248ad` | ZXMAK2-v78-ZXEVO-BC-Portable-SD-Folder-Test-20261005 | Предыдущий откат: портативные SD-сессии |
| v77 | `d984e2c` | ZXMAK2-v77-ZXEVO-BC-MoonSound-NedoOS-ENDOS-Test-20261004 | Исправление физического ENDOS MoonSound |
| v80 | ветка `codex/optimization-v80`, метка `zxmak2-v80-optimized` | ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007 | Оптимизация без изменения эмуляции/звука; отчёт OPTIMIZATION_V80_REPORT.md |
| v81 | ветка `codex/optimization-v81`, метка `zxmak2-v81-optimized` | ZXMAK2-v81-ZXEVO-BC-Engine-Optimization-Test-20261007 | Второй проход движка: timing hooks / reuse routing tables; отчёт OPTIMIZATION_V81_REPORT.md |
| v82 | ветка `codex/audio-v82`, метка `zxmak2-v82-common-audio` | ZXMAK2-v82-ZXEVO-BC-Common-Audio-Test-20261008 | Общий wide mixer / fixed headroom / 5 Hz DC, legacy-переключатель, Moon Volume; AUDIO_V82_REPORT.md |
| v82 BC/NMI | исходная правка `654a891`, tag `backup/v82-printscreen-nmi` | D:\release82_BC058_NMI (пользовательская тестовая копия); контроль сохранён в v84/baseline-v82-bc-nmi | Принятый PrintScreen → штатный NMI; core/audio от v82 |
| v83 | историческая `codex/audio-v83`, tag `zxmak2-v83-ay-hq` (`b91b8ad`) | ZXMAK2-v83-ZXEVO-BC-AY-HQ-Test-20261008 | **ОТМЕНЕНА пользователем**: нагрузка/задержки FIR. Архив эксперимента, не основа работы |
| v84 | `codex/release-v84-bc-nmi`, tag `zxmak2-v84-bc-nmi` | ZXMAK2-v84-ZXEVO-BC-NMI-PrintScreen-20261008 | **ТЕКУЩАЯ**: v82 + принятый NMI/PrintScreen; Legacy v81 удалён из UI; V84_RELEASE_REPORT.md |
| v85 | `codex/sd-refresh-v85`, tag `zxmak2-v85-sd-refresh` | ZXMAK2-v85-ZXEVO-BC-SD-Refresh-Test-20261009 | Refresh с выборочным сохранением сессии, внешние PC-правки, штатный холодный старт; SD_REFRESH_V85.md |
| v86 | `codex/release-v86-tape-sd`, tag `zxmak2-v86-tape-sd` | ZXMAK2-v86-ZXEVO-BC-Tape-SD-Test-20261010 | Два патча BC: Tape для REAL, правильные R1/CRC/data response SD; CPU/NMI не изменены; TAPE_SD_V86.md |
| v87 | `codex/portable-v87`, tag `zxmak2-v87-portable-test` | ZXMAK2-v87-Portable-Test-20261010 | **ТОЛЬКО ТЕСТ**: переносимая структура, локальные настройки/flash/сохранения, защита внешних оригиналов, новый Help; PORTABLE_V87_REPORT.md |
| v88 | `codex/soundcards-v88`, tag `zxmak2-v88-soundcards-test` | ZXMAK2-v88-Portable-SoundCards-Test-20261010 | **ТОЛЬКО ТЕСТ**: общее ymfm YM2203 для TSFM/MultiSound/Max/Omni, Max host-rate, IDE/CMOS paths; SOUNDCARDS_V88_REPORT.md |
| v89 | `codex/sd-folder-v89`, tag `zxmak2-v89-sd-folder-test` | ZXMAK2-v89-Portable-SD-Folder-Fix-Test-20261010 | **ТОЛЬКО ТЕСТ**: long SD paths, живой save progress, Refresh state, close-error resume; SD_FOLDER_V89_REPORT.md |
| v90 | `codex/sd-direct-v90`, tag `zxmak2-v90-direct-sd-test` | ZXMAK2-v90-Portable-Direct-SD-Folder-Test-20261011 | **ТОЛЬКО ТЕСТ**: Save в выбранную SD-папку без клона/смены пути; SD_DIRECT_V90_REPORT.md |

v91: исходная точка v92 — `877816002ce9c9ae166418755e01f2d7e6040a5b`,
пакет `ZXMAK2-v91-Extreme-Rev03-Test-20261028` (Extreme Rev.03).
v92: `ZXMAK2-v92-BaseConf-Prefinal-Test-20261029`; Ctrl+F, BaseConf opening,
Open With, безопасное применение звукового оборудования. Статус — тест,
не заменяет рабочую версию без подтверждения пользователя.

Пакеты более ранних версий сохранены в той же папке ZX; сопоставление старых
изменений — docs/history/PROJECT_JOURNAL.md и FORK_CHANGELOG.md.
Не переименовывать/удалять пользовательские ROM, музыку и образы.

v88 продолжает исходники v87, не становится рабочей автоматически. Пользователь
подтвердил замену YM2203 на ymfm с проверками, но приёмка сборки/переход к
дальнейшей работе требуют отдельного подтверждения. В пакете baseline-v87,
Full/Clean ZIP, исходники с OmniSound, обе Git history и V88_RELEASE_REPORT.md.
MoonSound и отменённый v83 FIR не менялись/не включались. Нагрузка активного FM
в локальном тесте снизилась, idle вырос на 0,09–0,17 мс/кадр; детали — в отчёте.

v87 НЕ принята автоматически: обязательны явное подтверждение пользователя,
что сборка работает, и отдельное согласие продолжать работу на ней. Принятая
v84 и полные откаты v85/v86 сохранены. Пакет содержит baseline-v86, Full/Clean ZIP,
source с pinned OmniSound/Git bundles, checkpoints и отчёт V87_RELEASE_REPORT.md.
Корневые boot.zip/ROMS.PAK/VMZ/VMIDE/CMOS/NVRAM остаются на месте. Нет набора SD
или BC Commander в runtime. Новая аппаратная эмуляция и тяжёлый v83 звук не добавлены.

v82: `release` — готовый runtime, Full/Clean ZIP в корне пакета; `baseline-v81`
содержит сохранённый контроль, `diagnostics` — PCM/PNG/логи и отдельные probe,
`source` — committed source + pinned OmniSound и полная локальная Git-история.
Для полного отката использовать прежний полный v81 архив, не смесь DLL.
UI-переключатель Legacy audio mix (v81) откатывает только mixer/DC; не ошибку
игнорирования MoonSound.Volume. Четыре старых untracked engine-research файла
не изменены; экспорт разрешает именно этот явный список, tracked edits запрещены.

## Текущая структура v84

v85 имеет ту же структуру с `baseline-v84`. Архивы runtime, source с OmniSound
и две истории Git сохранены отдельно. 157 проверок Refresh и реальный NedoOS
проходят; 10 machine/PCM fingerprints совпадают с v84. Звук/CPU/NMI не изменены.
Следующая проверка пользователя — v85; v84 остаётся принятой точкой отката.

v86 продолжает актуальные исходники после v85 (`cea0ac7`, `f68d667`), не
откатывает их. Runtime отличается от опубликованной v85 только Hardware и
Circuits DLL плюс README; `baseline-v85` сохранён полностью. 6184 SPI checks,
29 native BC tape/write сценариев, прежние Refresh/close/audio проверки проходят.
Отдельный дефект CPU NMI на префиксе из входного документа пока не исправлен.
Для новой проверки брать v86; v85 и v84 остаются сохранёнными откатами.

`release` — runtime; Full/Clean ZIP — в корне пакета; `source` — исходники
с закреплённым OmniSound и полная local Git history двух репозиториев.
`baseline-v82-bc-nmi` — 58 runtime файлов принятой D:\release82_BC058_NMI,
без диагностических exe и чужой SD-папки. `diagnostics` — проверки,
снимок UI, SHA256, служебные программы и прежние README.
Главный каталог всех версий: L:\Work_two\ZX\ZXMAK2-RELEASES.md.

Движок/Hardware/звуковой mixer/native/ROM/SF2 v84 побайтно сохранены от v82;
пересобран только WinForms frontend. В исходниках нет FIR/HQ из v83.
Для полного отката использовать сохранённую v82, не смешивать DLL разных версий.
Legacy v81 отсутствует в интерфейсе; старый XML читается для совместимости,
Apply настроек ULA возвращает принятый v82 mixer. Master 0..200% сохранён.
PrintScreen на отпускании вызывает VM → NMI; для окна BC нужен установленный
NMI-резидент самого BC Commander. Репозиторий/Build BC Commander не изменены.

v83 и её source/history/результаты не удалены: эксперимент отмечен отменённым
в каталоге версий и RELEASE-STATUS.md. Новые работы начинать только от v84.
Пользовательские D: каталоги не перезаписывались. Четыре preexisting untracked
engine-research файла сохранены без правок. Удаления пользовательских данных нет.

## Архив перед обновлением основного checkout

`L:\Work_two\ZX\ZXMAK2-Local-Archive-20261007` содержит:
- `control-v79/ZXMAK2-v79-source.zip`: полный tracked source основной части.
- `control-v79/ZX-OmniSound-submodule-0794d8d.zip`: содержимое закреплённого
  submodule; Git archive основной части сам не включает gitlink contents.
- `ZXMAK2-local-history.bundle`: все локальные ветки/метки/история, включая
  сохранённые HEAD старых рабочих копий; bundle verify прошёл.
- `primary-wip/source-with-wip.zip`, patch и точные исходные файлы незавершённой
  adaptive audio правки; её ветка `codex/archive-primary-audio-wip-20261007`,
  commit `cdb6fca`. Это НЕ часть контрольной v79 и НЕ часть оптимизации.
- `primary-wip/unpublished-runtime`: бывшая `.codex-build`, перенесённая целиком.
- manifest и SHA256. Существующие выпущенные архивы не перезаписаны.

Удалены только регистрации пяти уже отсутствующих worktree, после сохранения
их HEAD в refs/archive и bundle. Реальные активные worktree не удалялись.
`backup/` и исследовательские материалы `tmp/` остаются историческими данными,
не текущими исходниками. Пять лишних ZIP-verification распаковок уже архивированы
с проверкой всех файлов, затем удалены. Восстановление: архив/redundant-extractions.

Исходники v80 (содержимое submodule включено) и две полные local history bundle:
`ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007/source/`.
В runtime ZIP только release; контроль и диагностические материалы лежат отдельно.
Для v81 полный source+submodule ZIP и две history bundle находятся в аналогичной
папке v81/source; v80 и её архивы не изменены. Пользовательский D: runtime v80
проверен по DLL/EXE/ROMS.PAK перед созданием независимой копии для v81.

## Сборка и контроль

VS 2022 BuildTools MSBuild: src/ZXVM.sln, Release, Any CPU, /m:1.
Утилиты/эмулятор используют x86 runtime; для Test.exe включить CorFlags /32BIT+.
Не запускать Test /perf или /cpu без ограничения: оба режима бесконечны.
Для замеров использовать ограниченный OptimizationProbe и обычный Test.exe.
Native DLL, ROMS.PAK, конфиги и музыкальные зависимости брать из контрольной
v79; не заменять их случайными файлами из старых временных папок.
Встроенный Test — benchmark движка, не измерение оконного/GPU backend целиком.
