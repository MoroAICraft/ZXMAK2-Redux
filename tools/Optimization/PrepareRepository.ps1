param(
    [string]$Repository = 'L:\Work_two\ZX\ZXMAK2-Fork',
    [string]$Archive = 'L:\Work_two\ZX\ZXMAK2-Local-Archive-20261007'
)
$ErrorActionPreference = 'Stop'
function RunGit { & git -C $Repository @args; if ($LASTEXITCODE -ne 0) { throw "git failed: $args" } }
if (Test-Path -LiteralPath $Archive) { throw 'Archive already exists: inspect it instead of overwriting.' }
$taskRepo = (Resolve-Path -LiteralPath $Repository).Path.TrimEnd('\')
if ($taskRepo -ne 'L:\Work_two\ZX\ZXMAK2-Fork') { throw 'Unexpected repository target.' }
if ([IO.Path]::GetFullPath($Archive) -ne 'L:\Work_two\ZX\ZXMAK2-Local-Archive-20261007') { throw 'Unexpected archive target.' }
$taskBase = '1993dd0b18579a22131557074c70582ff269d6a9'
RunGit merge-base --is-ancestor HEAD $taskBase
$taskAllowed = @('src/ZXMAK2.Host.WinForms/Mdx/DirectSound.cs', 'src/ZXMAK2/unity.config',
    'tools/BaseConfAudioTest/DirectSoundBufferProbe.cs')
$taskDirty = @(& git -C $Repository diff --name-only)
if (@($taskDirty | Where-Object { $_ -notin $taskAllowed }).Count) { throw 'Unexpected tracked edits.' }
if (@(& git -C $Repository diff --cached --name-only).Count) { throw 'Unexpected staged edits.' }
New-Item -ItemType Directory -Path $Archive, "$Archive\primary-wip", "$Archive\control-v79" | Out-Null
RunGit status --short | Out-File -LiteralPath "$Archive\primary-wip\status-before.txt" -Encoding UTF8
RunGit worktree list --porcelain | Out-File -LiteralPath "$Archive\worktrees-before.txt" -Encoding UTF8
RunGit diff --binary "--output=$Archive\primary-wip\unpublished-audio.patch"
foreach ($taskFile in $taskAllowed) {
    $taskTarget = Join-Path "$Archive\primary-wip\files" $taskFile
    New-Item -ItemType Directory -Path (Split-Path $taskTarget) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $Repository $taskFile) -Destination $taskTarget
}
$taskBuild = Join-Path $Repository '.codex-build'
if ((Resolve-Path -LiteralPath $taskBuild).Path -ne "$taskRepo\.codex-build") { throw 'Unsafe build target.' }
Move-Item -LiteralPath $taskBuild -Destination "$Archive\primary-wip\unpublished-runtime"
RunGit switch -c codex/archive-primary-audio-wip-20261007
RunGit add -- @taskAllowed
RunGit commit -m 'Archive unpublished adaptive audio work before v79 repository update'
$taskWipCommit = (& git -C $Repository rev-parse HEAD).Trim()
RunGit archive --format=zip "--output=$Archive\primary-wip\source-with-wip.zip" HEAD
RunGit switch codex/zxbus-baseconf
RunGit merge --ff-only $taskBase
RunGit tag -a zxmak2-v79-control $taskBase -m 'Published v79 control before performance optimization; runtime archives preserved on L:'
RunGit archive --format=zip "--output=$Archive\control-v79\ZXMAK2-v79-source.zip" $taskBase
# Preserve detached/prunable heads before pruning only stale registrations.
$taskWorktreeLines = @(& git -C $Repository worktree list --porcelain)
foreach ($taskLine in $taskWorktreeLines) {
    if ($taskLine -match '^HEAD ([0-9a-f]{40})$') {
        $taskSha = $Matches[1]
        $taskRef = "refs/archive/worktrees-20261007/$taskSha"
        RunGit update-ref $taskRef $taskSha
    }
}
RunGit bundle create "$Archive\ZXMAK2-local-history.bundle" --all
RunGit bundle verify "$Archive\ZXMAK2-local-history.bundle"
RunGit worktree prune --dry-run --verbose | Out-File -LiteralPath "$Archive\pruned-registrations.txt" -Encoding UTF8
RunGit worktree prune --verbose
RunGit switch -c codex/optimization-v80
$taskManifest = [ordered]@{
    Date = '2026-10-07'; Repository = $Repository; ControlCommit = $taskBase
    ControlTag = 'zxmak2-v79-control'; OptimizationBranch = 'codex/optimization-v80'
    PreservedWipCommit = $taskWipCommit; PreservedWipBranch = 'codex/archive-primary-audio-wip-20261007'
    ControlRelease = 'L:\Work_two\ZX\ZXMAK2-v79-ZXEVO-BC-F10-GuestOnly-Test-20261007'
    Note = 'No existing releases, user media or active worktrees deleted. .codex-build moved intact.'
}
$taskManifest | ConvertTo-Json | Out-File -LiteralPath "$Archive\manifest.json" -Encoding UTF8
Get-ChildItem -LiteralPath "$Archive\control-v79", "$Archive\primary-wip" -File -Recurse |
    Get-FileHash -Algorithm SHA256 | Export-Csv -LiteralPath "$Archive\backup-sha256.csv" -NoTypeInformation -Encoding UTF8
RunGit status --short
