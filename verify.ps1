#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$BepInExDir,
    [string]$GalacticScaleDir,
    [string]$DSPModSaveDir
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -BepInExDir $BepInExDir -GalacticScaleDir $GalacticScaleDir -DSPModSaveDir $DSPModSaveDir
$references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $outputDir 'DiverseBiomes.dll')))
$trees.Clear()
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs') {
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($source.FullName)))
}
$checks = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('DiverseBiomes.Checks', $trees, $references, $options)
$stream = [IO.MemoryStream]::new()
$result = $checks.Emit($stream)
if (!$result.Success) { $result.Diagnostics | ForEach-Object { $_.ToString() }; throw 'Checks did not compile.' }
$checkPath = Join-Path $outputDir 'DiverseBiomes.Checks.dll'
[IO.File]::WriteAllBytes($checkPath, $stream.ToArray())
$stream.Dispose()
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests/run-native.ps1') -GameDir $GameDir -BepInExDir $BepInExDir -GalacticScaleDir $GalacticScaleDir -DSPModSaveDir $DSPModSaveDir -ArtifactDir $outputDir
if ($LASTEXITCODE -ne 0) { throw 'Native release checks failed.' }
