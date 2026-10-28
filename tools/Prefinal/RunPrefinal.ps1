param([string]$Package='L:\Work_two\ZX\ZXMAK2-v92-BaseConf-Prefinal-Test-20261029')
$ErrorActionPreference='Stop'
$taskRuntime=Join-Path $Package 'diagnostics\test-runtime'
$taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /main:PrefinalProbe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRuntime\PrefinalProbe.exe" @taskRefs "$PSScriptRoot\PrefinalProbe.cs" "$PSScriptRoot\..\SoundCards\SoundCardsProbe.cs" "$PSScriptRoot\..\SoundCards\MusicFixtures.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\PrefinalProbe.exe.config" -Force
$taskPreviousPath=$env:PATH
Push-Location $taskRuntime
try {
    $env:PATH="$taskRuntime\Lib\Native;"+$env:PATH
    & "$taskRuntime\PrefinalProbe.exe" *> "$Package\diagnostics\prefinal.txt"
    Get-Content "$Package\diagnostics\prefinal.txt"
    if($LASTEXITCODE -ne 0){throw 'Prefinal probe failed'}
} finally {Pop-Location;$env:PATH=$taskPreviousPath}
