param([string]$Package = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007')
$ErrorActionPreference = 'Stop'
$taskRelease = "$Package\release"
$taskCsc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$taskRefs = @(Get-ChildItem -LiteralPath $taskRelease -Filter 'ZXMAK2*.dll' | ForEach-Object { '/r:' + $_.FullName })
$taskProbes = [ordered]@{
    KeyboardRouting = 'tools\BaseConfInputAudioTest\KeyboardRoutingProbe.cs'
    PrintScreenNmiProbe = 'tools\BaseConfInputAudioTest\PrintScreenNmiProbe.cs'
    GoldenRenderer = 'tools\BaseConfVideoTest\GoldenRendererProbe-B40.cs'
    ContentionFloatingBus = 'tools\BaseConfVideoTest\ContentionFloatingBusProbe-B40.cs'
    InterruptNmi = 'tools\BaseConfInterruptTest\InterruptNmiProbe-B36.cs'
    WaitPort = 'tools\BaseConfInterruptTest\WaitPortProbe-B37.cs'
    VideoTiming = 'tools\BaseConfVideoTest\VideoTimingProbe-B30.cs'
    TapeClock = 'tools\BaseConfInputAudioTest\TapeClockProbe-B40.cs'
    SdLifecycleProbe = 'tools\SdSessionTest\LifecycleProbe.cs'
    RefreshFolder = 'tools\SdSessionTest\RefreshProbe.cs'
    PentEvoProfile = 'tools\BaseConfConfigTest\PentEvoProfileProbe.cs'
}
foreach ($taskEntry in $taskProbes.GetEnumerator()) {
    & $taskCsc /nologo /target:exe /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRelease\$($taskEntry.Key).exe" @taskRefs $taskEntry.Value
    if ($LASTEXITCODE -ne 0) { throw "Compile failed: $($taskEntry.Key)" }
    Copy-Item -LiteralPath "$taskRelease\Test.exe.config" -Destination "$taskRelease\$($taskEntry.Key).exe.config"
}
Push-Location $taskRelease
try {
    foreach ($taskEntry in $taskProbes.GetEnumerator()) {
        $taskExe = "$taskRelease\$($taskEntry.Key).exe"
        $taskLog = "$Package\diagnostics\regression-$($taskEntry.Key).txt"
        if ($taskEntry.Key -eq 'RefreshFolder') { & $taskExe "$Package\diagnostics" *> $taskLog }
        elseif ($taskEntry.Key -in @('KeyboardRouting','PrintScreenNmiProbe','TapeClock','SdLifecycleProbe','PentEvoProfile')) { & $taskExe *> $taskLog }
        else { & $taskExe $taskRelease *> $taskLog }
        if ($LASTEXITCODE -ne 0) { Get-Content $taskLog -Tail 15; throw "Regression failed: $($taskEntry.Key)" }
        Get-Content $taskLog -Tail 2
    }
    foreach ($taskArg in @('/hostfoldersd','/moonsound','/omnisound','/multisoundmax','/omni-opl','/master-volume','/evo-display')) {
        $taskLog = "$Package\diagnostics\regression-$($taskArg.Substring(1)).txt"
        & '.\Test.exe' $taskArg *> $taskLog
        if ($LASTEXITCODE -ne 0) { Get-Content $taskLog -Tail 15; throw "Test failed: $taskArg" }
        Get-Content $taskLog -Tail 2
    }
    & '.\Test.exe' *> "$Package\diagnostics\full-benchmark.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Full Test/ULA/benchmark failed' }
    Write-Output 'PASS: full Test/ULA/benchmark.'
} finally { Pop-Location }
