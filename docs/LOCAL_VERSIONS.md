# Локальные версии — единая точка входа

Актуальные исходники: `L:\Work_two\ZX\ZXMAK2-Fork`.
Актуальная ветка звука: `codex/audio-v82`, основа — контрольная v81.
Не путать с историческими рабочими копиями в `.codex/worktrees`.

| Версия | Исходники | Пакет в L:\Work_two\ZX | Назначение |
| --- | --- | --- | --- |
| v79 | `1993dd0`, метка `zxmak2-v79-control` | ZXMAK2-v79-ZXEVO-BC-F10-GuestOnly-Test-20261007 | Контроль до оптимизации: F10 только машине в главном окне |
| v78 | `29248ad` | ZXMAK2-v78-ZXEVO-BC-Portable-SD-Folder-Test-20261005 | Предыдущий откат: портативные SD-сессии |
| v77 | `d984e2c` | ZXMAK2-v77-ZXEVO-BC-MoonSound-NedoOS-ENDOS-Test-20261004 | Исправление физического ENDOS MoonSound |
| v80 | ветка `codex/optimization-v80`, метка `zxmak2-v80-optimized` | ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007 | Оптимизация без изменения эмуляции/звука; отчёт OPTIMIZATION_V80_REPORT.md |
| v81 | ветка `codex/optimization-v81`, метка `zxmak2-v81-optimized` | ZXMAK2-v81-ZXEVO-BC-Engine-Optimization-Test-20261007 | Второй проход движка: timing hooks / reuse routing tables; отчёт OPTIMIZATION_V81_REPORT.md |
| v82 | ветка `codex/audio-v82`, метка `zxmak2-v82-common-audio` | ZXMAK2-v82-ZXEVO-BC-Common-Audio-Test-20261008 | Общий wide mixer / fixed headroom / 5 Hz DC, legacy-переключатель, Moon Volume; AUDIO_V82_REPORT.md |

Пакеты более ранних версий сохранены в той же папке ZX; сопоставление старых
изменений — docs/history/PROJECT_JOURNAL.md и FORK_CHANGELOG.md.
Не переименовывать/удалять пользовательские ROM, музыку и образы.

v82: `release` — готовый runtime, Full/Clean ZIP в корне пакета; `baseline-v81`
содержит сохранённый контроль, `diagnostics` — PCM/PNG/логи и отдельные probe,
`source` — committed source + pinned OmniSound и полная локальная Git-история.
Для полного отката использовать прежний полный v81 архив, не смесь DLL.
UI-переключатель Legacy audio mix (v81) откатывает только mixer/DC; не ошибку
игнорирования MoonSound.Volume. Четыре старых untracked engine-research файла
не изменены; экспорт разрешает именно этот явный список, tracked edits запрещены.

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
