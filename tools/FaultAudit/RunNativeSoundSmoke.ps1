param([Parameter(Mandatory=$true)][string]$Audit)
$ErrorActionPreference='Stop'
$auditPath=[IO.Path]::GetFullPath($Audit)
if(-not $auditPath.StartsWith('D:\ZXMAK2_Work\fault_audit_',[StringComparison]::OrdinalIgnoreCase)){throw 'Private audit workspace required.'}
$runtime="$auditPath\runtime\release"
$refs=@(Get-ChildItem "$runtime\Lib\Managed\ZXMAK2*.dll" | ForEach-Object {'/r:'+$_.FullName})
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$runtime\NativeSoundSmoke.exe" @refs "$PSScriptRoot\NativeSoundSmoke.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$runtime\Test.exe.config" "$runtime\NativeSoundSmoke.exe.config"
$info=New-Object System.Diagnostics.ProcessStartInfo
$info.FileName="$runtime\NativeSoundSmoke.exe";$info.WorkingDirectory=$runtime;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$child=New-Object System.Diagnostics.Process;$child.StartInfo=$info;[void]$child.Start()
$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
if(-not $child.WaitForExit(12000)){$child.Kill();throw 'Native smoke timeout'}
$child.WaitForExit()
[IO.File]::WriteAllText("$auditPath\diagnostics\NativeSoundSmoke.txt",$stdout.Result)
[IO.File]::WriteAllText("$auditPath\diagnostics\NativeSoundSmoke.txt.err",$stderr.Result)
Write-Output $stdout.Result;Write-Output $stderr.Result
if($child.ExitCode -ne 0){throw 'Native smoke failed'}
