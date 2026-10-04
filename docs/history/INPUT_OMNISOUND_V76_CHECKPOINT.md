# v76 — OmniSound и единая маршрутизация клавиатуры

База: чистый worktree `codex/zx-omnisound-integration`, `8ecb595` (v75).
Основной checkout с пользовательскими изменениями не трогать. GitHub запрещён.
Готовую v75 в L:\Work_two\ZX сохранить как откатную; v76 — отдельная папка
и Full/Clean ZIP по прежней схеме. Test.exe, музыку, ROMS.PAK, лицензии оставить.

Проверено до изменений: OmniSound уже содержит тот же ZxmMoonSoundDevice,
исправленный в v75. MultiSound Max OPL4 не содержит; GS ROM fallback уже исправлен.
Не переносить MoonSound в Max и не дублировать ядро в OmniSound.

Найдены ошибки DirectKeyboard: Acquire failure всё равно устанавливает
_isAcquired=true; DIERR_NOTACQUIRED ничего не делает; старый HWND сохраняется
навсегда; Deactivate не очищает guest-state; общий latch всех стрелок.
WinFormsKeyboard также не очищает state при потере фокуса/Dispose.

Реализация: общий router для UI/DirectInput/fallback; единый реестр host-команд;
всё меню изолировано, включая Escape и отпускание после закрытия;
синхронизированный reacquire/handle lifecycle и reset. Добавить проверки
последовательностей input и Omni PCM/flash, Apply Max; собрать/упаковать.

Незавершено: реализация, проверки, упаковка. Продолжать отсюда.
