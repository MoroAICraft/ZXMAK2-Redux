$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = 'L:\Work_two\ZX\ZXMAK2-Fork\tmp'
$taskArchive = 'L:\Work_two\ZX\ZXMAK2-Local-Archive-20261007\redundant-extractions'
New-Item -ItemType Directory -Path $taskArchive -Force | Out-Null
$taskNames = @('b34-zip-verify-140643', 'b36-zip-verify-181135',
    'b37-zipverify-20260920-214709', 'b39-zip-verify-20260921-102501', 'release-alpha-0.1-verify')
foreach ($taskName in $taskNames) {
    $taskPath = Join-Path $taskRoot $taskName
    if (!(Test-Path -LiteralPath $taskPath)) { continue }
    $taskResolved = (Resolve-Path -LiteralPath $taskPath).Path.TrimEnd('\')
    if ($taskResolved -ne "$taskRoot\$taskName") { throw 'Unexpected cleanup target.' }
    $taskEntries = @(Get-ChildItem -LiteralPath $taskResolved -Recurse -Force)
    if ($taskEntries | Where-Object { $_.Name -eq '.git' -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }) {
        throw "Embedded Git/reparse point: do not remove $taskResolved"
    }
    $taskZip = Join-Path $taskArchive ($taskName + '.zip')
    if (Test-Path -LiteralPath $taskZip) { throw 'Existing archive: verify manually, do not overwrite.' }
    [IO.Compression.ZipFile]::CreateFromDirectory($taskResolved, $taskZip,
        [IO.Compression.CompressionLevel]::Optimal, $false)
    $taskVerify = [IO.Compression.ZipFile]::OpenRead($taskZip)
    try {
        $taskFiles = @($taskEntries | Where-Object { !$_.PSIsContainer })
        if (@($taskVerify.Entries | Where-Object { $_.Name }).Count -ne $taskFiles.Count) { throw 'Archive file count mismatch.' }
        foreach ($taskFile in $taskFiles) {
            $taskRelative = $taskFile.FullName.Substring($taskResolved.Length + 1).Replace('\','/')
            $taskEntry = $taskVerify.GetEntry($taskRelative)
            if (!$taskEntry -or $taskEntry.Length -ne $taskFile.Length) { throw "Archive mismatch: $taskRelative" }
            $taskHash = [Security.Cryptography.SHA256]::Create(); $taskInput = $taskEntry.Open()
            try { $taskActual = [BitConverter]::ToString($taskHash.ComputeHash($taskInput)).Replace('-','') }
            finally { $taskInput.Dispose(); $taskHash.Dispose() }
            if ($taskActual -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash) { throw 'SHA256 mismatch.' }
        }
    } finally { $taskVerify.Dispose() }
    # Exact target validated above; archive is verified before recursive removal.
    Remove-Item -LiteralPath $taskResolved -Recurse -Force
    Write-Output "Archived and removed duplicate extraction: $taskResolved -> $taskZip"
}
Get-ChildItem -LiteralPath $taskArchive -File -Filter '*.zip' | Get-FileHash |
    Export-Csv -LiteralPath "$taskArchive\sha256.csv" -NoTypeInformation -Encoding UTF8
