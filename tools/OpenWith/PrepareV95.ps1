param([string]$Built, [string]$Package,
      [string]$Baseline='L:\Work_two\ZX\ZXMAK2-v94-NMI-Prefix-Test-20261102\release')
$ErrorActionPreference='Stop'
$taskRelease=Join-Path $Package 'release'
if(Test-Path -LiteralPath $taskRelease){throw 'Preserve existing release'}
New-Item -ItemType Directory -Path $taskRelease,(Join-Path $Package 'diagnostics'),(Join-Path $Package 'rollback'),(Join-Path $Package 'checkpoints') -Force | Out-Null
Get-ChildItem -LiteralPath $Baseline -File | Where-Object {$_.Name -notlike 'README-v*.txt'} | Copy-Item -Destination $taskRelease
foreach($taskSub in @('Lib','Config','Resources','Docs','Data')) {
    Copy-Item -LiteralPath (Join-Path $Baseline $taskSub) -Destination (Join-Path $taskRelease $taskSub) -Recurse
}
# Keep every emulation/core/native/resource binary from the verified base.
foreach($taskDll in @('ZXMAK2.Logging.dll','ZXMAK2.Host.WinForms.dll')) {
    Copy-Item -LiteralPath (Join-Path $Built $taskDll) -Destination (Join-Path "$taskRelease\Lib\Managed" $taskDll) -Force
}
Copy-Item -LiteralPath (Join-Path $Built 'ZXMAK2.exe') -Destination "$taskRelease\ZXMAK2.exe" -Force
& 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\CorFlags.exe' "$taskRelease\ZXMAK2.exe" /32BIT+ /nologo
if($LASTEXITCODE -ne 0){throw 'CorFlags failed'}
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\Help\index.html" -Destination "$taskRelease\Docs\Help\index.html" -Force
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\releases\README-v95.txt" -Destination $taskRelease
$taskChanged=@('ZXMAK2.exe','Lib\Managed\ZXMAK2.Logging.dll','Lib\Managed\ZXMAK2.Host.WinForms.dll','Docs\Help\index.html')
$taskComparison=foreach($taskFile in Get-ChildItem -LiteralPath $Baseline -File -Recurse) {
    $taskRelative=$taskFile.FullName.Substring($Baseline.TrimEnd('\').Length+1)
    if($taskRelative -like 'README-v*.txt'){continue}
    $taskOther=Join-Path $taskRelease $taskRelative
    if(!(Test-Path -LiteralPath $taskOther)){throw "Missing base file: $taskRelative"}
    $taskSame=(Get-FileHash -LiteralPath $taskFile.FullName).Hash -eq (Get-FileHash -LiteralPath $taskOther).Hash
    if(!$taskSame -and $taskRelative -notin $taskChanged){throw "Unexpected runtime change: $taskRelative"}
    [pscustomobject]@{Path=$taskRelative;Identical=$taskSame;AllowedChange=($taskRelative -in $taskChanged)}
}
$taskComparison | Export-Csv -LiteralPath "$Package\diagnostics\base-v94-comparison.csv" -NoTypeInformation -Encoding UTF8
Copy-Item -LiteralPath (Join-Path (Split-Path $Baseline -Parent) 'ZXMAK2-v94-NMI-Prefix-Test.zip') -Destination "$Package\rollback"
foreach($taskResearch in @('docs\history\ENGINE_RESEARCH_V81.md','docs\history\ENGINE_RESEARCH_V81_CHECKPOINT.md','tools\Optimization\EngineResearchProbe.cs','tools\Optimization\RunEngineResearch.ps1')) {
    Copy-Item -LiteralPath (Join-Path "$PSScriptRoot\..\.." $taskResearch) -Destination "$Package\checkpoints"
}
Write-Output 'PASS: v95 runtime prepared; all other v94 runtime files unchanged; rollback preserved.'
