param([string]$PackageDirectory = 'L:\Work_two\ZX\ZXMAK2-v80-ZXEVO-BC-Optimization-Test-20261007')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRelease = Join-Path $PackageDirectory 'release'
$taskRuntimeNames = @(Get-ChildItem -LiteralPath "$PackageDirectory\baseline-v79" -File |
    Where-Object {$_.Name -notlike 'OptimizationProbe*' -and $_.Name -ne 'README-v79.txt'} |
    Select-Object -ExpandProperty Name) + @('README-v80.txt')
$taskFiles = @($taskRuntimeNames | ForEach-Object {Get-Item -LiteralPath (Join-Path $taskRelease $_)})
$taskExcluded = @('ZXMAK2.vmz','ZXMAK2.vmide','ZXMAK2.cmos','YRW801-M - Yamaha - 1993.rom')
foreach ($taskClean in @($false,$true)) {
    $taskSuffix = if ($taskClean) {'-Clean'} else {''}
    $taskZipPath = Join-Path $PackageDirectory ("ZXMAK2-v80-ZXEVO-BC-Optimization-Test$taskSuffix.zip")
    if (Test-Path -LiteralPath $taskZipPath) {throw "Archive already exists: $taskZipPath"}
    $taskSelected = @($taskFiles | Where-Object {!$taskClean -or $_.Name -notin $taskExcluded})
    $taskZipStream = [IO.File]::Open($taskZipPath,[IO.FileMode]::CreateNew)
    $taskZip = [IO.Compression.ZipArchive]::new($taskZipStream,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($taskFile in $taskSelected) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskFile.FullName,
                ('release/'+$taskFile.Name),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally {$taskZip.Dispose(); $taskZipStream.Dispose()}
    $taskVerify = [IO.Compression.ZipFile]::OpenRead($taskZipPath)
    try {
        if ($taskVerify.Entries.Count -ne $taskSelected.Count) {throw 'Entry count mismatch'}
        foreach ($taskFile in $taskSelected) {
            $taskEntry = $taskVerify.GetEntry('release/'+$taskFile.Name)
            if (!$taskEntry -or $taskEntry.Length -ne $taskFile.Length) {throw "Missing/invalid: $($taskFile.Name)"}
            $taskHash = [Security.Cryptography.SHA256]::Create(); $taskInput = $taskEntry.Open()
            try {$taskActual = [BitConverter]::ToString($taskHash.ComputeHash($taskInput)).Replace('-','')}
            finally {$taskInput.Dispose(); $taskHash.Dispose()}
            if ($taskActual -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash) {throw 'ZIP hash mismatch'}
        }
        foreach ($taskRequired in @('Test.exe','Test.exe.config','ROMS.PAK','GeneralUser-GS.sf2',
            'ymfm_opl4.dll','omni_opn.dll','omni_midi.dll','README-v80.txt','HOTKEYS.md')) {
            if (!$taskVerify.GetEntry('release/'+$taskRequired)) {throw "Required file omitted: $taskRequired"}
        }
        if ($taskClean) {
            foreach ($taskName in $taskExcluded) {
                if ($taskVerify.GetEntry('release/'+$taskName)) {throw "Not clean: $taskName"}
            }
        }
    } finally {$taskVerify.Dispose()}
    Write-Output "PASS: $taskZipPath; $($taskSelected.Count) files"
}
Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.zip' | Get-FileHash |
    Export-Csv -LiteralPath "$PackageDirectory\diagnostics\package-sha256.csv" -NoTypeInformation -Encoding UTF8
