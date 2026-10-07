param([string]$Package = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007')
$ErrorActionPreference = 'Stop'
# Apply affinity only to bounded child probes. No emulator/OS policy mutation.
$taskPreviousAffinity = $env:ZXMAK2_BENCH_AFFINITY
$taskCsc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$taskOldLogs = @(Get-ChildItem -LiteralPath "$Package\diagnostics" -Filter 'paired-*.txt')
if ($taskOldLogs.Count) {
    $taskArchive = "$Package\diagnostics\previous-paired-$(Get-Date -Format yyyyMMdd-HHmmss)"
    New-Item -ItemType Directory -Path $taskArchive | Out-Null
    $taskOldLogs | Move-Item -Destination $taskArchive
}
foreach ($taskVariant in @('baseline-v79','release')) {
    $taskDir = Join-Path $Package $taskVariant
    $taskRefs = @(Get-ChildItem -LiteralPath $taskDir -Filter 'ZXMAK2*.dll' |
        ForEach-Object {'/r:'+ $_.FullName})
    & $taskCsc /nologo /platform:x86 /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$taskDir\OptimizationProbePinned.exe" @taskRefs tools\Optimization\OptimizationProbe.cs
    if ($LASTEXITCODE -ne 0) {throw 'Bounded probe compile failed'}
    Copy-Item -LiteralPath "$taskDir\Test.exe.config" -Destination "$taskDir\OptimizationProbePinned.exe.config"
}
try {
    $env:ZXMAK2_BENCH_AFFINITY = '1'
    & "$PSScriptRoot\MeasurePaired.ps1" -Package $Package -ProbeName 'OptimizationProbePinned.exe'
    & "$PSScriptRoot\SummarizeMeasurements.ps1" -Package $Package
} finally { $env:ZXMAK2_BENCH_AFFINITY = $taskPreviousAffinity }
