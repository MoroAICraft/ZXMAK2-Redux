param([string]$Built, [string]$Work)
$ErrorActionPreference='Stop'
$taskWork=[IO.Path]::GetFullPath($Work)
if(Test-Path -LiteralPath $taskWork){throw 'Use a new disposable test directory'}
$taskProbe=Join-Path $taskWork 'probe'
$taskDispatcher=Join-Path $taskWork 'dispatcher'
New-Item -ItemType Directory -Path $taskProbe,$taskDispatcher | Out-Null
foreach($taskDir in @($taskProbe,$taskDispatcher)) {
    foreach($taskDll in @('ZXMAK2.Logging.dll','log4net.dll')) {
        Copy-Item -LiteralPath (Join-Path $Built $taskDll) -Destination $taskDir
    }
}
# EXE dependencies are needed for JIT resolution, but intentionally NO VMZ,
# Config/ROM/native DLLs. A forwarded request must never initialize a machine.
foreach($taskFile in @('ZXMAK2.exe','ZXMAK2.exe.config','ZXMAK2.Dependency.dll','ZXMAK2.Host.Presentation.dll','ZXMAK2.Host.dll','ZXMAK2.Engine.dll','ZXMAK2.Mvvm.dll')) {
    Copy-Item -LiteralPath (Join-Path $Built $taskFile) -Destination $taskDispatcher
}
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$taskProbe\ZXMAK2.Logging.dll" "/out:$taskProbe\ZXMAK2.exe" "$PSScriptRoot\OpenWithProbe.cs" "$PSScriptRoot\..\..\src\ZXMAK2\OpenWithDialog.cs"
if($LASTEXITCODE -ne 0){throw 'Probe compilation failed'}
& "$taskProbe\ZXMAK2.exe" "$taskDispatcher\ZXMAK2.exe" *> "$taskWork\open-with.txt"
Get-Content -LiteralPath "$taskWork\open-with.txt"
if($LASTEXITCODE -ne 0){throw 'Open With probe failed'}
