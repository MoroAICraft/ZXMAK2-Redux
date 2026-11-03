param([string]$Root = 'D:\ZXMAK2_Work\audio_v98_20261103')
$ErrorActionPreference = 'Stop'
$refs = Join-Path $Root 'references'
$guest = Join-Path $Root 'baseline\diagnostics\ay-pt3'
$csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$oldProbe = Join-Path $refs 'AudioReferenceCompare.exe'
& $csc /nologo "/out:$oldProbe" (Join-Path $PSScriptRoot '..\Optimization\AudioReferenceCompare.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compare compilation failed' }
$raw = Join-Path $guest 'music-3-AYCHRV.wav'
$mixed = Join-Path $guest 'music-mixed.wav'
$reference = Join-Path $refs 'waiting-zxtune-ay-hq-controlled.wav'
$comparison = & $oldProbe $raw $mixed $reference
if ($LASTEXITCODE -ne 0) { throw 'Reference comparison failed' }
$comparison | Tee-Object -FilePath (Join-Path $refs 'guest-comparison-v97.txt')
$match = [regex]::Match(($comparison -join "`n"), 'bestOffset=([0-9.,]+)s')
if (!$match.Success) { throw 'Alignment result missing' }
$offset = $match.Groups[1].Value.Replace(',', '.')
$clipsExe = Join-Path $refs 'ReferenceCompareClips.exe'
& $csc /nologo "/out:$clipsExe" (Join-Path $PSScriptRoot 'ReferenceCompareClips.cs')
if ($LASTEXITCODE -ne 0) { throw 'Clips compilation failed' }
$clips = Join-Path $refs 'comparison-clips'
& $clipsExe $mixed $reference $clips $offset | Tee-Object -FilePath (Join-Path $refs 'comparison-clips.txt')
if ($LASTEXITCODE -ne 0) { throw 'Clips generation failed (outputs intentionally are not overwritten)' }
$files = @($raw, $mixed, $reference) + @(Get-ChildItem -LiteralPath $clips -File | ForEach-Object FullName)
Get-FileHash -LiteralPath $files -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $refs 'comparison-sha256.json') -Encoding UTF8
& (Join-Path $refs 'AudioFileMetrics.exe') (Join-Path $clips 'A-ZXMAK2-v97-PT3-matched.wav') (Join-Path $clips 'B-ZXTune-r5100-PT3-matched.wav') | Tee-Object -FilePath (Join-Path $refs 'comparison-clips-metrics.txt')
if ($LASTEXITCODE -ne 0) { throw 'Clips metrics failed' }
