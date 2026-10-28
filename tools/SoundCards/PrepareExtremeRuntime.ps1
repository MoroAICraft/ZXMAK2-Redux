param([Parameter(Mandatory=$true)][string]$Baseline,
      [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
# Build ZXVM.sln Release/Any CPU and the x86 native OPN bridge first.
# Never target a user's existing installation. The common script refuses it.
& "$PSScriptRoot\..\Portable\PrepareRuntime.ps1" -Baseline $Baseline -Destination $Destination -Version v91
Copy-Item -LiteralPath "$PSScriptRoot\..\..\src\ZX-OmniSound\native\YmfmOpn\omni_opn.dll" -Destination "$Destination\Lib\Native\omni_opn.dll" -Force
Copy-Item -LiteralPath "$PSScriptRoot\Extreme-Rev03.vmz" -Destination "$Destination\ZXMAK2.vmz" -Force
