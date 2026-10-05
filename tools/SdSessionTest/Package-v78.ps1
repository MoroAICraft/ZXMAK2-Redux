param(
    [string]$PackageDirectory = 'L:\Work_two\ZX\ZXMAK2-v78-ZXEVO-BC-Portable-SD-Folder-Test-20261005'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRelease = Join-Path $PackageDirectory 'release'
$taskFiles = @(Get-ChildItem -LiteralPath $taskRelease -File)
$taskExcluded = @('ZXMAK2.vmz', 'ZXMAK2.vmide', 'ZXMAK2.cmos', 'YRW801-M - Yamaha - 1993.rom')
$taskArchiveName = 'ZXMAK2-v78-ZXEVO-BC-Portable-SD-Folder-Test'
foreach ($taskClean in @($false, $true)) {
    $taskSuffix = if ($taskClean) { '-Clean' } else { '' }
    $taskZipPath = Join-Path $PackageDirectory ($taskArchiveName + $taskSuffix + '.zip')
    if (Test-Path -LiteralPath $taskZipPath) { throw "Archive already exists: $taskZipPath" }
    $taskSelected = @($taskFiles | Where-Object { !$taskClean -or $_.Name -notin $taskExcluded })
    $taskZipStream = [System.IO.File]::Open($taskZipPath, [System.IO.FileMode]::CreateNew)
    $taskZip = [System.IO.Compression.ZipArchive]::new($taskZipStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($taskFile in $taskSelected) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $taskZip, $taskFile.FullName, ('release/' + $taskFile.Name),
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $taskZip.Dispose(); $taskZipStream.Dispose() }
    $taskVerify = [System.IO.Compression.ZipFile]::OpenRead($taskZipPath)
    try {
        if ($taskVerify.Entries.Count -ne $taskSelected.Count) { throw 'Entry count mismatch' }
        foreach ($taskFile in $taskSelected) {
            $taskEntry = $taskVerify.GetEntry('release/' + $taskFile.Name)
            if (!$taskEntry -or $taskEntry.Length -ne $taskFile.Length) { throw ('Missing/invalid: ' + $taskFile.Name) }
            $taskHash = [System.Security.Cryptography.SHA256]::Create()
            $taskEntryStream = $taskEntry.Open()
            try { $taskActual = [BitConverter]::ToString($taskHash.ComputeHash($taskEntryStream)).Replace('-', '') }
            finally { $taskEntryStream.Dispose(); $taskHash.Dispose() }
            if ($taskActual -ne (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash) {
                throw ('Hash mismatch: ' + $taskFile.Name)
            }
        }
        foreach ($taskRequired in @('Test.exe', 'Test.exe.config', 'ROMS.PAK', 'GeneralUser-GS.sf2',
            'ymfm_opl4.dll', 'omni_opn.dll', 'omni_midi.dll', 'README-v78.txt', 'HOTKEYS.md')) {
            if (!$taskVerify.GetEntry('release/' + $taskRequired)) { throw ('Required file omitted: ' + $taskRequired) }
        }
        if ($taskClean) {
            foreach ($taskName in $taskExcluded) {
                if ($taskVerify.GetEntry('release/' + $taskName)) { throw ('Not clean: ' + $taskName) }
            }
        }
    } finally { $taskVerify.Dispose() }
    Write-Output ("PASS: {0}; {1} files; SHA256 {2}" -f $taskZipPath, $taskSelected.Count,
        (Get-FileHash -LiteralPath $taskZipPath -Algorithm SHA256).Hash)
}
