param(
    [Parameter(Mandatory=$true)][string]$BaselineRuntime,
    [Parameter(Mandatory=$true)][string]$Work,
    [string]$MSBuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
)
$ErrorActionPreference='Stop'
# This runner never starts the desktop or mounts media. Work must be a NEW private directory.
$taskWork=[IO.Path]::GetFullPath($Work)
if(!$taskWork.StartsWith('D:\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $taskWork)) {
    throw 'Choose a new, private directory on D:; no installed runtime is accepted as Work.'
}
$taskSource=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$taskSource.StartsWith('D:\',[StringComparison]::OrdinalIgnoreCase)){throw 'Build only the source copy on D:'}
New-Item -ItemType Directory -Path $taskWork | Out-Null
Copy-Item -LiteralPath $BaselineRuntime -Destination "$taskWork\before" -Recurse
Copy-Item -LiteralPath $BaselineRuntime -Destination "$taskWork\after" -Recurse
Push-Location $taskSource
try {
    & $MSBuild src/ZXMAK2.Hardware.Circuits/ZXMAK2.Hardware.Circuits.csproj /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /m:1 /v:quiet /nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    Copy-Item src\_binrelease\ZXMAK2.Hardware.Circuits.dll "$taskWork\after\Lib\Managed\ZXMAK2.Hardware.Circuits.dll"
} finally {Pop-Location}
foreach($taskKind in @('before','after')) {
    $taskRuntime=Join-Path $taskWork $taskKind
    $taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed\ZXMAK2*.dll" | ForEach-Object {'/r:'+$_.FullName})
    foreach($taskName in @('PsgNoiseZeroProbe','PsgNoiseRoutesProbe','PsgAssemblyAudit')) {
        & C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Core.dll @taskRefs "/out:$taskRuntime\$taskName.exe" "$PSScriptRoot\$taskName.cs"
        if($LASTEXITCODE -ne 0){throw 'Probe compile failed'}
        Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\$taskName.exe.config"
    }
    & "$taskRuntime\PsgNoiseZeroProbe.exe" golden "$taskWork\$taskKind-golden.txt"
    if($LASTEXITCODE -ne 0){throw 'Golden probe failed'}
    & "$taskRuntime\PsgAssemblyAudit.exe" "$taskWork\$taskKind-il.txt" "$taskWork\$taskKind-api.txt"
    if($LASTEXITCODE -ne 0){throw 'Assembly audit failed'}
}
if(Compare-Object (Get-Content "$taskWork\before-golden.txt") (Get-Content "$taskWork\after-golden.txt")){throw 'Nonzero periods changed'}
if(Compare-Object (Get-Content "$taskWork\before-api.txt") (Get-Content "$taskWork\after-api.txt")){throw 'Public API changed'}
Push-Location "$taskWork\after"
try {
    & .\PsgNoiseZeroProbe.exe fixed
    if($LASTEXITCODE -ne 0){throw 'Corrected core tests failed'}
    & .\PsgNoiseRoutesProbe.exe
    if($LASTEXITCODE -ne 0){throw 'PSG routes failed'}
} finally {Pop-Location}
Write-Output 'PASS: core, nonzero golden, API, actual board port/PCM routes. Guest/listening tests are separate.'
