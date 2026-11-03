param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$opl3Runtime=Join-Path $Package 'release'
if (!(Test-Path -LiteralPath "$opl3Runtime\Lib\Managed")) {throw 'Prepare private disposable release first'}
$opl3Refs=@(Get-ChildItem "$opl3Runtime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$opl3Runtime\Opl3Probe.exe" @opl3Refs "$PSScriptRoot\Opl3Probe.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$opl3Runtime\Test.exe.config" "$opl3Runtime\Opl3Probe.exe.config" -Force
$opl3OldPath=$env:PATH
Push-Location $opl3Runtime
try {
    $env:PATH="$opl3Runtime\Lib\Native;"+$env:PATH
    & "$opl3Runtime\Opl3Probe.exe" *> "$Package\diagnostics\opl3.txt"
    Get-Content "$Package\diagnostics\opl3.txt"
    if($LASTEXITCODE -ne 0){throw 'OPL3 probe failed'}
} finally {Pop-Location;$env:PATH=$opl3OldPath}
