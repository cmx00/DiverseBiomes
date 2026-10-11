using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DiverseBiomes
{
    internal static class GiantVegetationApplicator
    {
        private sealed class Stamp { internal VegeData[] Pool; }
        private static readonly ConditionalWeakTable<PlanetData, Stamp> completed = new ConditionalWeakTable<PlanetData, Stamp>();
        private static readonly object gate = new object();
        internal static bool WasProcessed(PlanetData planet)
        {
            if (planet == null) return false;
            lock (gate) { Stamp stamp; return completed.TryGetValue(planet, out stamp) && ReferenceEquals(stamp.Pool, planet.data?.vegePool); }
        }
        internal static int Apply(PlanetData planet, VegeData[] pool, int cursor, DistributionSettings settings)
        {
            if (planet == null || pool == null || settings == null) return 0;
            lock (gate) {
                Stamp stamp;
                if (completed.TryGetValue(planet, out stamp) && ReferenceEquals(stamp.Pool, pool)) return 0;
                completed.Remove(planet); completed.Add(planet, new Stamp { Pool = pool });
            }
            if (EcologyGeneration.RevisionFor(planet) > 0 && EcologyGeneration.RevisionFor(planet) < 4) return 0;
            if (planet.radius + .5f < WorldEcology.MinimumGiantRadius(planet) ||
                !VegetationDistribution.ShouldGenerateGiantVegetation(planet.seed, planet.id, settings)) return 0;
            int maximum = VegetationDistribution.GetGiantVegetationCount(planet.seed, planet.id, settings);
            if (maximum <= 0) return 0;
            // A bounded ordered reservoir gives stable choices without fixed-size
            // pools or scanning a second full list. The ID resolves score ties.
            var chosen = new SortedDictionary<ulong, int>();
            int end = Math.Min(cursor, pool.Length);
            for (int id = 1; id < end; id++) {
                var plant = pool[id];
                if (plant.id != id || !EcologyGeneration.IsPlant(plant.protoId) || GroundCover.IsGrass(plant.protoId)) continue;
                if (EcologyGeneration.UsesCommunities(planet) && EcologyGeneration.CommunityAt(planet, plant.pos).Kind != CommunityKind.AncientGrove) continue;
                uint key = VegetationDistribution.CreatePlantKey(planet.seed, planet.id, id, plant.protoId, plant.pos.x, plant.pos.y, plant.pos.z);
                ulong rank = ((ulong)VegetationDistribution.GetGiantSelectionScore(key) << 32) | (uint)id;
                chosen.Add(rank, id);
                if (chosen.Count > maximum) {
                    ulong last = 0; foreach (var item in chosen) last = item.Key;
                    chosen.Remove(last);
                }
            }
            var normal = BirthVegetationProfile.Create(planet, settings);
            foreach (int id in chosen.Values) {
                var plant = pool[id];
                uint key = VegetationDistribution.CreatePlantKey(planet.seed, planet.id, id, plant.protoId, plant.pos.x, plant.pos.y, plant.pos.z);
                float previous = EcologyGeneration.TreeMultiplier(planet, plant, key, normal);
                float desired = VegetationDistribution.SampleGiantMultiplier(key, settings);
                if (previous > 0) pool[id].scl *= desired / previous;
            }
            return chosen.Count;
        }
    }
}
