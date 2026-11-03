param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$Zig)
$ErrorActionPreference='Stop'
$opl3Output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $opl3Output -Force | Out-Null
$opl3Source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\YmfmOpl4'))
if ($Zig) {
    # Portable Zig 0.15.2 is the v97 reference toolchain. No installation,
    # user-specific compiler path or additional DLL is needed by the runtime.
    $opl3OldCache=$env:ZIG_GLOBAL_CACHE_DIR
    try {
        $env:ZIG_GLOBAL_CACHE_DIR=Join-Path $opl3Output 'zig-cache'
        & $Zig c++ -target x86-windows-gnu -shared -O2 -std=c++14 "-I$opl3Source" "$PSScriptRoot\ymfm_opl3_wrapper.cpp" "$opl3Source\ymfm_opl.cpp" "$opl3Source\ymfm_pcm.cpp" "$opl3Source\ymfm_adpcm.cpp" -o "$opl3Output\ymfm_opl3.dll"
        if($LASTEXITCODE -ne 0){throw 'OPL3 native Zig build failed'}
    } finally {$env:ZIG_GLOBAL_CACHE_DIR=$opl3OldCache}
    exit 0
}
$opl3Vs='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools'
$opl3Compiler=Get-ChildItem "$opl3Vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1
if(!$opl3Compiler){throw 'VS2022 x86 C++ Build Tools not found'}
$opl3Sdk=Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\Include' -Directory | Where-Object {Test-Path "$($_.FullName)\ucrt"} | Sort-Object Name -Descending | Select-Object -First 1
if(!$opl3Sdk){throw 'Windows SDK not found'}
$opl3PriorPath=$env:PATH; $opl3PriorInclude=$env:INCLUDE; $opl3PriorLib=$env:LIB
try {
    $env:PATH="$($opl3Compiler.FullName)\bin\Hostx64\x86;"+$env:PATH
    $env:INCLUDE="$($opl3Compiler.FullName)\include;$($opl3Sdk.FullName)\ucrt;$($opl3Sdk.FullName)\shared;$($opl3Sdk.FullName)\um"
    $opl3SdkLib="C:\Program Files (x86)\Windows Kits\10\Lib\$($opl3Sdk.Name)"
    $env:LIB="$($opl3Compiler.FullName)\lib\x86;$opl3SdkLib\ucrt\x86;$opl3SdkLib\um\x86"
    Push-Location $opl3Output
    try {
        & cl.exe /nologo /LD /MT /EHsc /O2 /std:c++14 "/I$opl3Source" "$PSScriptRoot\ymfm_opl3_wrapper.cpp" "$opl3Source\ymfm_opl.cpp" "$opl3Source\ymfm_pcm.cpp" "$opl3Source\ymfm_adpcm.cpp" /link /OUT:ymfm_opl3.dll
        if($LASTEXITCODE -ne 0){throw 'OPL3 native build failed'}
    } finally {Pop-Location}
} finally {$env:PATH=$opl3PriorPath; $env:INCLUDE=$opl3PriorInclude; $env:LIB=$opl3PriorLib}
