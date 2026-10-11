using System;

namespace DiverseBiomes
{
    internal static class BirthVegetationProfile
    {
        internal static bool IsBirth(PlanetData planet)
        {
            return planet != null && planet.id > 0 && planet.galaxy != null && planet.galaxy.birthPlanetId == planet.id;
        }

        internal static PlanetVegetationProfile Create(PlanetData planet, DistributionSettings settings)
        {
            return VegetationDistribution.CreateProfileWithBirthOverride(planet.seed, planet.id, settings, IsBirth(planet));
        }

        // The fresh terrain pool already contains the new deterministic distribution.
        // Copy SCALE ONLY for matching surviving natural plants. No accumulated
        // multipliers, cross-save config marker, respawning or guessed old scale.
        internal static bool SyncFactory(PlanetFactory factory)
        {
            var planet = factory?.planet;
            if (!IsBirth(planet) || !GiantVegetationApplicator.WasProcessed(planet) || planet.data?.vegePool == null || factory.vegePool == null) return false;
            var source = planet.data.vegePool;
            if (ReferenceEquals(source, factory.vegePool)) return true;
            for (int i = 1; i < Math.Min(factory.vegeCursor, factory.vegePool.Length); i++)
            {
                if (i >= Math.Min(planet.data.vegeCursor, source.Length)) break;
                var saved = factory.vegePool[i]; var fresh = source[i];
                if (saved.id != i || fresh.id != i || saved.protoId != fresh.protoId || (saved.pos - fresh.pos).sqrMagnitude > .0001f) continue;
                factory.vegePool[i].scl = fresh.scl;
            }
            return true;
        }
    }
}
