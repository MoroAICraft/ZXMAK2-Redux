param(
    [Parameter(Mandatory=$true)][string]$Package,
    [string]$MusicFolder='L:\Work_two\ZX\ZX_IMG\flash_pentevo\test_tfm',
    [switch]$SkipMusic
)
$ErrorActionPreference='Stop'
# ONLY a private diagnostics runtime: never run the probes in published release.
$taskRuntime=Join-Path $Package 'diagnostics\test-runtime'
if (!(Test-Path -LiteralPath "$taskRuntime\Lib\Managed")) {throw 'Prepare the private test-runtime copy first'}
$taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRuntime\SoundCardsProbe.exe" @taskRefs "$PSScriptRoot\SoundCardsProbe.cs" "$PSScriptRoot\MusicFixtures.cs"
if($LASTEXITCODE -ne 0){throw 'SoundCards compile failed'}
Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\SoundCardsProbe.exe.config" -Force
$taskPreviousPath=$env:PATH
$taskAffinity=$env:ZXMAK2_BENCH_AFFINITY
$taskReverse=$env:ZXMAK2_SOUND_REVERSE
try {
    $env:PATH="$taskRuntime\Lib\Native;"+$env:PATH
    $env:ZXMAK2_BENCH_AFFINITY='1'
    $env:ZXMAK2_SOUND_REVERSE='0'
    & "$taskRuntime\SoundCardsProbe.exe" *> "$Package\diagnostics\soundcards.txt"
    if($LASTEXITCODE -ne 0){throw 'SoundCards functional/clock/benchmark failed'}
    & "$taskRuntime\SoundCardsProbe.exe" paths *> "$Package\diagnostics\soundcard-paths.txt"
    if($LASTEXITCODE -ne 0){throw 'SoundCards portable IDE/CMOS failed'}
    $env:ZXMAK2_SOUND_REVERSE='1'
    & "$taskRuntime\SoundCardsProbe.exe" bench *> "$Package\diagnostics\soundcards-reverse-bench.txt"
    if($LASTEXITCODE -ne 0){throw 'Reverse-order benchmark failed'}
    if(!$SkipMusic){
        & "$taskRuntime\SoundCardsProbe.exe" music $MusicFolder *> "$Package\diagnostics\music-fixtures.txt"
        if($LASTEXITCODE -ne 0){throw 'Music fixture replay failed'}
    }
    Write-Output 'PASS: soundcards, clocks, chunk boundaries, IDE/CMOS and paired bounded performance. Music replay follows SkipMusic option.'
} finally {$env:PATH=$taskPreviousPath;$env:ZXMAK2_BENCH_AFFINITY=$taskAffinity;$env:ZXMAK2_SOUND_REVERSE=$taskReverse}
