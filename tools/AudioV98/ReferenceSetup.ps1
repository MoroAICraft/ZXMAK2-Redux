param([string]$Root = 'D:\ZXMAK2_Work\audio_v98_20261103\references')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Root -Force | Out-Null
$sources = @{
    'ay-emul' = 'L:\Work_two\ZX\Ay_Emul30-64'
    'zxtune' = 'L:\Work_two\ZX\ZX-Tune'
}
$manifest = @()
foreach ($name in $sources.Keys) {
    $source = $sources[$name]
    $target = Join-Path $Root $name
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $target -Recurse -Force
    }
    foreach ($item in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = $item.FullName.Substring($source.Length + 1)
        $copy = Join-Path $target $relative
        $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) {
            throw "Copy mismatch: $copy"
        }
        $manifest += [pscustomobject]@{ Player=$name; RelativePath=$relative; Length=$item.Length; SHA256=$hash }
    }
}
$track = 'L:\Work_two\ZX\ZX_IMG\flash_pentevo\TESTING_PLAYER\PT3\Lamer Pinky - Waiting 4 Robot (2023) (FOReVER 2023 - ROBOT, 3).pt3'
Copy-Item -LiteralPath $track -Destination (Join-Path $Root 'waiting.pt3')
$manifest += [pscustomobject]@{Player='input';RelativePath='waiting.pt3';Length=(Get-Item -LiteralPath $track).Length;SHA256=(Get-FileHash -LiteralPath $track -Algorithm SHA256).Hash}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Root 'originals-sha256.json') -Encoding UTF8
$manifest | Select-Object Player,RelativePath,Length,SHA256
