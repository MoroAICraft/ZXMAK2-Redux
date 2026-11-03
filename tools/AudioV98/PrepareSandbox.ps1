param([string]$Root='D:\ZXMAK2_Work\audio_v98_20261103')
$ErrorActionPreference='Stop'
$accepted='L:\Work_two\ZX\ZXMAK2-v97-ZX-OPL3-Test-20261103\release'
$installed='D:\release_97'
$media='L:\Work_two\ZX\ZX_IMG\flash_test_redux'
$baseline=Join-Path $Root 'baseline'
if(Test-Path -LiteralPath $baseline){throw 'Preserve existing baseline'}
New-Item -ItemType Directory -Path "$baseline\release","$baseline\diagnostics","$Root\source-state","$Root\seed","$Root\profiles" -Force | Out-Null
$checks=foreach($file in Get-ChildItem -LiteralPath $accepted -Recurse -File | Where-Object {$_.Extension -in '.exe','.dll'}) {
    $relative=$file.FullName.Substring($accepted.Length+1)
    $other=Join-Path $installed $relative
    $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if(!(Test-Path -LiteralPath $other)){throw "Missing installed binary $relative"}
    $installedHash=(Get-FileHash -LiteralPath $other -Algorithm SHA256).Hash
    if($hash -ne $installedHash){throw "Installed binary differs from accepted v97: $relative"}
    [pscustomobject]@{File=$relative;AcceptedSHA256=$hash;InstalledSHA256=$installedHash}
}
$checks | Export-Csv -LiteralPath "$Root\source-state\v97-binary-identity.csv" -NoTypeInformation -Encoding UTF8
Get-ChildItem -LiteralPath $accepted -File | Copy-Item -Destination "$baseline\release"
foreach($sub in 'Lib','Config','Resources','Docs'){Copy-Item -LiteralPath "$accepted\$sub" -Destination "$baseline\release\$sub" -Recurse}
foreach($name in 'ZXMAK2.vmz','ZXMAK2.vmide','ZXMAK2.cmos','ZXMAK2.exe.config') {
    Copy-Item -LiteralPath "$installed\$name" -Destination "$Root\source-state\$name"
}
# All guest input is copied. Do not bring user's large disk images or journals.
Copy-Item -LiteralPath "$media\bin","$media\ini" -Destination "$Root\seed" -Recurse
foreach($name in 'sd_boot.$C','AUTOBOOT.INI') {Copy-Item -LiteralPath (Join-Path $media $name) -Destination "$Root\seed"}
Get-ChildItem -LiteralPath "$Root\seed" -Recurse -File | ForEach-Object {
    [pscustomobject]@{File=$_.FullName.Substring(($Root+'\seed\').Length);Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
} | Export-Csv -LiteralPath "$Root\source-state\guest-seed.csv" -NoTypeInformation -Encoding UTF8
$cards=[ordered]@{ay='AYCHRV';tsfm='TurboSoundFmPro';multi='ZxMultiSoundDevice';max='ZxMultiSoundMaxDevice';extreme='ZxmSoundCardExtreme';moon='ZxmMoonSoundDevice';omni='ZxOmniSoundDevice';opl3='ZxOpl3Ur4qbpDevice';pair='ZxOpl3Ur4qbpDevice'}
$slots=@{multi='MultiSound';max='MultiSoundMax';extreme='SoundCardExtreme';moon='MoonSound';omni='OmniSound';opl3='Opl3Ur4qbp';pair='Opl3Ur4qbp'}
foreach($entry in $cards.GetEnumerator()) {
    $profile=[xml](Get-Content -LiteralPath "$installed\ZXMAK2.vmz" -Raw)
    $bus=$profile.VirtualMachine.Bus
    foreach($node in @($bus.Device)) {
        if($node.type -match 'ZxmSoundCardExtreme|ZxMultiSound|ZxmMoonSound|ZxOmniSound|ZxOpl3|NeoGsDevice|AYCHRV|AY8910|TurboSoundFmPro') {[void]$bus.RemoveChild($node)}
    }
    foreach($attr in @($bus.SelectNodes('.//@sdPath|.//@sdImage|.//@ideImage|.//@ideCdDrive|.//@hostFolder|.//@fileName|.//@neoGsSdImage'))) {$attr.Value=''}
    $ula=$bus.Device | Where-Object {$_.type -match '\.UlaPentEvo,'}
    $ula.SetAttribute('internalSound','None');$ula.SetAttribute('internalSoundVolume','100')
    $ula.SetAttribute('zxBusSlot1Enabled','False');$ula.SetAttribute('zxBusSlot1Device','Empty')
    $ula.SetAttribute('zxBusSlot2Enabled','False');$ula.SetAttribute('zxBusSlot2Device','Empty')
    $namespace='ZXMAK2.Hardware.Evo.'
    if($entry.Key -eq 'ay') {$ula.SetAttribute('internalSound','AY8910CHRV')}
    elseif($entry.Key -eq 'tsfm') {$ula.SetAttribute('internalSound','TurboSoundFmPro')}
    else {$ula.SetAttribute('zxBusSlot1Enabled','True');$ula.SetAttribute('zxBusSlot1Device',$slots[$entry.Key])}
    $device=$profile.CreateElement('Device');$device.SetAttribute('type',($namespace+$entry.Value+', ZXMAK2.Hardware'));$device.SetAttribute('volume','100');[void]$bus.AppendChild($device)
    if($entry.Key -eq 'pair') {
        $ula.SetAttribute('zxBusSlot2Enabled','True');$ula.SetAttribute('zxBusSlot2Device','MultiSound')
        $second=$profile.CreateElement('Device');$second.SetAttribute('type','ZXMAK2.Hardware.Evo.ZxMultiSoundDevice, ZXMAK2.Hardware');$second.SetAttribute('volume','100');[void]$bus.AppendChild($second)
    }
    $profile.Save("$Root\profiles\$($entry.Key).vmz")
}
Write-Output "Verified $($checks.Count) v97 binaries; isolated baseline and copied guest seed: $Root"
