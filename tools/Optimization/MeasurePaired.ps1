param([string]$Package = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007',
    [string]$ProbeName = 'OptimizationProbe.exe', [string]$BaselineName = 'baseline-v79')
$ErrorActionPreference = 'Stop'
# Reverse order on repeat, no profiling or concurrent probes during timings.
foreach ($taskRound in 0,1) {
    $taskOrder = if ($taskRound -eq 0) { @('release',$BaselineName) } else { @($BaselineName,'release') }
    foreach ($taskVariant in $taskOrder) {
        foreach ($taskMode in @('bench','evo-bench')) {
            $taskDir = Join-Path $Package $taskVariant
            $taskLog = Join-Path "$Package\diagnostics" ("paired-$taskRound-$taskVariant-$taskMode.txt")
            Push-Location $taskDir
            try {
                if ($taskMode -eq 'bench') { & (Join-Path $taskDir $ProbeName) bench *> $taskLog }
                else { & (Join-Path $taskDir $ProbeName) evo-bench '.\ZXMAK2.vmz' *> $taskLog }
                if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $taskVariant/$taskMode" }
            } finally { Pop-Location }
            Write-Output "Measured $taskRound / $taskVariant / $taskMode"
        }
    }
}
