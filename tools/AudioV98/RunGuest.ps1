param(
 [string]$Root='D:\ZXMAK2_Work\audio_v98_20261103',
 [string]$Variant='baseline',
 [Parameter(Mandatory=$true)][string]$Card,
 [Parameter(Mandatory=$true)][string]$Track,
 [string]$RunName='music',
 [int]$Frames=1500,
 [int]$Wait=300,
 [switch]$TraceIo,
 [switch]$ExtremeSaa,
 [int]$Benchmark=0
)
$ErrorActionPreference='Stop'
$runtime=Join-Path $Root "$Variant\release"
$out=Join-Path $Root "$Variant\diagnostics\$Card-$RunName"
if(Test-Path -LiteralPath $out){throw 'Preserve prior run; select a fresh name'}
New-Item -ItemType Directory -Path $out -Force | Out-Null
Copy-Item -LiteralPath "$Root\seed" -Destination "$out\sd" -Recurse
if($ExtremeSaa) {
 if($Card -ne 'extreme'){throw '-ExtremeSaa is only valid for the isolated Extreme test profile'}
 $ini=Join-Path $out 'sd\bin\gp\gp.ini'
 $textIni=[IO.File]::ReadAllText($ini)
 $oldSetting=[regex]::Match($textIni,'(?m)^ExtremeSAA=.*$').Value.Trim()
 if($oldSetting -eq ''){throw 'The copied gp.ini has no existing ExtremeSAA setting'}
 $textIni=[regex]::Replace($textIni,'(?m)^ExtremeSAA=.*$','ExtremeSAA=1')
 [IO.File]::WriteAllText($ini,$textIni,[Text.Encoding]::ASCII)
 [IO.File]::WriteAllText((Join-Path $out 'fixture-setting.txt'),
   "Sandbox-only player setting: $oldSetting -> ExtremeSAA=1`r`n"+
   "Uses existing player setting; no guest code or original media modified.`r`n",[Text.Encoding]::ASCII)
}
$ext=[IO.Path]::GetExtension($Track).ToLowerInvariant()
Copy-Item -LiteralPath $Track -Destination "$out\sd\zzztrack$ext"
# Generated profile only: all external media attributes were stripped by PrepareSandbox.
$profile=[xml](Get-Content -LiteralPath "$Root\profiles\$Card.vmz" -Raw)
foreach($node in @($profile.SelectNodes('//Device'))) {
 if($node.GetAttribute('type') -eq 'ZXMAK2.Hardware.General.AYCHRV, ZXMAK2.Hardware') {
   $node.SetAttribute('type','ZXMAK2.Hardware.Evo.AYCHRV, ZXMAK2.Hardware')
 }
}
$profile.Save("$out\profile.vmz")
$refs=@(Get-ChildItem "$runtime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$runtime\RealNedoProbe.exe" @refs "$PSScriptRoot\..\MoonSoundTest\RealNedoProbe.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item -LiteralPath "$runtime\Test.exe.config" -Destination "$runtime\RealNedoProbe.exe.config" -Force
$oldPath=$env:PATH
Push-Location $runtime
try {
 $env:PATH="$runtime\Lib\Native;"+$env:PATH
 $argsGuest=@("$out\profile.vmz","$out\sd",$out,'no-trace')
 if($TraceIo){$argsGuest+='iohash'}
 $argsGuest+=@('key:D5','wait:300','key:F10','key:Return','gp','wait:120','key:Home','key:Return','key:End','key:Return',"wait:$Wait","audio:music:$Frames")
 if($Benchmark -gt 0){$argsGuest+="bench:$Benchmark"}
 & "$runtime\RealNedoProbe.exe" @argsGuest *> "$out\guest.txt"
 $code=$LASTEXITCODE
 Get-Content -LiteralPath "$out\guest.txt" | Select-Object -Last 20
 if($code -ne 0){throw "Guest failed $code"}
} finally {Pop-Location;$env:PATH=$oldPath}
