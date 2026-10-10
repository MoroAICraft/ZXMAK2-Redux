param([string]$Package='L:\Work_two\ZX\ZXMAK2-v87-Portable-Test-20261010',
    [string]$ArchiveName='ZXMAK2-v87-Portable-Test')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRuntime=[IO.Path]::GetFullPath((Join-Path $Package 'release'))
$taskFiles=@(Get-ChildItem -LiteralPath $taskRuntime -File -Recurse)
$taskRootState=@('ZXMAK2.vmz','ZXMAK2.vmide','ZXMAK2.cmos','ZXMAK2.nvram')
if($taskFiles | Where-Object { $_.Name -match 'Probe|KeyboardRouting|GoldenRenderer|WaitPort|RefreshFolder|SdWriteProtocol' }) {throw 'Diagnostic program leaked into runtime'}
if(Get-ChildItem "$taskRuntime\Data" -File -Recurse) {throw 'Test data leaked into runtime'}
$taskManifest=foreach($taskFile in $taskFiles) {
    [pscustomobject]@{Path=$taskFile.FullName.Substring($taskRuntime.Length+1);Length=$taskFile.Length;SHA256=(Get-FileHash -LiteralPath $taskFile.FullName).Hash}
}
$taskManifest | Export-Csv -LiteralPath "$Package\diagnostics\runtime-sha256.csv" -NoTypeInformation -Encoding UTF8
foreach($taskClean in @($false,$true)) {
    $taskSuffix=if($taskClean){'-Clean'}else{''}
    $taskZipPath=Join-Path $Package "$ArchiveName$taskSuffix.zip"
    if(Test-Path -LiteralPath $taskZipPath){throw "Preserve previous archive: $taskZipPath"}
    $taskSelected=@($taskFiles | Where-Object {
        !$taskClean -or $_.FullName.Substring($taskRuntime.Length+1) -notin $taskRootState
    })
    $taskOutput=[IO.File]::Open($taskZipPath,[IO.FileMode]::CreateNew)
    $taskZip=[IO.Compression.ZipArchive]::new($taskOutput,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach($taskFile in $taskSelected) {
            $taskRelative=$taskFile.FullName.Substring($taskRuntime.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskFile.FullName,'release/'+$taskRelative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        foreach($taskDirectory in Get-ChildItem -LiteralPath $taskRuntime -Directory -Recurse) {
            $taskZip.CreateEntry('release/'+$taskDirectory.FullName.Substring($taskRuntime.Length+1).Replace('\','/')+'/') | Out-Null
        }
    } finally {$taskZip.Dispose();$taskOutput.Dispose()}
    $taskVerify=[IO.Compression.ZipFile]::OpenRead($taskZipPath)
    try {
        foreach($taskFile in $taskSelected) {
            $taskRelative=$taskFile.FullName.Substring($taskRuntime.Length+1).Replace('\','/')
            $taskEntry=$taskVerify.GetEntry('release/'+$taskRelative)
            if(!$taskEntry -or $taskEntry.Length -ne $taskFile.Length){throw "Archive file missing or truncated: $taskRelative"}
            $taskInput=$taskEntry.Open();$taskHash=[Security.Cryptography.SHA256]::Create()
            try {$taskActual=[BitConverter]::ToString($taskHash.ComputeHash($taskInput)).Replace('-','')}
            finally {$taskInput.Dispose();$taskHash.Dispose()}
            if($taskActual -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash){throw "Archive hash mismatch: $taskRelative"}
        }
        foreach($taskRequired in @('ZXMAK2.exe','Test.exe','boot.zip','ROMS.PAK','Config/unity.config','Config/machines.config',
            'Config/Keyboard.config','Lib/Native/ymfm_opl4.dll','Lib/Native/omni_midi.dll','Lib/Native/omni_opn.dll',
            'Lib/Native/SAASound.dll','Resources/SoundBanks/GeneralUser-GS.sf2','Docs/Help/index.html')) {
            if(!$taskVerify.GetEntry('release/'+$taskRequired)){throw "Required dependency omitted: $taskRequired"}
        }
        if($taskClean) {foreach($taskState in $taskRootState){if($taskVerify.GetEntry('release/'+$taskState)){throw 'Clean archive contains active state'}}}
    } finally {$taskVerify.Dispose()}
    Write-Output "PASS archive entry sizes/SHA256/dependencies: $taskZipPath ($($taskSelected.Count) files)"
}
Get-ChildItem -LiteralPath $Package -File -Filter '*.zip' | Get-FileHash |
    Export-Csv -LiteralPath "$Package\diagnostics\package-sha256.csv" -NoTypeInformation -Encoding UTF8
