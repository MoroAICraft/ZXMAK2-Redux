param(
    [Parameter(Mandatory=$true)][string]$Destination,
    [Parameter(Mandatory=$true)][string]$Built,
    [string]$Baseline='L:\Work_two\ZX\ZXMAK2-v96-Fault-Recovery-Test-20261103\release'
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Destination){throw 'Preserve existing runtime; choose a fresh destination'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Get-ChildItem -LiteralPath $Baseline -File | Where-Object {$_.Name -notmatch '^README-v\d+\.txt$'} | Copy-Item -Destination $Destination
foreach($opl3Dir in @('Lib','Config','Resources','Docs')) {Copy-Item -LiteralPath (Join-Path $Baseline $opl3Dir) -Destination (Join-Path $Destination $opl3Dir) -Recurse}
foreach($opl3Dll in @('ZXMAK2.Hardware.dll','ZXMAK2.Host.WinForms.dll')) {Copy-Item -LiteralPath (Join-Path $Built $opl3Dll) -Destination "$Destination\Lib\Managed\$opl3Dll" -Force}
Copy-Item -LiteralPath (Join-Path $Built 'ymfm_opl3.dll') -Destination "$Destination\Lib\Native\ymfm_opl3.dll" -Force
Copy-Item -LiteralPath "$PSScriptRoot\..\..\src\Libraries\YmfmOpl4\LICENSE" -Destination "$Destination\Docs\Licenses\ymfm-opl3.LICENSE.txt"
Copy-Item -LiteralPath "$PSScriptRoot\..\..\src\Libraries\YmfmOpl3\Licenses" -Destination "$Destination\Docs\Licenses\OPL3-Toolchain" -Recurse
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\Help\index.html" -Destination "$Destination\Docs\Help\index.html" -Force
Copy-Item -LiteralPath "$PSScriptRoot\..\..\docs\releases\README-v97.txt" -Destination "$Destination\README-v97.txt"
foreach($opl3Dir in @('State','Sessions','Saves','Recordings','Logs','Backups','Cache','Temp')) {New-Item -ItemType Directory -Path "$Destination\Data\$opl3Dir" -Force | Out-Null}
# Test-only default: board alone, no attached user media. These XML transforms
# create package artifacts; the accepted baseline files remain untouched.
$opl3Profile=[xml](Get-Content -LiteralPath "$Destination\ZXMAK2.vmz" -Raw)
$opl3Bus=$opl3Profile.VirtualMachine.Bus
foreach($opl3Device in @($opl3Bus.Device)) {
    if($opl3Device.type -match 'ZxmSoundCardExtreme|ZxMultiSound|ZxmMoonSound|ZxOmniSound|NeoGsDevice|AYCHRV|TurboSoundFmPro') {[void]$opl3Bus.RemoveChild($opl3Device)}
}
$opl3Ula=$opl3Bus.Device | Where-Object {$_.type -match '\.UlaPentEvo,'}
$opl3Ula.SetAttribute('internalSound','None')
$opl3Ula.SetAttribute('zxBusSlot1Enabled','True');$opl3Ula.SetAttribute('zxBusSlot1Device','Opl3Ur4qbp')
$opl3Ula.SetAttribute('zxBusSlot2Enabled','False');$opl3Ula.SetAttribute('zxBusSlot2Device','Empty')
$opl3Board=$opl3Profile.CreateElement('Device');$opl3Board.SetAttribute('type','ZXMAK2.Hardware.Evo.ZxOpl3Ur4qbpDevice, ZXMAK2.Hardware');$opl3Board.SetAttribute('volume','100');[void]$opl3Bus.AppendChild($opl3Board)
foreach($opl3Attr in @($opl3Bus.SelectNodes('.//@sdPath|.//@sdImage|.//@ideImage|.//@ideCdDrive|.//@hostFolder|.//@fileName'))) {if($opl3Attr.Value){throw "Unexpected baseline media path: $($opl3Attr.Value)"}}
$opl3Profile.Save([IO.Path]::GetFullPath("$Destination\ZXMAK2.vmz"))
Write-Output "Prepared v97 standalone OPL3 runtime: $Destination"
