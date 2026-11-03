param([string]$Workspace='D:\ZXMAK2_Work\audio_v98_20261103',
 [string]$Package='L:\Work_two\ZX\ZXMAK2-v98-Audio-Output-Test-20261103')
$ErrorActionPreference='Stop'
$dest=Join-Path $Package 'diagnostics'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
function Copy-Files($from,$to,$filter) {
 if(!(Test-Path -LiteralPath $from)){return}
 New-Item -ItemType Directory -Path $to -Force | Out-Null
 Get-ChildItem -LiteralPath $from -File | Where-Object {& $filter $_} | Copy-Item -Destination $to -Force
}
$textFiles={param($f) $f.Extension -in '.txt','.csv','.tsv','.json','.md','.conf'}
$guestFiles={param($f) $f.Extension -in '.txt','.csv','.png','.wav','.vmz'}
# Deliberately NOT recursive across runtimes, guest SDs, original configs/banks.
foreach($variant in 'baseline','candidate') {
 foreach($dir in Get-ChildItem "$Workspace\$variant\diagnostics" -Directory) {
  if(Test-Path -LiteralPath "$($dir.FullName)\guest.txt") {
   Copy-Files $dir.FullName "$dest\guest-$variant\$($dir.Name)" $guestFiles
  }
 }
}
Copy-Files "$Workspace\candidate-final-regression\diagnostics" "$dest\regression-final" $textFiles
Copy-Files "$Workspace\mixer_audit" "$dest\mixer" $textFiles
Copy-Files "$Workspace\card_levels" "$dest\card-levels" $textFiles
Copy-Files "$Workspace\candidate\diagnostics\mixer-bench" "$dest\performance" $textFiles
Copy-Files "$Workspace\candidate\diagnostics" "$dest\integration" $textFiles
Copy-Files "$Workspace\references" "$dest\references" $textFiles
foreach($dir in Get-ChildItem "$Workspace\references" -Directory) {
 if($dir.Name -eq 'comparison-clips' -or $dir.Name -like 'candidate-comparison*') {
  Copy-Item -LiteralPath $dir.FullName -Destination "$dest\references" -Recurse -Force
 }
}
Copy-Files "$Workspace\references" "$dest\references" {param($f) $f.Name -in 'waiting-zxtune-ay-hq-controlled.wav','waiting-zxtune.aydump'}
foreach($name in 'v97-binary-identity.csv','guest-inputs.csv','guest-seed.csv','runtime-v97-v98.csv','originals-final-check.txt') {
 if(Test-Path -LiteralPath "$Workspace\source-state\$name") {Copy-Item -LiteralPath "$Workspace\source-state\$name" -Destination $dest -Force}
}
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\history\AUDIO_V98_CARD_LEVELS.md" -Destination "$dest\card-levels" -Force
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\releases\AUDIO_V98_REPORT.txt" -Destination $dest -Force
Copy-Files $Workspace "$Package\checkpoints" {param($f) $f.Name -like 'build*.log'}
# Preserve prior unrelated research without adding it to the release git tree.
foreach($path in 'docs/history/ENGINE_RESEARCH_V81.md','docs/history/ENGINE_RESEARCH_V81_CHECKPOINT.md','tools/Optimization/EngineResearchProbe.cs','tools/Optimization/RunEngineResearch.ps1') {
 $inputPath=Join-Path "$PSScriptRoot\..\.." $path
 if(Test-Path -LiteralPath $inputPath) {
  $outputPath=Join-Path "$Package\checkpoints\preexisting-research" $path
  New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
  Copy-Item -LiteralPath $inputPath -Destination $outputPath -Force
 }
}
Write-Output 'Diagnostic artifacts copied without runtime probes, personal SD contents or original active configs.'
