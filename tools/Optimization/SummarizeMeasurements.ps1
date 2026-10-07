param([string]$Package = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007')
$ErrorActionPreference = 'Stop'
function Median($values) {
    $sorted = @($values | Sort-Object)
    if (!$sorted.Count) { throw 'No measurements' }
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
$samples = @()
foreach ($variant in @('baseline-v79','release')) {
    foreach ($round in 0,1) {
        foreach ($kind in @('bench','evo-bench')) {
            $path = "$Package\diagnostics\paired-$round-$variant-$kind.txt"
            foreach ($line in Get-Content -LiteralPath $path) {
                if ($kind -eq 'bench' -and $line -match '^ROUND \d+ (.+):\s+(\d+) \[ms\]') {
                    $samples += [pscustomobject]@{Variant=$variant; Test=$Matches[1]; Milliseconds=[double]$Matches[2]}
                } elseif ($kind -eq 'evo-bench' -and $line -match '^EVO \d+ rate=(\d+) (\d+) ms') {
                    $samples += [pscustomobject]@{Variant=$variant; Test=('Evo-rate-'+$Matches[1]); Milliseconds=[double]$Matches[2]}
                }
            }
        }
    }
}
$report = foreach ($test in @($samples.Test | Select-Object -Unique)) {
    $before = @($samples | Where-Object {$_.Test -eq $test -and $_.Variant -eq 'baseline-v79'} | Select-Object -ExpandProperty Milliseconds)
    $after = @($samples | Where-Object {$_.Test -eq $test -and $_.Variant -eq 'release'} | Select-Object -ExpandProperty Milliseconds)
    $b = Median $before; $a = Median $after
    [pscustomobject]@{Test=$test; BaselineMedianMs=$b; OptimizedMedianMs=$a;
        TimeReductionPercent=[Math]::Round(100*(1-$a/$b),2);
        BeforeSamples=$before.Count; AfterSamples=$after.Count;
        BeforeMin=($before|Measure-Object -Minimum).Minimum; BeforeMax=($before|Measure-Object -Maximum).Maximum;
        AfterMin=($after|Measure-Object -Minimum).Minimum; AfterMax=($after|Measure-Object -Maximum).Maximum}
}
$report | Export-Csv -LiteralPath "$Package\diagnostics\performance-summary.csv" -NoTypeInformation -Encoding UTF8
$report | Format-Table Test,BaselineMedianMs,OptimizedMedianMs,TimeReductionPercent -AutoSize
