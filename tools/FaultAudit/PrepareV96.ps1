param([Parameter(Mandatory=$true)][string]$Built, [Parameter(Mandatory=$true)][string]$Package,
      [string]$Baseline='L:\Work_two\ZX\ZXMAK2-v95-Open-With-Test-20261103\release')
$ErrorActionPreference='Stop'
$release=Join-Path $Package 'release'
if(Test-Path -LiteralPath $release){throw 'Preserve existing release'}
New-Item -ItemType Directory -Path $release,"$Package\diagnostics","$Package\rollback","$Package\checkpoints" -Force | Out-Null
Get-ChildItem -LiteralPath $Baseline -File | Where-Object {$_.Name -notlike 'README-v*.txt'} | Copy-Item -Destination $release
foreach($sub in @('Lib','Config','Resources','Docs','Data')) {
    Copy-Item -LiteralPath (Join-Path $Baseline $sub) -Destination (Join-Path $release $sub) -Recurse
}
# Preserve CPU, renderer, sound cores, native DLLs, EXEs and resources byte-for-byte.
$dlls=@('ZXMAK2.Hardware.Circuits.dll','ZXMAK2.Hardware.dll','ZXMAK2.Host.dll','ZXMAK2.Host.WinForms.dll')
foreach($dll in $dlls) {Copy-Item -LiteralPath (Join-Path $Built $dll) -Destination "$release\Lib\Managed\$dll" -Force}
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\Help\index.html" -Destination "$release\Docs\Help\index.html" -Force
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\releases\README-v96.txt" -Destination $release
$allowed=@($dlls | ForEach-Object {"Lib\Managed\$_"})+@('Docs\Help\index.html')
$comparison=foreach($file in Get-ChildItem -LiteralPath $Baseline -File -Recurse) {
    $relative=$file.FullName.Substring($Baseline.TrimEnd('\').Length+1)
    if($relative -like 'README-v*.txt'){continue}
    $other=Join-Path $release $relative
    if(!(Test-Path -LiteralPath $other)){throw "Missing base file: $relative"}
    $same=(Get-FileHash -LiteralPath $file.FullName).Hash -eq (Get-FileHash -LiteralPath $other).Hash
    if(!$same -and $relative -notin $allowed){throw "Unexpected runtime change: $relative"}
    [pscustomobject]@{Path=$relative;Identical=$same;AllowedChange=($relative -in $allowed)}
}
$comparison | Export-Csv -LiteralPath "$Package\diagnostics\base-v95-comparison.csv" -NoTypeInformation -Encoding UTF8
Copy-Item -LiteralPath (Join-Path (Split-Path $Baseline -Parent) 'ZXMAK2-v95-Open-With-Test.zip') -Destination "$Package\rollback"
foreach($research in @('docs\history\ENGINE_RESEARCH_V81.md','docs\history\ENGINE_RESEARCH_V81_CHECKPOINT.md','tools\Optimization\EngineResearchProbe.cs','tools\Optimization\RunEngineResearch.ps1')) {
    Copy-Item -LiteralPath (Join-Path "$PSScriptRoot\..\.." $research) -Destination "$Package\checkpoints"
}
Write-Output 'PASS v96 prepared: only four managed DLLs, help and README changed; v95 rollback preserved.'
