param([Parameter(Mandatory=$true)][string]$Destination,
 [string]$Built='L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease',
 [string]$Baseline='L:\Work_two\ZX\ZXMAK2-v97-ZX-OPL3-Test-20261103\release')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Destination){throw 'Preserve existing runtime; choose a fresh destination'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Get-ChildItem -LiteralPath $Baseline -File | Where-Object {$_.Name -notmatch '^README-v\d+\.txt$'} | Copy-Item -Destination $Destination
foreach($sub in 'Lib','Config','Resources','Docs'){Copy-Item -LiteralPath "$Baseline\$sub" -Destination "$Destination\$sub" -Recurse}
# Keep all old native cores, launcher and unrelated managed components exact.
foreach($name in 'ZXMAK2.Host.dll','ZXMAK2.Hardware.dll') {
 Copy-Item -LiteralPath "$Built\$name" -Destination "$Destination\Lib\Managed\$name" -Force
}
# /omni-opl now checks OPL4 versus YMF262's documented mixer difference.
Copy-Item -LiteralPath "$Built\Test.exe" -Destination "$Destination\Test.exe" -Force
& 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\CorFlags.exe' "$Destination\Test.exe" /32BIT+ /nologo
if($LASTEXITCODE -ne 0){throw 'x86 Test flag failed'}
foreach($sub in 'State','Sessions','Saves','Recordings','Logs','Backups','Cache','Temp') {
 New-Item -ItemType Directory -Path "$Destination\Data\$sub" -Force | Out-Null
}
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\releases\README-v98.txt" -Destination "$Destination\README-v98.txt"
$profile=[xml](Get-Content -LiteralPath "$Destination\ZXMAK2.vmz" -Raw)
foreach($attr in @($profile.SelectNodes('//@sdPath|//@sdImage|//@ideImage|//@ideCdDrive|//@hostFolder|//@fileName|//@neoGsSdImage'))) {
 if($attr.Value){throw "Unexpected baseline media: $($attr.Value)"}
}
Write-Output "Prepared clean isolated v98 runtime: $Destination"
