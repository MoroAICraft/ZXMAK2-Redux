param([Parameter(Mandatory=$true)][string]$Package,
      [Parameter(Mandatory=$true)][string]$Fixture,
      [Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference = 'Stop'
$taskFixture = [IO.Path]::GetFullPath($Fixture)
$taskDiagnostics = [IO.Path]::GetFullPath((Join-Path $Package 'diagnostics'))
if (!$taskFixture.StartsWith($taskDiagnostics + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Native BC tests require a private fixture inside package diagnostics.'
}
$taskRelease = Join-Path $Package 'release'
$taskProbe = Join-Path $taskRelease 'CommanderMediaProbe.exe'
$taskFirmware = Join-Path $taskFixture 'Build\probe\zxevo77.rom'
$taskSnapshot = Join-Path $taskFixture 'Build\probe\BC086-ui.sna'
$taskTape = Join-Path $taskFixture 'Build\probe\tapes\BC052-tapes-SD.ima'
# Fixture contains COPIES of current BC Build binaries, symbols and generator/
# probe sources. Never run generators in the user's BC checkout or SD folder.
$taskPassed = [Collections.Generic.List[string]]::new()
function Invoke-Native([string]$Name, [string]$Image, [string[]]$Options) {
    $taskLog = Join-Path $taskDiagnostics ("bc-$Name.txt")
    & $taskProbe $taskFixture $taskRelease $taskFirmware $taskSnapshot $Image --hardware-timing @Options *> $taskLog
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $taskLog -Tail 12; throw "Native BC failed: $Name" }
    Get-Content -LiteralPath $taskLog -Tail 2
    $taskPassed.Add($Name)
}
foreach ($taskVolume in @(0,20,60)) {
    Invoke-Native "tape-$taskVolume" $taskTape @('--manual-tape-menu','--real-tape','--bc-tape-volume',"$taskVolume")
}
foreach ($taskVolume in @(20,60)) {
    Invoke-Native "tzx-$taskVolume" $taskTape @('--manual-tape-menu','--real-tape','--tape-tzx','--bc-tape-volume',"$taskVolume")
}
foreach ($taskCase in @('tzx','break','error','f12')) {
    $taskOptions = @('--manual-tape-menu','--real-tape','--bc-tape-volume','0')
    $taskOptions += switch ($taskCase) {
        'tzx' {'--tape-tzx'}
        'break' {'--break-real-tape'}
        'error' {'--bad-real-wave'}
        'f12' {'--reset-during-tape'}
    }
    Invoke-Native "tape-$taskCase" $taskTape $taskOptions
}
foreach ($taskFormat in @('tap','tzx')) {
    $taskOptions = @('--manual-tape-menu')
    if ($taskFormat -eq 'tzx') { $taskOptions += '--tape-tzx' }
    Invoke-Native "fast-$taskFormat" $taskTape $taskOptions
}
$taskCases = @('closed','sector','range','cancel','readonly','files','active','full',
    'partition','invalid','geometry','source','sdsc-range','reject','busy','mismatch','sdhc','collision')
foreach ($taskCase in $taskCases) {
    $taskFixtureLog = Join-Path $taskDiagnostics "fixture-$taskCase.txt"
    & $Python -X utf8 "$taskFixture\tools\prepare_sd_write_probe.py" $taskCase *> $taskFixtureLog
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $taskFixtureLog -Tail 10; throw "Fixture failed: $taskCase" }
    $taskOptions = @('--sd-write-case',$taskCase)
    if ($taskCase -eq 'readonly') { $taskOptions += '--write-readonly' }
    if ($taskCase -eq 'sdhc') { $taskOptions += '--write-sdhc' }
    Invoke-Native "write-$taskCase" "$taskFixture\Build\probe\stage063\$taskCase\BC045-plugins-SD.ima" $taskOptions
    $taskAudit = Join-Path $taskDiagnostics "audit-$taskCase.txt"
    & $Python -X utf8 "$taskFixture\tools\verify_sd_write_probe.py" $taskCase *> $taskAudit
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $taskAudit -Tail 10; throw "Independent FAT audit failed: $taskCase" }
    Get-Content -LiteralPath $taskAudit -Tail 1
}
[ordered]@{
    Hardware = (Get-FileHash -LiteralPath "$taskRelease\ZXMAK2.Hardware.dll").Hash
    Circuits = (Get-FileHash -LiteralPath "$taskRelease\ZXMAK2.Hardware.Circuits.dll").Hash
    BcPayload = (Get-FileHash -LiteralPath "$taskFixture\Build\BCSTART.bin").Hash
    TapePlugin = (Get-FileHash -LiteralPath "$taskFixture\Build\BCC\PLUGINS\TAPELOAD.BCP").Hash
    NativeCases = $taskPassed.ToArray()
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$taskDiagnostics\bc-verification.json" -Encoding UTF8
Write-Output "PASS: $($taskPassed.Count) native BC cases (11 Tape/FAST + 18 SD writes); all independent FAT audits."
