param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$SeedFolder,
    [Parameter(Mandatory=$true)][string]$Vgm,
    [switch]$MultiSound,
    [ValidatePattern('^[a-z0-9-]+$')][string]$RunName='playback'
)
$ErrorActionPreference='Stop'
$opl3Runtime=Join-Path $Package 'release'
$opl3Label=if($MultiSound){'opl3-multisound-guest'}else{'opl3-guest'}
$opl3Label+='-'+$RunName
$opl3Output=Join-Path $Package "diagnostics\$opl3Label"
if(Test-Path -LiteralPath $opl3Output){throw 'Preserve previous guest evidence; use a new private package'}
New-Item -ItemType Directory -Path $opl3Output | Out-Null
$opl3Sd=Join-Path $opl3Output 'sd-copy'
Copy-Item -LiteralPath $SeedFolder -Destination $opl3Sd -Recurse
Copy-Item -LiteralPath $Vgm -Destination "$opl3Sd\zzzopl3.vgz"
$opl3Config=[xml](Get-Content -LiteralPath "$opl3Runtime\ZXMAK2.vmz" -Raw)
if($MultiSound){
    $opl3Bus=$opl3Config.VirtualMachine.Bus
    $opl3Ula=$opl3Bus.Device | Where-Object {$_.type -match '\.UlaPentEvo,'}
    $opl3Ula.SetAttribute('zxBusSlot2Enabled','True');$opl3Ula.SetAttribute('zxBusSlot2Device','MultiSound')
    $opl3Multi=$opl3Config.CreateElement('Device');$opl3Multi.SetAttribute('type','ZXMAK2.Hardware.Evo.ZxMultiSoundDevice, ZXMAK2.Hardware');[void]$opl3Bus.AppendChild($opl3Multi)
}
$opl3Config.Save([IO.Path]::GetFullPath("$opl3Output\profile.vmz"))
$opl3Refs=@(Get-ChildItem "$opl3Runtime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$opl3Runtime\RealNedoProbe.exe" @opl3Refs "$PSScriptRoot\..\MoonSoundTest\RealNedoProbe.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$opl3Runtime\Test.exe.config" "$opl3Runtime\RealNedoProbe.exe.config" -Force
$opl3OldPath=$env:PATH
Push-Location $opl3Runtime
try {
    $env:PATH="$opl3Runtime\Lib\Native;"+$env:PATH
    & "$opl3Runtime\RealNedoProbe.exe" "$opl3Output\profile.vmz" $opl3Sd $opl3Output no-trace key:D5 wait:300 key:F10 key:Return gp wait:120 key:Home key:Return key:End key:Return wait:1800 audio:opl3:150 *> "$opl3Output\guest.txt"
    Get-Content "$opl3Output\guest.txt"
    if($LASTEXITCODE -ne 0){throw 'Guest run failed'}
    $opl3Capture=Select-String -LiteralPath "$opl3Output\guest.txt" -Pattern 'source=\d+-ZxOpl3Ur4qbpDevice .*peak=(\d+)'
    if(!$opl3Capture -or [int]$opl3Capture.Matches[0].Groups[1].Value -lt 100){throw 'Guest did not produce OPL3 audio; inspect captured screens'}
    Write-Output 'PASS: unmodified guest player produced OPL3 PCM. Inspect screen for selected track/output.'
} finally {Pop-Location;$env:PATH=$opl3OldPath}
