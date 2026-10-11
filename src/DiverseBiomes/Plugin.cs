using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using crecheng.DSPModSave;
using HarmonyLib;

namespace DiverseBiomes
{
    [BepInPlugin(PluginGuid, "Diverse Biomes", PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [BepInIncompatibility("Zincon.DSPVegetationScale")]
    [BepInIncompatibility("Zincon.DSPPreserveVegetation")]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [BepInDependency("dsp.galactic-scale.2", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("cmx.BetterSolarSystem", BepInDependency.DependencyFlags.SoftDependency)]
    [ModSaveSettings(LoadOrder = LoadOrder.Preload)]
    public sealed class Plugin : BaseUnityPlugin, IModCanSave
    {
        // Keep the established ID so existing settings are read after the rename.
        public const string PluginGuid = "cmx.LivingWorlds";
        public const string PluginVersion = "2.4.0";
        private readonly List<Harmony> modules = new List<Harmony>();
        private void Awake()
        {
            var settings = ModConfig.Bind(Config);
            EcologyGeneration.Configure(Config); GroundCover.Configure(Config);
            WorldEcology.Configure(settings);
            Enable("SaveState", h => WorldEcology.Apply(h));
            Enable("Generation", h => VegetationGenerationPatches.Apply(h, settings));
            Enable("GalacticScale", h => EcologyAdapters.LoadOptional(h));
            Enable("Migration", h => ExistingVegetationMigration.Apply(h, settings, Config));
            Enable("Colliders", h => ColliderHooks.Apply(h));
            Enable("Preservation", h => PreservationHooks.Apply(h));
            Enable("PlanetInfo", h => PlanetInfoUiPatches.Apply(h, settings));
            Logger.LogInfo("Diverse Biomes " + PluginVersion + " initialized.");
        }
        private void Enable(string feature, Action<Harmony> install)
        {
            var module = new Harmony(PluginGuid + "." + feature);
            try { install(module); modules.Add(module); }
            catch (Exception failure) { module.UnpatchSelf(); Logger.LogError("Could not install " + feature + ": " + failure); }
        }
        private void OnDestroy() { foreach (var module in modules) module.UnpatchSelf(); modules.Clear(); }
        public void Export(BinaryWriter writer) => WorldEcology.Export(writer);
        public void Import(BinaryReader reader) => WorldEcology.Import(reader);
        public void IntoOtherSave() => WorldEcology.IntoOtherSave();
    }
}
