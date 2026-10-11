param([string]$GameDir, [string]$BepInExDir, [string]$GalacticScaleDir, [string]$ArtifactDir, [string]$DSPModSaveDir)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'DSPGAME_Data/Managed'
$core = Join-Path $BepInExDir 'core'
foreach ($name in @('UnityEngine.CoreModule.dll', 'UnityEngine.dll', 'Assembly-CSharp.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $managed $name)) }
foreach ($name in @('BepInEx.dll', '0Harmony.dll', 'Mono.Cecil.dll', 'MonoMod.Utils.dll', 'MonoMod.RuntimeDetour.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $core $name)) }
if (!$DSPModSaveDir) { $DSPModSaveDir = Join-Path $BepInExDir 'plugins/CommonAPI-DSPModSave' }
[void][Reflection.Assembly]::LoadFrom((Join-Path $DSPModSaveDir 'DSPModSave.dll'))
$lw = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $ArtifactDir 'DiverseBiomes.dll')))
$test = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $ArtifactDir 'DiverseBiomes.Checks.dll')))
try { $test.GetType('DiverseBiomes.ReleaseChecks').GetMethod('Run').Invoke($null, @()) }
catch { throw $_.Exception.GetBaseException() }
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$hook = $lw.GetType('DiverseBiomes.VegetationGenerationPatches').GetMethod('Decorate', $flags)
$harmony = [HarmonyLib.Harmony]::new('cmx.release.vegetation.check')
try {
    [void]$harmony.Patch([PlanetRawData].GetMethod('AddVegeData'), [HarmonyLib.HarmonyMethod]::new($hook))
    'PASS: actual AddVegeData prefix installation against current game assemblies.'
} finally { $harmony.UnpatchSelf() }
$field = [HarmonyLib.AccessTools]::Field([PlanetAlgorithm], 'planet')
if ($field.FieldType -ne [PlanetData]) { throw 'Generation context field changed.' }
$model = [HarmonyLib.AccessTools]::Method([PlanetModelingManager], 'LoadingPlanetFactoryMain', [type[]]@([PlanetData]))
if ($null -eq $model -or [HarmonyLib.AccessTools]::Field([PlanetModelingManager], 'currentFactingStage').FieldType -ne [int]) { throw 'Factory loading contract changed.' }
'PASS: generation context and pre-model factory reconciliation signatures.'

if (@($lw.GetReferencedAssemblies() | Where-Object Name -match 'GalacticScale|GSUI').Count) { throw 'Core still requires GS2.' }
if (@([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -match 'GalacticScale|GSUI' }).Count) { throw 'Standalone checks unexpectedly loaded GS2.' }
[void]$lw.GetTypes()
'PASS: all core types load with Galactic Scale and BetterSolarSystem absent.'
$world = $lw.GetType('DiverseBiomes.WorldEcology')
function Test-HookGroup([string]$name, [scriptblock]$install) {
    $module = [HarmonyLib.Harmony]::new('cmx.release.' + $name + '.check')
    try { & $install $module }
    catch {
        $cause = $_.Exception.GetBaseException()
        if ($cause -is [System.Security.SecurityException] -and $cause.Message -like '*ECall methods*') {
            "LIMIT: $name hook execution requires Unity's runtime; current signatures are checked separately."
        } else { throw $cause }
    }
    finally {
        try { $module.UnpatchSelf() }
        catch {
            $cause = $_.Exception.GetBaseException()
            if (!($cause -is [System.Security.SecurityException] -and $cause.Message -like '*ECall methods*')) { throw $cause }
        }
    }
}
foreach ($target in @(
    @([GameData], 'NewGame', [type[]]@([GameDesc])),
    @([GameSave], 'LoadCurrentGame', [type[]]@([string])),
    @([GameSave], 'LoadCurrentGameInResource', [type[]]@([int])),
    @([UniverseGen], 'CreateGalaxy', [type[]]@([GameDesc])))) {
    if ($null -eq [HarmonyLib.AccessTools]::Method($target[0], $target[1], $target[2])) { throw 'World save/generation contract changed.' }
}
'PASS: current new-game/load/galaxy API contracts.'
Test-HookGroup 'lifecycle' {
    param($lifecycle)
    [void]$world.GetMethod('Apply', $flags).Invoke($null, @($lifecycle))
    'PASS: actual new-game/load/galaxy hook installation.'
}
Test-HookGroup 'generation' {
    param($generation)
    $settings = $lw.GetType('DiverseBiomes.DistributionSettings').GetMethod('CreateDefaults', $flags).Invoke($null, @())
    $count = $lw.GetType('DiverseBiomes.VegetationGenerationPatches').GetMethod('Apply', $flags).Invoke($null, @($generation, $settings))
    if ($count -lt 1) { throw 'No vanilla vegetation generator was patched.' }
    "PASS: actual vanilla vegetation generator hooks ($count entry points)."
}
if ($GalacticScaleDir) {
    foreach ($name in @('GSUI.dll', 'GalacticScale.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $GalacticScaleDir $name)) }
    $adapter = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $ArtifactDir 'DiverseBiomes.GalacticScale.dll')))
    $instance = $adapter.CreateInstance('DiverseBiomes.GalacticScale.Adapter')
    if (!$lw.GetType('DiverseBiomes.IEcologyAdapter').IsInstanceOfType($instance)) { throw 'Optional adapter contract differs from core.' }
    $gsMethod = [HarmonyLib.AccessTools]::Method([GalacticScale.GS2PlanetAlgorithm], 'GenerateVegetables')
    if ($null -eq $gsMethod -or [HarmonyLib.AccessTools]::Field([GalacticScale.GS2PlanetAlgorithm], 'planet').FieldType -ne [PlanetData]) { throw 'GS2 vegetation contract changed.' }
    'PASS: optional GS2 adapter loads after standalone core checks.'
    Test-HookGroup 'integration' {
        param($integration)
        [void]$instance.Attach($integration)
        'PASS: actual optional GS2 vegetation and vein-position hook installation.'
    }
}
