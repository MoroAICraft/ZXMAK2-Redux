param([Parameter(Mandatory=$true)][string]$Package,[string]$AmstradFolder)
$ErrorActionPreference='Stop'
$taskRuntime=Join-Path $Package 'diagnostics\test-runtime'
if (!(Test-Path -LiteralPath "$taskRuntime\Lib\Managed")) {throw 'Prepare private diagnostics/test-runtime first'}
$taskRefs=@(Get-ChildItem "$taskRuntime\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRuntime\ExtremeProbe.exe" @taskRefs "$PSScriptRoot\ExtremeProbe.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$taskRuntime\Test.exe.config" "$taskRuntime\ExtremeProbe.exe.config" -Force
$taskOldPath=$env:PATH
Push-Location $taskRuntime
try {
    $env:PATH="$taskRuntime\Lib\Native;"+$env:PATH
    $taskArgs=@();if($AmstradFolder){$taskArgs+= $AmstradFolder}
    & "$taskRuntime\ExtremeProbe.exe" @taskArgs *> "$Package\diagnostics\extreme.txt"
    if($LASTEXITCODE -ne 0){Get-Content "$Package\diagnostics\extreme.txt";throw 'Extreme probe failed'}
    Get-Content "$Package\diagnostics\extreme.txt"
} finally {Pop-Location;$env:PATH=$taskOldPath}
