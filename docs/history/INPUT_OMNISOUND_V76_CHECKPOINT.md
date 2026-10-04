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

Реализация сохранена в `82ea81c`. Общий HostKeyboardRouter: реестр команд,
фильтр WinForms сообщений до dispatch, per-key latch до реального отпускания,
учёт вложенных меню/Alt-F10/toolbar focus/dispose. DirectInput повторяет Acquire
после любой потери, отслеживает новый HWND, синхронизирован с UI/dispose.
WinForms fallback использует физический GetAsyncKeyState + тот же router,
исправлено ошибочное соответствие RightShift=ControlKey. VM warm/cold reset
вызывает optional IResettableHostKeyboard. Исправлен null joystick при Scan.

PASS: KeyboardRoutingProbe (483 assertions): все guest keys, все текущие host
chords, AltGr, неназначенные Ctrl+P/Ctrl+D/Alt+D, удержание Escape после close,
независимость стрелок, вложенные/удалённые меню, focus-loss, failed Acquire,
NOTACQUIRED/INPUTLOST, recreate HWND, concurrent reset/scan, Max Apply+Connect.
PentEvoProfileProbe PASS: Music/ZXBUS/Omni slots/volumes/SD. Старый probe
обновлён: у SD меню теперь есть Save as image; вместо удалённых отдельных
shortcut-фильтров проверяется общий router.
Test.exe: /moonsound, /multisoundmax, /multisound, /tsfm, /omni-opl,
/hostfoldersd, /master-volume PASS; /omnisound PASS при 44100/48000/96000,
включая новые CPLD/flash-ID assertions в составе Omni. Benchmark сохранён
и default Test.exe завершился с exit 0.

GUI Computer Use: v76 запускается, SD dropdown открывается/навигация/Escape
закрытие без изменения guest selection; Alt+P открывает Machine Settings.
Проверка физического guest ввода через helper НЕ подтверждена: его короткие
инъекции не видит даже отдельный KeyboardPhysicalMonitor с polling 1ms.
Не заявлять ручной PASS arrows-after-reset; пользователь проверит реальными
нажатиями. Helper не позволяет нажать Cancel в дочернем modal окне (target
window mismatch), поэтому закрыт только созданный нами процесс v76, без
Apply/изменения пользовательских настроек. Другие процессы не затронуты.

Финальная папка: L:\Work_two\ZX\ZXMAK2-v76-ZXEVO-BC-OmniSound-Keyboard-Test-20261004.
v75 runtime/ROMS.PAK/unity.config — основа пакета; изменённые DLL/EXE заменены,
32BIT_REQUIRED EXE восстановлен после MSBuild. DirectSound исходник v75
соответствует её DLL (adaptive-buffer полей нет); посторонний незакоммиченный
adaptive patch в основном checkout НЕ переносился. Настройки 195% и звук
предыдущей сборки сохранены. ROMs/music/native DLL не менялись.

Завершено: README-v76.txt, Full/Clean ZIP. Все записи архивов проверены
по длине и SHA256 относительно release; Full — 59 файлов, Clean — 55.
Clean исключает только vmz/vmide/cmos и дублирующий внешний Yamaha ROM;
Test.exe, SF2, native DLL, лицензии и ROMS.PAK сохранены в обоих архивах.
После распаковки Clean: /moonsound, /multisoundmax, /omnisound PASS.

Full SHA256: 2FE72B0722A22B806E7BBEE57295E01DD2901F890A70F18EDD4B26BF671ACF6E
Clean SHA256: BF8B037681C40A93C0D7549DFE67DB36A86AAEC792E8AEF4F786B1923B59BA04
v75 оставлена для отката. GitHub не изменялся; финальные исходники и probes
сохраняются локальным commit вместе с этой записью.
