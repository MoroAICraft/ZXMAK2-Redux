# v84 checkpoint / рабочая основа

Пользователь отменил v83 из-за нагрузки и задержки звука. Требование:
взять принятую v82 с NMI/PrintScreen из BC Commander, убрать Legacy v81 из ULA
и оформить как v84; это основа дальнейшей работы, не перенос FIR v83.

Ветка codex/release-v84-bc-nmi создана от 70f2e0c (v82).
Из исходного коммита 654a891 перенесена только NMI-правка cherry-pick 6a88ec8;
родительские изменения v83 не включены. Исторический сборщик перенесён в
tools/history/build_v82_nmi.ps1, чтобы не путать его с текущей сборкой.

Контроль D:\release82_BC058_NMI: Engine/Host/Hardware точно совпали с выпущенной
v82. Подтверждено чтением SHA256. В staging runtime скопированы 58 runtime
файлов этой принятой копии; пользовательская D: папка не изменена.

Staging: L:\Work_two\ZX\ZXMAK2-v84-ZXEVO-BC-NMI-PrintScreen-20261008.
baseline-v82-bc-nmi хранит контроль. Пересобран только WinForms frontend:
PrintScreen release→VM/NMI + удалён Legacy v81 checkbox. Apply ULA settings
не сохраняет скрытый старый mixer; XML legacy остаётся обратно совместимым.
Master volume/ULAplus сохранены. Движок/звук/native/ROM/SD не изменены.

Сборка успешна; проверки PrintScreen keyup-only/down-up/hold/disabled label,
F10/F12, keyboard, video, interrupts, SD и sound cards прошли.
583176 audio checks и десять полных machine/IO/video/PCM/tact fingerprints
совпали с v82. Full/Clean ZIP (58/55 файлов) проверены по entry SHA256.
Runtime/контроль очищены от диагностических программ переносом в diagnostics.
Конечный отчёт V84_RELEASE_REPORT.md. Итоговый tag zxmak2-v84-bc-nmi;
source+OmniSound и обе полные local histories экспортируются после commit.
