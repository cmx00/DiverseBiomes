using System;
using BepInEx.Configuration;
using UnityEngine;

namespace DiverseBiomes
{
    // Grass is ground cover, not a tree eligible for the large/giant tree peaks.
    internal static class GroundCover
    {
        private static ConfigEntry<float> largeShare, widthCap, heightCap;
        internal static float LargeShare => largeShare?.Value ?? .08f;
        internal static float WidthLimit => widthCap?.Value ?? 1.75f;
        internal static float HeightLimit => heightCap?.Value ?? 1.45f;
        internal static void Configure(ConfigFile config)
        {
            const string section = "Ground cover / 地被草";
            largeShare = config.Bind(section, "Large grass share / 大草比例", .08f,
                new ConfigDescription("Base share; LW3 meadows increase it, dense understory decreases it, up to 30%. / 基准比例；草甸提高、密林下层降低，上限三成。", new AcceptableValueRange<float>(0f, .30f)));
            widthCap = config.Bind(section, "Width limit / 宽度上限", 1.75f,
                new ConfigDescription("Relative to the prototype's original maximum; applies safely on loading existing factories too. / 相对原型正常尺寸上限，读旧档也限制，不累乘。", new AcceptableValueRange<float>(1f, 2f)));
            heightCap = config.Bind(section, "Height limit / 高度上限", 1.45f,
                new ConfigDescription("Keeps belts easier to see. / 降低草对传送带走向的遮挡。", new AcceptableValueRange<float>(1f, 1.6f)));
        }
        internal static bool IsGrass(short id)
        {
            // GS2's named grass prototypes; do not classify fragments, seaweed or effects as grass.
            return id >= 61 && id <= 63 || id >= 1001 && id <= 1004 ||
                id >= 1011 && id <= 1013 || id >= 1021 && id <= 1023 || id >= 1081 && id <= 1086 || id >= 1091 && id <= 1094;
        }
        internal static float Multiplier(uint key, float large = .04f)
        {
            float u = CommunityDesign.Unit(key ^ 0x318ead27u);
            float center = u < 1f - large - .22f ? 1f : u < 1f - large ? 1.18f : 1.48f;
            return Math.Max(1f, center * (.96f + .08f * CommunityDesign.Unit(key ^ 0xe7869af3u)));
        }
        internal static float Sample(uint key) => Multiplier(key, largeShare?.Value ?? .04f);
        internal static float Sample(uint key,CommunitySample sample,float basis)
        {
            float large=Math.Min(.30f,Math.Max(0f,basis*sample.GrassLargeFactor));
            float u=CommunityDesign.Unit(key ^ 0x318ead27u);
            float center=u<1f-large-.22f ? 1f : u<1f-large ? 1.22f : 1.68f;
            return Math.Max(1f,center*(.96f+.08f*CommunityDesign.Unit(key ^ 0xe7869af3u)));
        }
        internal static Vector3 Limit(Vector3 scale, Vector4 range, float width = 1.6f, float height = 1.25f)
        {
            float vertical = Math.Max(.1f, 1f + range.y);
            float horizontal = vertical * Math.Max(.1f, 1f + range.w);
            float factor = Math.Min(1f, Math.Min(horizontal * width / Math.Max(.0001f, Math.Max(Math.Abs(scale.x), Math.Abs(scale.z))),
                vertical * height / Math.Max(.0001f, Math.Abs(scale.y))));
            return scale * factor; // Preserve the model's proportions; never grow a saved plant.
        }
        internal static void Clamp(ref VegeData vegetation, EcologyGeneration.GenerationPolicy policy = null)
        {
            if (!IsGrass(vegetation.protoId)) return;
            var proto = LDB.veges?.Select(vegetation.protoId);
            var saved=policy ?? EcologyGeneration.GroundCoverPolicy;
            float width=saved?.Revision>=3 ? saved.GrassWidth : WidthLimit;
            float height=saved?.Revision>=3 ? saved.GrassHeight : HeightLimit;
            if (proto != null) vegetation.scl = Limit(vegetation.scl, proto.ScaleRange,width,height);
        }
        internal static void ClampFactory(PlanetFactory factory)
        {
            if (factory?.vegePool == null) return;
            for (int i = 1; i < Math.Min(factory.vegeCursor, factory.vegePool.Length); i++)
                if (factory.vegePool[i].id == i) Clamp(ref factory.vegePool[i], EcologyAdapters.PolicyFor(factory.planet));
        }
        internal static void SyncFactoryGrass(PlanetFactory factory)
        {
            var planet=factory?.planet;
            if (planet?.data?.vegePool==null || factory.vegePool==null || !GiantVegetationApplicator.WasProcessed(planet)) return;
            SyncMatchingGrass(factory.vegePool,factory.vegeCursor,planet.data.vegePool,planet.data.vegeCursor);
        }
        internal static int SyncMatchingGrass(VegeData[] saved,int savedCursor,VegeData[] fresh,int freshCursor)
        {
            if (saved==null || fresh==null || ReferenceEquals(saved,fresh)) return 0;
            int limit=Math.Min(Math.Min(savedCursor,saved.Length),Math.Min(freshCursor,fresh.Length)),count=0;
            for(int i=1;i<limit;i++) {
                var old=saved[i];var baseline=fresh[i];
                if(old.id!=i || baseline.id!=i || old.protoId!=baseline.protoId || !IsGrass(old.protoId) ||
                    (old.pos-baseline.pos).sqrMagnitude>.0001f) continue;
                // A fresh deterministic reference replaces scale, never multiplies saved scale.
                saved[i].scl=baseline.scl;count++;
            }
            return count;
        }
    }
}
