param(
    [Parameter(Mandatory=$true)][string]$Runtime,
    [Parameter(Mandatory=$true)][string]$FixtureArchive,
    [Parameter(Mandatory=$true)][string]$Work,
    [switch]$Smoke
)
$ErrorActionPreference='Stop'
$taskWork=[IO.Path]::GetFullPath($Work)
if(Test-Path -LiteralPath $taskWork){throw 'Work must be a NEW private directory, never an installed emulator.'}
if(!(Test-Path -LiteralPath "$Runtime\Lib\Managed\ZXMAK2.Engine.Cpu.dll")){throw 'Portable runtime required'}
New-Item -ItemType Directory -Path "$taskWork\logs" -Force | Out-Null
Copy-Item -LiteralPath $Runtime -Destination "$taskWork\runtime" -Recurse
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($FixtureArchive),"$taskWork\fixture")
$taskRelease="$taskWork\runtime"
$taskFixture="$taskWork\fixture"
$taskRefs=@(Get-ChildItem -LiteralPath "$taskRelease\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /platform:x86 /r:System.Drawing.dll "/out:$taskRelease\NativeNmiProbe.exe" @taskRefs `
    "$taskFixture\tools\CommanderMediaProbe.cs" "$taskFixture\tools\SdWriteChecks.cs" "$PSScriptRoot\NativeNmiFixtureHooks.cs"
if($LASTEXITCODE -ne 0){throw 'Native NMI probe compilation failed'}
Copy-Item -LiteralPath "$taskRelease\Test.exe.config" -Destination "$taskRelease\NativeNmiProbe.exe.config"
$taskArgs=@($taskFixture,"$taskRelease\Lib\Managed","$taskFixture\Build\probe\zxevo77.rom",
    "$taskFixture\Build\probe\BC086-ui.sna","$taskFixture\Build\probe\BC052-tapes-SD.ima",'--hardware-timing','--nmi-swap')
function Invoke-Nmi([string]$Name,[string[]]$Options){
    & "$taskRelease\NativeNmiProbe.exe" @taskArgs @Options *> "$taskWork\logs\$Name.txt"
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath "$taskWork\logs\$Name.txt" -Tail 15; throw "Native NMI failed: $Name"}
    Get-Content -LiteralPath "$taskWork\logs\$Name.txt" -Tail 1
}
$taskRates=if($Smoke){@(2)}else{@(1,2,4)}
$taskPrefixes=if($Smoke){@('ED')}else{@('ED','DD','FD','CB','DDCB','FDCB')}
foreach($taskRate in $taskRates){foreach($taskPrefix in $taskPrefixes){
    Invoke-Nmi "$taskPrefix-rate$taskRate" @('--nmi-v94-prefix',$taskPrefix,'--nmi-v94-rate',"$taskRate")
}}
Invoke-Nmi 'ordinary' @()
if(!$Smoke){
    Invoke-Nmi '48' @('--nmi-48')
    Invoke-Nmi 'ei' @('--nmi-ei')
    Invoke-Nmi '48-ei' @('--nmi-48','--nmi-ei')
}
Write-Output 'PASS: native BC prefix NMI/resume. Scripted guest keys, not physical desktop input.'
