using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace DiverseBiomes
{
    internal static class ExistingVegetationMigration
    {
        // New implementation reconciles against a freshly modeled reference.
        // It never infers an old multiplier or uses a cross-save migration list.
        internal static void Apply(Harmony harmony, DistributionSettings settings, ConfigFile config)
        {
            var model = AccessTools.Method(typeof(PlanetModelingManager), "LoadingPlanetFactoryMain", new[] { typeof(PlanetData) });
            var stage = AccessTools.Field(typeof(PlanetModelingManager), "currentFactingStage");
            if (model == null || stage?.FieldType != typeof(int)) throw new MissingMethodException("Planet factory loading stages changed.");
            harmony.Patch(model, prefix: new HarmonyMethod(typeof(ExistingVegetationMigration), nameof(BeforeModels)));
        }
        private static void BeforeModels(PlanetData planet, int ___currentFactingStage)
        {
            if (___currentFactingStage != 1 || planet?.factory?.vegePool == null) return;
            var factory = planet.factory;
            if ((EcologyGeneration.RevisionFor(planet) == 0 || EcologyGeneration.RevisionFor(planet) >= 4) && GiantVegetationApplicator.WasProcessed(planet))
                SyncMatchingPlants(factory.vegePool, factory.vegeCursor, planet.data.vegePool, planet.data.vegeCursor);
            GroundCover.ClampFactory(factory);
        }
        internal static int SyncMatchingPlants(VegeData[] saved, int savedCursor, VegeData[] reference, int referenceCursor)
        {
            if (saved == null || reference == null || ReferenceEquals(saved, reference)) return 0;
            int changed = 0, end = Math.Min(Math.Min(savedCursor, saved.Length), Math.Min(referenceCursor, reference.Length));
            for (int slot = 1; slot < end; slot++) {
                VegeData a = saved[slot], b = reference[slot];
                if (a.id != slot || b.id != slot || a.protoId != b.protoId || a.protoId >= 9999 ||
                    (a.pos - b.pos).sqrMagnitude > .0001f ||
                    (!GroundCover.IsGrass(a.protoId) && !EcologyGeneration.IsPlant(a.protoId))) continue;
                saved[slot].scl = b.scl; changed++;
            }
            return changed;
        }
    }
}
