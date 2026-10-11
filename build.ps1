#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$BepInExDir,
    [string]$GalacticScaleDir,
    [string]$DSPModSaveDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Use the installed game's reference assemblies, not the PowerShell runtime's.
$managedDir = Join-Path $GameDir 'DSPGAME_Data/Managed'
$coreDir = Join-Path $BepInExDir 'core'
if (-not $DSPModSaveDir) {
    $DSPModSaveDir = Join-Path $BepInExDir 'plugins/CommonAPI-DSPModSave'
}
$outputDir = Join-Path $PSScriptRoot 'artifacts'
$referencePaths = @(
    (Join-Path $managedDir 'mscorlib.dll'),
    (Join-Path $managedDir 'netstandard.dll'),
    (Join-Path $managedDir 'System.dll'),
    (Join-Path $managedDir 'System.Core.dll'),
    (Join-Path $managedDir 'Assembly-CSharp.dll'),
    (Join-Path $managedDir 'UnityEngine.dll'),
    (Join-Path $managedDir 'UnityEngine.CoreModule.dll'),
    (Join-Path $coreDir 'BepInEx.dll'),
    (Join-Path $coreDir '0Harmony.dll'),
    (Join-Path $DSPModSaveDir 'DSPModSave.dll')
)
$compilerPaths = @(
    (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll'),
    (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
)
foreach ($path in $referencePaths + $compilerPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required assembly not found: $path"
    }
}
Add-Type -Path $compilerPaths

$references = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach ($path in $referencePaths) {
    $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($path))
}
$trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
$sourceDir = Join-Path $PSScriptRoot 'src/DiverseBiomes'
foreach ($file in Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' -File -Recurse | Sort-Object FullName) {
    $relativePath = [System.IO.Path]::GetRelativePath($PSScriptRoot, $file.FullName).Replace('\', '/')
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [System.IO.File]::ReadAllText($file.FullName), $null, $relativePath))
}
if ($trees.Count -eq 0) { throw "No C# source files found: $sourceDir" }
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
    [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary
).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
    'DiverseBiomes', $trees, $references, $options)
$stream = [System.IO.MemoryStream]::new()
try {
    $result = $compilation.Emit($stream)
    if (-not $result.Success) {
        $result.Diagnostics | Where-Object Severity -eq Error | ForEach-Object { Write-Host $_.ToString() }
        throw 'Compilation failed: DiverseBiomes'
    }
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    $target = Join-Path $outputDir 'DiverseBiomes.dll'
    [System.IO.File]::WriteAllBytes($target, $stream.ToArray())
    Write-Host "Built $target ($((Get-Item -LiteralPath $target).Length) bytes)"
} finally {
    $stream.Dispose()
}

# Optional adapter is compiled separately. The core above never references GS2.
if ($GalacticScaleDir) {
    $gsReference = Join-Path $GalacticScaleDir 'GalacticScale.dll'
    if (!(Test-Path -LiteralPath $gsReference)) { throw "Missing optional GS2 reference: $gsReference" }
    $adapterRefs = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new($references)
    $adapterRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($gsReference))
    $adapterRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $outputDir 'DiverseBiomes.dll')))
    $adapterTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src/DiverseBiomes.GalacticScale') -Filter '*.cs' -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($PSScriptRoot,$file.FullName).Replace('\','/')
        $adapterTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName),$null,$relative))
    }
    $adapter = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('DiverseBiomes.GalacticScale',$adapterTrees,$adapterRefs,$options)
    $adapterStream = [IO.MemoryStream]::new()
    try {
        $result = $adapter.Emit($adapterStream)
        if (!$result.Success) { $result.Diagnostics | Where-Object Severity -eq Error | ForEach-Object { $_.ToString() }; throw 'Optional adapter did not compile.' }
        $target = Join-Path $outputDir 'DiverseBiomes.GalacticScale.dll'
        [IO.File]::WriteAllBytes($target,$adapterStream.ToArray())
        Write-Host "Built optional adapter $target ($((Get-Item -LiteralPath $target).Length) bytes)"
    } finally { $adapterStream.Dispose() }
}
