param([string]$Package='L:\Work_two\ZX\ZXMAK2-v92-BaseConf-Prefinal-Test-20261029')
$ErrorActionPreference='Stop'
$taskRuntime=Join-Path $Package 'diagnostics\test-runtime'
$taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Drawing.dll "/out:$taskRuntime\TapeGuestProbe.exe" @taskRefs "$PSScriptRoot\TapeGuestProbe.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Tape guest probe compile failed'}
Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\TapeGuestProbe.exe.config" -Force
$taskPreviousPath=$env:PATH
Push-Location $taskRuntime
try {
    $env:PATH="$taskRuntime\Lib\Native;"+$env:PATH
    & "$taskRuntime\TapeGuestProbe.exe" *> "$Package\diagnostics\tape-guest.txt"
    Get-Content "$Package\diagnostics\tape-guest.txt"
    if($LASTEXITCODE -ne 0){throw 'Tape guest probe failed'}
} finally {Pop-Location;$env:PATH=$taskPreviousPath}
