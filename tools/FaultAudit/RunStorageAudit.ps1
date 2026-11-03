param([string]$Destination = 'D:\ZXMAK2_Work\fault_audit_v95_20261103', [string]$Built)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (-not $destinationPath.StartsWith('D:\ZXMAK2_Work\fault_audit_', [StringComparison]::OrdinalIgnoreCase)) { throw 'Private audit workspace required.' }
if (Test-Path -LiteralPath $destinationPath) { throw 'Audit destination must be new.' }
$package = 'L:\Work_two\ZX\ZXMAK2-v95-Open-With-Test-20261103'
$archive = "$package\ZXMAK2-v95-Open-With-Test-Clean.zip"
New-Item -ItemType Directory -Path "$destinationPath\runtime", "$destinationPath\diagnostics" | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archive,"$destinationPath\runtime")
$runtime = "$destinationPath\runtime"
if (Test-Path "$runtime\release\ZXMAK2.exe") { $runtime = "$runtime\release" }
if (-not (Test-Path "$runtime\Lib\Managed\ZXMAK2.Hardware.dll")) { throw 'Unexpected archive layout.' }
if ($Built) {
    foreach ($dll in @('ZXMAK2.Hardware.Circuits.dll','ZXMAK2.Hardware.dll','ZXMAK2.Host.dll','ZXMAK2.Host.WinForms.dll')) {
        Copy-Item -LiteralPath (Join-Path $Built $dll) -Destination "$runtime\Lib\Managed\$dll" -Force
    }
}
$refs = @(Get-ChildItem "$runtime\Lib\Managed\ZXMAK2*.dll" | ForEach-Object { '/r:' + $_.FullName })
$csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$probes = [ordered]@{
    StorageFaultProbe = @('tools\FaultAudit\StorageFaultProbe.cs')
    SdLifecycleProbe = @('tools\SdSessionTest\LifecycleProbe.cs')
    RefreshFolder = @('tools\SdSessionTest\RefreshProbe.cs')
    PortableFolder = @('tools\SdSessionTest\PortableFolderProbe.cs','tools\SdSessionTest\DirectFolderCases.cs')
}
$oldPath = $env:PATH
$env:PATH = "$runtime\Lib\Native;" + $env:PATH
Push-Location $runtime
try {
    foreach ($entry in $probes.GetEnumerator()) {
        $source = @($entry.Value | ForEach-Object { Join-Path $repo $_ })
        & $csc /nologo /target:exe /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$runtime\$($entry.Key).exe" @refs @source
        if ($LASTEXITCODE -ne 0) { throw "Compile failed: $($entry.Key)" }
        Copy-Item "$runtime\Test.exe.config" "$runtime\$($entry.Key).exe.config"
        $log = "$destinationPath\diagnostics\$($entry.Key).txt"
        $arguments = if ($entry.Key -eq 'RefreshFolder') { @("$runtime\Data\Temp") } else { @() }
        if ($Built -and $entry.Key -eq 'StorageFaultProbe') { $arguments = @('--fixed') }
        # No real-source/large-tree modes; each probe creates small disposable data.
        $info = New-Object System.Diagnostics.ProcessStartInfo
        $info.FileName = "$runtime\$($entry.Key).exe"
        $info.WorkingDirectory = $runtime
        $info.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
        $info.UseShellExecute = $false; $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
        if ($entry.Key -eq 'StorageFaultProbe') { $info.StandardOutputEncoding = [Text.Encoding]::UTF8; $info.StandardErrorEncoding = [Text.Encoding]::UTF8 }
        $child = New-Object System.Diagnostics.Process
        $child.StartInfo = $info
        [void]$child.Start()
        $stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
        if (-not $child.WaitForExit(30000)) { $child.Kill(); throw "Probe timeout: $($entry.Key)" }
        $child.WaitForExit()
        [IO.File]::WriteAllText($log,$stdout.Result)
        [IO.File]::WriteAllText("$log.err",$stderr.Result)
        Get-Content $log -Encoding UTF8 -Tail 12
        Get-Content "$log.err" -Encoding UTF8
        if ($child.ExitCode -ne 0) { throw "Probe failed: $($entry.Key); see $log" }
    }
} finally { Pop-Location; $env:PATH = $oldPath }
Get-FileHash $archive
