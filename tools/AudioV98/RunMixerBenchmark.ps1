param(
    [Parameter(Mandatory=$true)][string]$BaselineRuntime,
    [Parameter(Mandatory=$true)][string]$CandidateRuntime,
    [Parameter(Mandatory=$true)][string]$EmptyMediaProfile,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$Frames=200,
    [int]$Rounds=3
)
$ErrorActionPreference='Stop'
if($Frames -lt 50 -or $Frames -gt 1000 -or $Rounds -lt 1 -or $Rounds -gt 5){throw 'Use bounded frames50..1000/rounds1..5'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
# Caller supplies PRIVATE diagnostic runtimes. This script creates only uniquely
# named probe EXE/config/logs; it does not rebuild or alter an emulator DLL.
$benchRoots=@{baseline=(Resolve-Path -LiteralPath $BaselineRuntime).Path;candidate=(Resolve-Path -LiteralPath $CandidateRuntime).Path}
foreach($key in 'baseline','candidate') {
    $runtime=$benchRoots[$key]
    $refs=@(Get-ChildItem -LiteralPath "$runtime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
    & C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /optimize+ /platform:x86 /main:MixerBenchmark /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$runtime\MixerBenchmark98.exe" @refs "$PSScriptRoot\MixerBenchmark.cs" "$PSScriptRoot\..\SoundCards\Opl3Probe.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
    if($LASTEXITCODE -ne 0){throw 'Benchmark compile failed'}
    Copy-Item -LiteralPath "$runtime\Test.exe.config" -Destination "$runtime\MixerBenchmark98.exe.config" -Force
}
$oldPath=$env:PATH
try {
    for($round=0;$round -lt $Rounds;$round++) {
        $order=if(($round%2)-eq 0){@('baseline','candidate')}else{@('candidate','baseline')}
        foreach($workload in 'reset-service','pcm-idle','pcm-18voices') {
            foreach($key in $order) {
                $runtime=$benchRoots[$key];$env:PATH="$runtime\Lib\Native;"+$oldPath
                Push-Location -LiteralPath $runtime
                try {
                    & '.\MixerBenchmark98.exe' $workload $round $Frames $EmptyMediaProfile *> "$OutputDirectory\$key-$workload-$round.txt"
                    if($LASTEXITCODE -ne 0){throw "Benchmark failed: $key/$workload/$round"}
                } finally {Pop-Location}
            }
        }
    }
} finally {$env:PATH=$oldPath}
Write-Output 'Paired full CPU/video/native audio/final mixer measurements saved. No Windows playback latency measured.'
