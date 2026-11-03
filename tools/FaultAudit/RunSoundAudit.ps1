param([string]$Audit = 'D:\ZXMAK2_Work\fault_audit_v95_20261103_run06', [switch]$Recovery)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot\..\..").Path
$auditPath=[IO.Path]::GetFullPath($Audit)
if(-not $auditPath.StartsWith('D:\ZXMAK2_Work\fault_audit_',[StringComparison]::OrdinalIgnoreCase)){throw 'Private audit workspace required.'}
$runtime="$auditPath\runtime\release"
$probe = if ($Recovery) { 'SoundRecoveryProbe' } else { 'SoundFaultProbe' }
$refs=@(Get-ChildItem "$runtime\Lib\Managed\ZXMAK2*.dll" | Where-Object {$_.Name -notin @('ZXMAK2.Host.WinForms.dll','ZXMAK2.DirectX.dll')} | ForEach-Object {'/r:'+$_.FullName})
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /unsafe /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$runtime\$probe.exe" @refs "$repo\tools\FaultAudit\$probe.cs" "$repo\src\ZXMAK2.Host.WinForms\Mdx\DirectSound.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item "$runtime\Test.exe.config" "$runtime\$probe.exe.config"
$info=New-Object System.Diagnostics.ProcessStartInfo
$info.FileName="$runtime\$probe.exe";$info.WorkingDirectory=$runtime;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$child=New-Object System.Diagnostics.Process;$child.StartInfo=$info;[void]$child.Start()
$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
if(-not $child.WaitForExit(30000)){$child.Kill();throw 'Sound audit timeout'}
$child.WaitForExit()
[IO.File]::WriteAllText("$auditPath\diagnostics\$probe.txt",$stdout.Result)
[IO.File]::WriteAllText("$auditPath\diagnostics\$probe.txt.err",$stderr.Result)
Write-Output $stdout.Result;Write-Output $stderr.Result
if($child.ExitCode -ne 0){throw 'Sound audit failed'}
Get-FileHash "$repo\src\ZXMAK2.Host.WinForms\Mdx\DirectSound.cs"
