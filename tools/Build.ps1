param([string]$MSBuild, [string]$Output)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $MSBuild){
 $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
 if(-not(Test-Path -LiteralPath $vswhere)){throw 'Install Visual Studio Build Tools with .NET desktop build tools, or pass -MSBuild.'}
 $MSBuild=@(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe') | Select-Object -First 1
}
if(-not $MSBuild -or -not(Test-Path -LiteralPath $MSBuild)){throw 'MSBuild executable not found.'}
if(-not $Output){$Output=Join-Path $repo 'artifacts\managed'}
$Output=[IO.Path]::GetFullPath($Output).TrimEnd('\')+'\'
& $MSBuild (Join-Path $repo 'src\ZXVM.sln') /t:Rebuild /m:2 /p:Configuration=Release '/p:Platform=Any CPU' "/p:OutDir=$Output" /nologo
if($LASTEXITCODE -ne 0){throw "MSBuild failed: $LASTEXITCODE"}
Write-Output "ZXMAK2-Redux managed build: $Output"
