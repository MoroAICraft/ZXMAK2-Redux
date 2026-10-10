param(
    [string]$Baseline = 'L:\Work_two\ZX\ZXMAK2-v86-ZXEVO-BC-Tape-SD-Test-20261010\release',
    [string]$Destination = 'L:\Work_two\ZX\ZXMAK2-v87-Portable-Test-20261010\release',
    [string]$Built = 'L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease',
    [string]$Version = 'v87'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $taskRoot) { throw "Preserve existing runtime: $taskRoot" }
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$taskNative = @('ymfm_opl4.dll','omni_midi.dll','omni_opn.dll','SAASound.dll','Pasmo2.dll')
$taskModified = @('ZXMAK2.exe','Test.exe','ZXMAK2.Logging.dll','ZXMAK2.Engine.dll',
    'ZXMAK2.Hardware.Circuits.dll','ZXMAK2.Hardware.dll','ZXMAK2.Host.Presentation.dll',
    'ZXMAK2.Host.WinForms.dll','ZXMAK2.Hardware.Adlers.dll')
if (Test-Path -LiteralPath (Join-Path $Baseline 'Lib\Managed')) {
    # Subsequent portable releases keep the structured layout. Never copy
    # previous sessions, logs, saves or diagnostic EXEs into a new runtime.
    foreach ($taskFile in Get-ChildItem -LiteralPath $Baseline -File) {
        if ($taskFile.Name -match '^README-v\d+\.txt$') { continue }
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskRoot
    }
    foreach ($taskSub in @('Lib','Config','Resources','Docs')) {
        Copy-Item -LiteralPath (Join-Path $Baseline $taskSub) -Destination (Join-Path $taskRoot $taskSub) -Recurse
    }
    foreach ($taskName in $taskModified) {
        $taskDirectory = if ($taskName.EndsWith('.dll')) { Join-Path $taskRoot 'Lib\Managed' } else { $taskRoot }
        Copy-Item -LiteralPath (Join-Path $Built $taskName) -Destination (Join-Path $taskDirectory $taskName) -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\src\ZXMAK2\app.config') -Destination "$taskRoot\ZXMAK2.exe.config" -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\src\Test\app.config') -Destination "$taskRoot\Test.exe.config" -Force
} else {
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
}
foreach ($taskSub in @('Resources\ROMs','Docs\Help','Data\State','Data\Sessions','Data\Saves',
    'Data\Recordings','Data\Logs','Data\Backups','Data\Cache','Data\Temp')) {
    New-Item -ItemType Directory -Path (Join-Path $taskRoot $taskSub) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\Help\index.html') -Destination "$taskRoot\Docs\Help\index.html"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\HOTKEYS.md') -Destination "$taskRoot\Docs\HOTKEYS.md"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "..\..\docs\releases\README-$Version.txt") -Destination "$taskRoot\README-$Version.txt"
$taskLegacyHelp=Join-Path $PSScriptRoot '..\..\src\Help'
New-Item -ItemType Directory -Path "$taskRoot\Docs\Help\Legacy" -Force | Out-Null
Get-ChildItem -LiteralPath $taskLegacyHelp -File |
    Where-Object { $_.Extension -in @('.htm','.html','.css') } |
    Copy-Item -Destination "$taskRoot\Docs\Help\Legacy"
foreach($taskSub in @('Pics','Assembler code')) {
    if (!(Test-Path -LiteralPath "$taskRoot\Docs\Help\Legacy\$taskSub")) {
        Copy-Item -LiteralPath (Join-Path $taskLegacyHelp $taskSub) -Destination "$taskRoot\Docs\Help\Legacy\$taskSub" -Recurse
    }
}
$taskFlags = 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\CorFlags.exe'
foreach ($taskExe in @('ZXMAK2.exe','Test.exe')) {
    & $taskFlags (Join-Path $taskRoot $taskExe) /32BIT+ /nologo
    if ($LASTEXITCODE -ne 0) { throw "x86 flag failed: $taskExe" }
}
Write-Output "Prepared portable TEST runtime: $taskRoot"
