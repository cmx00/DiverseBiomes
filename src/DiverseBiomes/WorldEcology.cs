using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace DiverseBiomes
{
    public interface IEcologyAdapter
    {
        bool AppliesTo(PlanetData planet);
        ThemeEcology ThemeFor(PlanetData planet);
        EcologyGeneration.GenerationPolicy LegacyPolicy();
        void Attach(Harmony harmony);
    }

    public static class EcologyAdapters
    {
        internal static IEcologyAdapter Current;
        public static EcologyGeneration.GenerationPolicy PolicyFor(PlanetData planet) => WorldEcology.For(planet)?.Policy;
        public static void RegisterVegetationGenerator(Harmony harmony, Type algorithm) => VegetationGenerationPatches.Register(harmony, algorithm);
        internal static void LoadOptional(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey("dsp.galactic-scale.2")) return;
            string file = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "DiverseBiomes.GalacticScale.dll");
            var assembly = Assembly.LoadFrom(file);
            var adapter = (IEcologyAdapter)assembly.CreateInstance("DiverseBiomes.GalacticScale.Adapter");
            if (adapter == null) throw new TypeLoadException("The Galactic Scale adapter is unavailable.");
            adapter.Attach(harmony);
            Current = adapter;
        }
    }

    // A galaxy owns its generation settings. No process-global seed or migration
    // list is used to decide whether another save has already been converted.
    internal static class WorldEcology
    {
        internal sealed class Record
        {
            internal EcologyGeneration.GenerationPolicy Policy;
            internal DistributionSettings Sizes;
            internal float GiantMinimumRadius;
            internal long CreationTicks;
            internal int Seed, StarCount;
        }
        private static readonly object gate = new object();
        private static ConditionalWeakTable<GalaxyData, Record> worlds = new ConditionalWeakTable<GalaxyData, Record>();
        private static Record imported, active;
        private static DistributionSettings configured;
        private static int newGameDepth, loadDepth;
        private sealed class Context
        {
            internal int Depth;
            internal Record PreviousActive, PreviousImported;
        }
        internal static EcologyGeneration.GenerationPolicy ActivePolicy => active?.Policy;

        internal static void Configure(DistributionSettings sizes) { configured = sizes; }
        internal static Record For(PlanetData planet)
        {
            if (planet?.galaxy == null) return null;
            lock (gate) {
                Record record;
                return worlds.TryGetValue(planet.galaxy, out record) ? record : null;
            }
        }
        internal static DistributionSettings SizesFor(PlanetData planet) => For(planet)?.Sizes ?? configured ?? DistributionSettings.CreateDefaults();
        internal static float MinimumGiantRadius(PlanetData planet) => For(planet)?.GiantMinimumRadius ?? ModConfig.GiantMinimumPlanetRadius;

        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(GameData), "NewGame", new[] { typeof(GameDesc) }),
                prefix: Hook(nameof(BeginNew)), finalizer: Hook(nameof(EndNew)));
            harmony.Patch(AccessTools.Method(typeof(GameSave), "LoadCurrentGame", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(WorldEcology), nameof(BeginLoad)) { priority = Priority.First }, finalizer: Hook(nameof(EndLoad)));
            harmony.Patch(AccessTools.Method(typeof(GameSave), "LoadCurrentGameInResource", new[] { typeof(int) }),
                prefix: new HarmonyMethod(typeof(WorldEcology), nameof(BeginLoad)) { priority = Priority.First }, finalizer: Hook(nameof(EndLoad)));
            harmony.Patch(AccessTools.Method(typeof(UniverseGen), "CreateGalaxy", new[] { typeof(GameDesc) }),
                postfix: new HarmonyMethod(typeof(WorldEcology), nameof(GalaxyCreated)) { priority = Priority.Last });
        }
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(WorldEcology), name);
        private static void BeginNew(out Context __state)
        {
            __state = new Context { Depth = newGameDepth++, PreviousActive = active, PreviousImported = imported };
            if (__state.Depth == 0) { imported = null; active = null; }
        }
        private static Exception EndNew(Exception __exception, bool __result, Context __state)
        {
            newGameDepth = __state.Depth;
            if (__state.Depth == 0 && (__exception != null || !__result)) Restore(__state);
            return __exception;
        }
        private static void BeginLoad(out Context __state)
        {
            __state = new Context { Depth = loadDepth++, PreviousActive = active, PreviousImported = imported };
            if (__state.Depth == 0) { imported = null; active = null; }
        }
        private static Exception EndLoad(Exception __exception, bool __result, Context __state)
        {
            loadDepth = __state.Depth;
            if (__state.Depth == 0 && (__exception != null || !__result)) Restore(__state);
            return __exception;
        }
        private static void Restore(Context state)
        { active = state.PreviousActive; imported = state.PreviousImported; }
        private static void GalaxyCreated([HarmonyArgument(0)] GameDesc gameDesc, GalaxyData __result)
        {
            if (__result == null || gameDesc == null) return;
            Record record;
            if (loadDepth > 0) {
                record = imported;
                if (record != null) ValidateIdentity(record, gameDesc);
                else record = LegacyRecord();
            } else record = FreshRecord();
            Bind(__result, record, gameDesc);
            if (newGameDepth > 0 || loadDepth > 0) active = record;
        }
        internal static Record FreshRecord() => new Record {
            Policy = EcologyGeneration.ConfiguredPolicy(), Sizes = configured ?? DistributionSettings.CreateDefaults(),
            GiantMinimumRadius = ModConfig.GiantMinimumPlanetRadius
        };
        private static Record LegacyRecord() => new Record {
            Policy = EcologyAdapters.Current?.LegacyPolicy(), Sizes = configured ?? DistributionSettings.CreateDefaults(),
            GiantMinimumRadius = ModConfig.GiantMinimumPlanetRadius
        };
        internal static void Bind(GalaxyData galaxy, Record record, GameDesc desc)
        {
            record.CreationTicks = desc.creationTime.ToUniversalTime().Ticks; record.Seed = desc.galaxySeed; record.StarCount = desc.starCount;
            lock (gate) { worlds.Remove(galaxy); worlds.Add(galaxy, record); }
        }
        internal static void ValidateIdentity(Record record, GameDesc desc)
        {
            if (record.CreationTicks != desc.creationTime.ToUniversalTime().Ticks || record.Seed != desc.galaxySeed || record.StarCount != desc.starCount)
                throw new InvalidDataException("Diverse Biomes data belongs to a different save. Restore its matching .moddsv file.");
        }
        internal static void IntoOtherSave()
        {
            // DSPModSave also calls this at the end of NewGame. Keep the fresh
            // record already captured before any planet is modeled.
            if (newGameDepth > 0) return;
            imported = null; active = null;
        }
        internal static void Export(BinaryWriter writer) { WriteRecord(writer, active); }
        internal static void Import(BinaryReader reader)
        {
            imported = null; active = null; // Failed reads cannot inherit the previous save's policy.
            imported = ReadRecord(reader);
        }
        internal static void WriteRecord(BinaryWriter writer, Record record)
        {
            writer.Write(0x44424931); writer.Write(1); writer.Write(record != null);
            if (record == null) return;
            writer.Write(record.CreationTicks); writer.Write(record.Seed); writer.Write(record.StarCount);
            writer.Write(record.Policy != null ? EcologyGeneration.EncodePolicy(record.Policy) : "");
            foreach (float value in (record.Sizes ?? DistributionSettings.CreateDefaults()).ExportValues()) writer.Write(value);
            writer.Write(record.GiantMinimumRadius);
        }
        internal static Record ReadRecord(BinaryReader reader)
        {
            if (reader.ReadInt32() != 0x44424931 || reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported Diverse Biomes save data.");
            if (!reader.ReadBoolean()) return null;
            var record = new Record { CreationTicks = reader.ReadInt64(), Seed = reader.ReadInt32(), StarCount = reader.ReadInt32() };
            string policy = reader.ReadString();
            record.Policy = policy.Length == 0 ? null : EcologyGeneration.DecodePolicy(policy);
            if (policy.Length != 0 && record.Policy == null) throw new InvalidDataException("Invalid Diverse Biomes policy.");
            float[] values = new float[15];
            for (int i = 0; i < values.Length; i++) {
                values[i] = reader.ReadSingle();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i] < 0 || values[i] > 100)
                    throw new InvalidDataException("Invalid saved plant sizes.");
            }
            record.Sizes = new DistributionSettings(values);
            record.GiantMinimumRadius = reader.ReadSingle();
            if (record.StarCount <= 0 || record.CreationTicks <= 0 || float.IsNaN(record.GiantMinimumRadius) ||
                float.IsInfinity(record.GiantMinimumRadius) || record.GiantMinimumRadius < 0 || record.GiantMinimumRadius > 10000)
                throw new InvalidDataException("Invalid Diverse Biomes world settings.");
            return record;
        }
    }
}
