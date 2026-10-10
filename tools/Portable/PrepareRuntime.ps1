param(
    [string]$Baseline = 'L:\Work_two\ZX\ZXMAK2-v86-ZXEVO-BC-Tape-SD-Test-20261010\release',
    [string]$Destination = 'L:\Work_two\ZX\ZXMAK2-v87-Portable-Test-20261010\release',
    [string]$Built = 'L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $taskRoot) { throw "Preserve existing runtime: $taskRoot" }
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$taskNative = @('ymfm_opl4.dll','omni_midi.dll','omni_opn.dll','SAASound.dll','Pasmo2.dll')
$taskModified = @('ZXMAK2.exe','Test.exe','ZXMAK2.Logging.dll','ZXMAK2.Engine.dll',
    'ZXMAK2.Hardware.Circuits.dll','ZXMAK2.Hardware.dll','ZXMAK2.Host.Presentation.dll',
    'ZXMAK2.Host.WinForms.dll','ZXMAK2.Hardware.Adlers.dll')
foreach ($taskFile in Get-ChildItem -LiteralPath $Baseline -File) {
    $taskName = $taskFile.Name
    $taskSub = ''
    if ($taskName.EndsWith('.dll')) {
        $taskSub = if ($taskName -in $taskNative) {'Lib\Native'} else {'Lib\Managed'}
    } elseif ($taskName -eq 'GeneralUser-GS.sf2') { $taskSub = 'Resources\SoundBanks' }
    elseif ($taskName -match '\.LICENSE\.txt$') { $taskSub = 'Docs\Licenses' }
    elseif ($taskName -in @('Keyboard.config','machines.config','unity.config','plugins.config','log4net.config','joystick-profiles.xml','debugger_config.xml')) { $taskSub = 'Config' }
    elseif ($taskName -match '^README-v\d+\.txt$') { continue }
    elseif ($taskName -in @('HOTKEYS.md','JOYSTICKS.md','ZXMAK2.chm')) { $taskSub = 'Docs' }
    $taskDirectory = Join-Path $taskRoot $taskSub
    New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
    $taskSource = if ($taskName -in $taskModified) { Join-Path $Built $taskName } else { $taskFile.FullName }
    if ($taskName -eq 'ZXMAK2.exe.config') { $taskSource = Join-Path $PSScriptRoot '..\..\src\ZXMAK2\app.config' }
    if ($taskName -eq 'Test.exe.config') { $taskSource = Join-Path $PSScriptRoot '..\..\src\Test\app.config' }
    if ($taskName -eq 'log4net.config') { $taskSource = Join-Path $PSScriptRoot '..\..\src\ZXMAK2\log4net.config' }
    Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskDirectory $taskName)
}
foreach ($taskSub in @('Resources\ROMs','Docs\Help','Data\State','Data\Sessions','Data\Saves',
    'Data\Recordings','Data\Logs','Data\Backups','Data\Cache','Data\Temp')) {
    New-Item -ItemType Directory -Path (Join-Path $taskRoot $taskSub) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\Help\index.html') -Destination "$taskRoot\Docs\Help\index.html"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\HOTKEYS.md') -Destination "$taskRoot\Docs\HOTKEYS.md"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\releases\README-v87.txt') -Destination "$taskRoot\README-v87.txt"
$taskFlags = 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\CorFlags.exe'
foreach ($taskExe in @('ZXMAK2.exe','Test.exe')) {
    & $taskFlags (Join-Path $taskRoot $taskExe) /32BIT+ /nologo
    if ($LASTEXITCODE -ne 0) { throw "x86 flag failed: $taskExe" }
}
Write-Output "Prepared portable TEST runtime: $taskRoot"
