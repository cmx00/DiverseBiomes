using System;
using UnityEngine;

namespace DiverseBiomes
{
    public enum CommunityKind { RiparianForest, MeadowWoodland, HighlandScrub, PolarTundra, AridScrub, AncientGrove }

    public struct TerrainTraits
    {
        public float Elevation, Slope, Latitude, Patch, Moisture, Detail;
        public bool HasWater, Cold, Dry;
    }

    public struct CommunitySample
    {
        public CommunityKind Kind;
        public float Retention, SmallShare, MediumShare;
        public int Revision;
        public string BiomeName;
        public float TreeScale, GrassRetention, GrassLargeFactor, DecorationScale;
        public CommunitySample(CommunityKind kind, float retention, float small, float medium, int revision = 1)
        {
            Kind = kind; Retention = retention; SmallShare = small; MediumShare = medium; Revision = revision;
            BiomeName = null; TreeScale = 1f; GrassRetention = -1f; GrassLargeFactor = 1f; DecorationScale = 1f;
        }
    }

    // Ecological preferences, not a climate simulation. All randomness is a pure
    // function of seed and position, independent of the game's generation RNG.
    public static class CommunityDesign
    {
        public static CommunitySample Choose(TerrainTraits t, bool refined = false)
        {
            if (refined) return Refined(t);
            if (t.Cold || t.Latitude > .80f)
                return new CommunitySample(CommunityKind.PolarTundra, .26f + .25f * t.Patch, .82f, .16f);
            if (t.Slope > .48f || t.Elevation > 4.2f)
                return new CommunitySample(CommunityKind.HighlandScrub, .30f + .30f * t.Patch, .68f, .28f);
            if (t.Dry && t.Moisture < .66f)
                return new CommunitySample(CommunityKind.AridScrub, .24f + .30f * t.Patch, .74f, .23f);
            if (t.HasWater && t.Elevation < 1.6f && t.Slope < .20f)
                return new CommunitySample(CommunityKind.RiparianForest, .84f + .14f * t.Patch, .12f, .52f);
            if (t.Patch > .62f && t.Moisture > .55f && t.Slope < .22f)
                return new CommunitySample(CommunityKind.AncientGrove, .88f, .10f, .32f);
            return new CommunitySample(CommunityKind.MeadowWoodland, .35f + .48f * t.Patch, .34f, .52f);
        }

        private static CommunitySample Refined(TerrainTraits t)
        {
            if (t.Cold || t.Latitude > .78f)
                return new CommunitySample(CommunityKind.PolarTundra, .16f + .20f * t.Patch, .94f, .055f, 2);
            if (t.Slope > .38f || t.Elevation > 5f)
                return new CommunitySample(CommunityKind.HighlandScrub, .20f + .22f * t.Patch, .86f, .13f, 2);
            if (t.Dry && t.Moisture < .70f)
                return new CommunitySample(CommunityKind.AridScrub, .16f + .24f * t.Patch, .90f, .09f, 2);
            if (t.HasWater && t.Elevation < 1.65f && t.Slope < .18f)
                return new CommunitySample(CommunityKind.RiparianForest, .86f + .12f * t.Patch, .08f, .62f, 2);
            if (t.Patch > .58f && t.Moisture > .48f && t.Slope < .18f)
                return new CommunitySample(CommunityKind.AncientGrove, .94f, .04f, .20f, 2);
            // Broad open meadows transition into small woodland patches.
            float cover = Math.Max(0f, Math.Min(1f, (t.Patch - .28f) / .40f));
            cover = cover * cover * (3f - 2f * cover);
            return new CommunitySample(CommunityKind.MeadowWoodland, .22f + .48f * cover, .54f, .42f, 2);
        }

        internal static float SizeMultiplier(PlanetVegetationProfile profile, CommunitySample sample, uint key)
        {
            float u = Unit(key ^ 0x7e119d23u);
            int peak = u < sample.SmallShare ? 0 : (u < sample.SmallShare + sample.MediumShare ? 1 : 2);
            float center = peak == 0 ? profile.FirstPeak : peak == 1 ? profile.SecondPeak : profile.ThirdPeak;
            if (sample.Revision == 2) {
                float stature = sample.Kind == CommunityKind.AncientGrove ? 1.08f :
                    sample.Kind == CommunityKind.PolarTundra ? .88f :
                    sample.Kind == CommunityKind.HighlandScrub || sample.Kind == CommunityKind.AridScrub ? .92f : 1f;
                center *= stature;
            }
            if (sample.Revision >= 3) center *= sample.TreeScale;
            // A narrow spread around each configured peak keeps the three modes visible.
            return Math.Max(1f, Math.Min(DistributionSettings.HardMaximumMultiplier, center * (.94f + .12f * Unit(key ^ 0x184bd38fu))));
        }

        public static float Unit(uint x)
        {
            unchecked { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; }
            return (x & 0xffffffu) / 16777216f;
        }

        public static float PatchNoise(Vector3 direction, int seed, float frequency)
        {
            double x = direction.x * frequency, y = direction.y * frequency, z = direction.z * frequency;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
            float fx = Smooth((float)(x - ix)), fy = Smooth((float)(y - iy)), fz = Smooth((float)(z - iz));
            float a = Lerp(Hash(ix, iy, iz, seed), Hash(ix + 1, iy, iz, seed), fx);
            float b = Lerp(Hash(ix, iy + 1, iz, seed), Hash(ix + 1, iy + 1, iz, seed), fx);
            float c = Lerp(Hash(ix, iy, iz + 1, seed), Hash(ix + 1, iy, iz + 1, seed), fx);
            float d = Lerp(Hash(ix, iy + 1, iz + 1, seed), Hash(ix + 1, iy + 1, iz + 1, seed), fx);
            return Lerp(Lerp(a, b, fy), Lerp(c, d, fy), fz);
        }
        private static float Hash(int x, int y, int z, int seed)
        { unchecked { return Unit((uint)seed ^ (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u); } }
        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
