param(
    [string]$ResearchRoot = 'L:\Work_two\ZX\ZXMAK2-Audio-Research-v81-20261007',
    [string]$EvoConfig = ''
)
$ErrorActionPreference = 'Stop'
# Isolated diagnostic exe only: no emulator DLL is built/replaced.
$audioRuntime = Join-Path $ResearchRoot 'baseline-v81\release'
if ((Get-FileHash -LiteralPath (Join-Path $audioRuntime 'ZXMAK2.Engine.dll')).Hash -ne '89C07730F1E54E8A6058F730E6E3566DD070925704ECB20517ACE14B980E02A2') {
    throw 'Requires unchanged published v81 engine'
}
$testConfig = Join-Path $PSScriptRoot '..\..\src\Test\machines.test.config'
if (!(Test-Path -LiteralPath $testConfig)) { $testConfig = Join-Path $PSScriptRoot 'machines.test.config' }
$testConfig = (Resolve-Path -LiteralPath $testConfig).Path
if ([string]::IsNullOrEmpty($EvoConfig)) { $EvoConfig = Join-Path $ResearchRoot 'diagnostics\source\EvoInput.vmz' }
$exe = Join-Path $audioRuntime 'AudioResearchProbe.exe'
$references = Get-ChildItem -LiteralPath $audioRuntime -Filter 'ZXMAK2*.dll' | ForEach-Object { '/r:' + $_.FullName }
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /unsafe /platform:x86 /target:exe /main:AudioResearchProbe /r:System.Drawing.dll /r:System.Windows.Forms.dll $references "/out:$exe" (Join-Path $PSScriptRoot 'OptimizationProbe.cs') (Join-Path $PSScriptRoot 'AudioResearchProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Probe compilation failed' }
Copy-Item -LiteralPath (Join-Path $audioRuntime 'Test.exe.config') -Destination "$exe.config" -Force
Push-Location -LiteralPath $audioRuntime
try {
    & $exe $testConfig $EvoConfig 2>&1 | Tee-Object -FilePath (Join-Path $ResearchRoot 'diagnostics\components.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Component probe failed' }
}
finally { Pop-Location }
