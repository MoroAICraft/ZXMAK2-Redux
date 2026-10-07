param([string]$Package = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007')
$ErrorActionPreference = 'Stop'
if (@(& git status --porcelain).Count) {throw 'Commit final sources before exporting'}
$taskSource = Join-Path $Package 'source'
New-Item -ItemType Directory -Path $taskSource -Force | Out-Null
$taskZip = Join-Path $taskSource 'ZXMAK2-v80-source-with-OmniSound.zip'
$taskSubZip = Join-Path $taskSource 'ZX-OmniSound-0794d8d-source.zip'
foreach ($taskTarget in @($taskZip,$taskSubZip)) {
    if (Test-Path -LiteralPath $taskTarget) {throw "Preserve existing archive: $taskTarget"}
}
& git archive --format=zip "--output=$taskZip" HEAD
if ($LASTEXITCODE -ne 0) {throw 'Main source archive failed'}
$taskPinned = (& git rev-parse HEAD:src/ZX-OmniSound).Trim()
if ((& git -C src/ZX-OmniSound rev-parse HEAD).Trim() -ne $taskPinned) {throw 'Submodule does not match gitlink'}
& git -C src/ZX-OmniSound archive --format=zip "--output=$taskSubZip" $taskPinned
if ($LASTEXITCODE -ne 0) {throw 'Submodule source archive failed'}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskMain = [IO.Compression.ZipFile]::Open($taskZip,[IO.Compression.ZipArchiveMode]::Update)
$taskSub = [IO.Compression.ZipFile]::OpenRead($taskSubZip)
try {
    foreach ($taskEntry in $taskSub.Entries) {
        $taskName = 'src/ZX-OmniSound/'+$taskEntry.FullName
        if ($taskMain.GetEntry($taskName)) {throw "Duplicate source entry: $taskName"}
        $taskNew = $taskMain.CreateEntry($taskName,[IO.Compression.CompressionLevel]::Optimal)
        $taskInput = $taskEntry.Open(); $taskOutput = $taskNew.Open()
        try {$taskInput.CopyTo($taskOutput)} finally {$taskOutput.Dispose(); $taskInput.Dispose()}
    }
} finally {$taskSub.Dispose(); $taskMain.Dispose()}
$taskVerify = [IO.Compression.ZipFile]::OpenRead($taskZip)
$taskSub = [IO.Compression.ZipFile]::OpenRead($taskSubZip)
try {
    foreach ($taskEntry in $taskSub.Entries) {
        $taskActual = $taskVerify.GetEntry('src/ZX-OmniSound/'+$taskEntry.FullName)
        if (!$taskActual -or $taskActual.Length -ne $taskEntry.Length) {throw 'Submodule archive mismatch'}
        $taskHash = [Security.Cryptography.SHA256]::Create()
        $taskInput = $taskEntry.Open(); $taskOutput = $taskActual.Open()
        try {
            if ([BitConverter]::ToString($taskHash.ComputeHash($taskInput)) -ne
                [BitConverter]::ToString($taskHash.ComputeHash($taskOutput))) {throw 'Submodule archive hash mismatch'}
        } finally {$taskInput.Dispose(); $taskOutput.Dispose(); $taskHash.Dispose()}
    }
} finally {$taskSub.Dispose(); $taskVerify.Dispose()}
$taskBundle = Join-Path $taskSource 'ZXMAK2-v80-local-history.bundle'
if (Test-Path -LiteralPath $taskBundle) {throw 'History bundle exists'}
& git bundle create $taskBundle --all
if ($LASTEXITCODE -ne 0) {throw 'History bundle create failed'}
& git bundle verify $taskBundle
if ($LASTEXITCODE -ne 0) {throw 'History bundle verify failed'}
$taskSubBundle = Join-Path $taskSource 'ZX-OmniSound-local-history.bundle'
if (Test-Path -LiteralPath $taskSubBundle) {throw 'Submodule bundle exists'}
& git -C src/ZX-OmniSound bundle create $taskSubBundle --all
if ($LASTEXITCODE -ne 0) {throw 'Submodule bundle create failed'}
& git -C src/ZX-OmniSound bundle verify $taskSubBundle
if ($LASTEXITCODE -ne 0) {throw 'Submodule bundle verify failed'}
Get-ChildItem -LiteralPath $taskSource -File | Get-FileHash |
    Export-Csv -LiteralPath "$Package\diagnostics\source-sha256.csv" -NoTypeInformation -Encoding UTF8
Write-Output 'PASS: combined source ZIP (including pinned OmniSound) and both verified local history bundles'
