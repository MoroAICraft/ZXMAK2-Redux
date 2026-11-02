param([string]$Runtime, [string]$Work)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Work){throw 'Use a new private startup-test directory'}
Copy-Item -LiteralPath $Runtime -Destination $Work -Recurse
$taskRefs=@(Get-ChildItem "$Work\Lib\Managed" -Filter 'ZXMAK2*.dll' | ForEach-Object {'/r:'+$_.FullName})
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /main:StartupProbe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$Work\StartupProbe.exe" @taskRefs "$PSScriptRoot\StartupProbe.cs" "$PSScriptRoot\..\..\src\ZXMAK2\Program.cs" "$PSScriptRoot\..\..\src\ZXMAK2\OpenWithDialog.cs" "$PSScriptRoot\..\..\src\PortableBootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Startup probe compilation failed'}
Copy-Item -LiteralPath "$Work\Test.exe.config" -Destination "$Work\StartupProbe.exe.config"
foreach($taskMode in @('direct','shell')) {
    & "$Work\StartupProbe.exe" $taskMode *> "$Work\startup-$taskMode.txt"
    Get-Content -LiteralPath "$Work\startup-$taskMode.txt"
    if($LASTEXITCODE -ne 0){throw "Startup probe failed: $taskMode"}
}
