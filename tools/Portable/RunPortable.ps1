param([string]$Package='L:\Work_two\ZX\ZXMAK2-v87-Portable-Test-20261010')
$ErrorActionPreference='Stop'
$taskRuntime=Join-Path $Package 'release'
$taskDiagnostics=Join-Path $Package 'diagnostics'
$taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRuntime\PortableProbe.exe" @taskRefs "$PSScriptRoot\PortableProbe.cs"
if($LASTEXITCODE -ne 0){throw 'Portable probe compile failed'}
Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\PortableProbe.exe.config"
$taskPreviousPath=$env:PATH
try {
    $env:PATH="$taskRuntime\Lib\Native;"+$env:PATH
    foreach($taskMode in @('','models')) {
        $taskLabel=if($taskMode){$taskMode}else{'core'}
        & "$taskRuntime\PortableProbe.exe" $taskMode *> "$taskDiagnostics\portable-$taskLabel.txt"
        if($LASTEXITCODE -ne 0){throw "Portable probe failed: $taskMode"}
    }
    $taskMoved=Join-Path $taskDiagnostics ('Moved # portable Кириллица '+[Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $taskMoved | Out-Null
    foreach($taskFile in Get-ChildItem -LiteralPath $taskRuntime -File) {
        if($taskFile.Name -notmatch 'Probe|KeyboardRouting|GoldenRenderer|ContentionFloatingBus|InterruptNmi|WaitPort|VideoTiming|TapeClock|RefreshFolder|SdWriteProtocol|PentEvoProfile' -or $taskFile.Name -like 'PortableProbe*') {
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskMoved
        }
    }
    foreach($taskSub in @('Lib','Config','Resources','Docs')) {
        Copy-Item -LiteralPath (Join-Path $taskRuntime $taskSub) -Destination (Join-Path $taskMoved $taskSub) -Recurse
    }
    # Do not let an inherited developer PATH hide broken native lookup.
    $env:PATH=$taskPreviousPath
    Push-Location $env:WINDIR
    try {
        & "$taskMoved\PortableProbe.exe" relocated *> "$taskDiagnostics\portable-relocated.txt"
        if($LASTEXITCODE -ne 0){throw 'Relocated path/config/ROM test failed'}
        & "$taskMoved\Test.exe" /moonsound *> "$taskDiagnostics\portable-relocated-moonsound.txt"
        if($LASTEXITCODE -ne 0){throw 'Relocated native MoonSound test failed'}
        & "$taskMoved\Test.exe" /midisynth *> "$taskDiagnostics\portable-relocated-midi.txt"
        if($LASTEXITCODE -ne 0){throw 'Relocated SoundBank/native MIDI test failed'}
    } finally {Pop-Location}
    Write-Output "PASS: portable protection, models, renamed Unicode/# path, foreign CWD, native sound and bank. Copy retained: $taskMoved"
} finally {$env:PATH=$taskPreviousPath}
