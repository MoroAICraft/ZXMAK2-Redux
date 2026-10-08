param(
    [string]$Original = 'D:/release82_sound_fix',
    [string]$Destination = 'D:/release82_BC058_NMI',
    [string]$WorkDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Original = [IO.Path]::GetFullPath($Original)
$Destination = [IO.Path]::GetFullPath($Destination)
if (!$WorkDirectory) { $WorkDirectory = Join-Path $root 'src/_binrelease/v82-nmi-build' }
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
if (!(Test-Path -LiteralPath (Join-Path $Original 'ZXMAK2.Host.WinForms.dll'))) { throw 'Original v82 frontend missing' }
if ($Destination.StartsWith($Original, [StringComparison]::OrdinalIgnoreCase) -or $WorkDirectory.StartsWith($Original, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the original emulator' }
if (Test-Path -LiteralPath $Destination) { throw 'Use a new destination directory; existing emulator/configuration will not be overwritten' }
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $Original 'ZXMAK2.Host.WinForms.dll'))
$options = [Collections.Generic.List[string]]::new()
$options.AddRange([string[]]@('/nologo','/target:library','/unsafe','/optimize+','/platform:anycpu',('/out:"'+(Join-Path $WorkDirectory 'ZXMAK2.Host.WinForms.dll')+'"')))
foreach($reference in @('System','System.Data','System.Drawing','System.Management','System.Windows.Forms','System.Xml')) { $options.Add('/r:'+ $reference +'.dll') }
foreach($dll in Get-ChildItem -LiteralPath $Original -Filter 'ZXMAK2*.dll' -File) {
    if ($dll.Name -ne 'ZXMAK2.Host.WinForms.dll') { $options.Add('/r:"'+$dll.FullName+'"') }
}
foreach($name in $assembly.GetManifestResourceNames()) {
    $stream = $assembly.GetManifestResourceStream($name)
    $output = [IO.File]::Create((Join-Path $WorkDirectory $name))
    try { $stream.CopyTo($output) } finally { $output.Dispose(); $stream.Dispose() }
    $options.Add('/resource:"'+(Join-Path $WorkDirectory $name)+'",'+$name)
}
$projectDir = Join-Path $root 'src/ZXMAK2.Host.WinForms'
[xml]$project = Get-Content -LiteralPath (Join-Path $projectDir 'ZXMAK2.Host.WinForms.csproj') -Raw
foreach($node in $project.SelectNodes("//*[local-name()='Compile']")) {
    $relative = $node.Include
    $path = Join-Path $projectDir $relative
    if ($relative -in @('Views\Configuration\Devices\CtlSettingsUla.cs','Views\Configuration\Devices\CtlSettingsUla.Designer.cs')) {
        # These are the only frontend files changed by v83's PSG settings.
        # Build against v82 hardware; do not introduce the v83 audio controls.
        $path = Join-Path $WorkDirectory ([IO.Path]::GetFileName($relative))
        $gitPath = 'src/ZXMAK2.Host.WinForms/'+$relative.Replace('\','/')
        $content = & git -C $root show ('70f2e0c:'+$gitPath)
        if($LASTEXITCODE -ne 0) { throw 'Pinned v82 sources unavailable' }
        [IO.File]::WriteAllText($path, ($content -join "`r`n"), [Text.UTF8Encoding]::new($false))
    }
    $options.Add('"'+$path+'"')
}
$rsp = Join-Path $WorkDirectory 'build.rsp'
[IO.File]::WriteAllLines($rsp,$options,[Text.UTF8Encoding]::new($false))
& C:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe ('@'+$rsp)
if ($LASTEXITCODE -ne 0) { throw 'v82 frontend compilation failed' }
New-Item -ItemType Directory -Path $Destination | Out-Null
Get-ChildItem -LiteralPath $Original -Force | Copy-Item -Destination $Destination -Recurse
Copy-Item -LiteralPath (Join-Path $WorkDirectory 'ZXMAK2.Host.WinForms.dll') -Destination $Destination -Force
$manifest = foreach($file in Get-ChildItem -LiteralPath $Original -Recurse -File) {
    $name = $file.FullName.Substring($Original.TrimEnd('\').Length+1)
    $before = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $after = (Get-FileHash -LiteralPath (Join-Path $Destination $name) -Algorithm SHA256).Hash
    if($name -ne 'ZXMAK2.Host.WinForms.dll' -and $before -ne $after) { throw "Unexpected v82 difference: $name" }
    [ordered]@{name=$name;original=$before;candidate=$after}
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $WorkDirectory 'manifest.json') -Encoding utf8
Write-Host "READY: $Destination; only WinForms frontend changed, original remains intact"
