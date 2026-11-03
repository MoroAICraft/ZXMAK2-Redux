param([string]$Root = 'D:\ZXMAK2_Work\audio_v98_20261103\references')
$ErrorActionPreference = 'Stop'
$exe = Join-Path $Root 'zxtune\zxtune123.exe'
$config = Join-Path $PSScriptRoot 'ReferenceZxtuneAy.conf'
$track = Join-Path $Root 'waiting.pt3'
$wav = Join-Path $Root 'waiting-zxtune-ay-hq-controlled.wav'
if (Test-Path -LiteralPath $wav) { throw "Refusing to overwrite $wav" }
& $exe --config $config --version | Tee-Object -FilePath (Join-Path $Root 'zxtune-version.txt')
& $exe --config $config --list-options | Set-Content -LiteralPath (Join-Path $Root 'zxtune-options.txt') -Encoding UTF8
& $exe --config $config --list-plugins | Set-Content -LiteralPath (Join-Path $Root 'zxtune-plugins.txt') -Encoding UTF8
& $exe --config $config --wav "filename=$wav" --quiet $track 2>&1 | Tee-Object -FilePath (Join-Path $Root 'zxtune-export.txt')
if ($LASTEXITCODE -ne 0) { throw 'ZXTune WAV export failed' }
$registers = Join-Path $Root 'waiting-zxtune.aydump'
if (!(Test-Path -LiteralPath $registers)) {
    & $exe --config $config --convert "mode=aydump,filename=$registers" $track
    if ($LASTEXITCODE -ne 0) { throw 'ZXTune register export failed' }
}
$metrics = Join-Path $Root 'AudioFileMetrics.exe'
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /target:exe "/out:$metrics" (Join-Path $PSScriptRoot '..\Optimization\AudioFileMetrics.cs')
if ($LASTEXITCODE -ne 0) { throw 'Metrics compilation failed' }
& $metrics $wav | Tee-Object -FilePath (Join-Path $Root 'zxtune-metrics.txt')
Get-FileHash -LiteralPath $exe,$config,$track,$wav,$registers -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Root 'export-sha256.json') -Encoding UTF8
