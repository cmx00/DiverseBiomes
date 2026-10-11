using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DiverseBiomes
{
    public sealed class OreCluster
    {
        public Vector3 position;
        public int count;
        public EVeinType type;
    }
    public static class EcologyGeneration
    {
        public const string Marker = "LivingWorlds.Generation.v1";
        private static ConfigEntry<bool> communities, oreTerrain;
        private static ConfigEntry<float> density, oreStrength;
        public sealed class GenerationPolicy
        {
            public readonly bool Communities;
            public readonly float Density, OreStrength;
            public readonly int Revision;
            public readonly float GrassShare, GrassWidth, GrassHeight;
            public GenerationPolicy(bool communities, float density, float oreStrength, int revision = 4,
                float grassShare = .08f, float grassWidth = 1.75f, float grassHeight = 1.45f)
            {
                Communities = communities; Density = density; OreStrength = oreStrength; Revision = revision;
                GrassShare = grassShare; GrassWidth = grassWidth; GrassHeight = grassHeight;
            }
        }
        public static string EncodePolicy(GenerationPolicy policy)
        {
            string text = "LW" + policy.Revision + ";" + (policy.Communities ? "1" : "0") + ";" +
                policy.Density.ToString("R", CultureInfo.InvariantCulture) + ";" + policy.OreStrength.ToString("R", CultureInfo.InvariantCulture);
            if (policy.Revision >= 3) text += ";" + policy.GrassShare.ToString("R",CultureInfo.InvariantCulture) + ";" +
                policy.GrassWidth.ToString("R",CultureInfo.InvariantCulture) + ";" + policy.GrassHeight.ToString("R",CultureInfo.InvariantCulture);
            return text;
        }
        public static GenerationPolicy DecodePolicy(string text)
        {
            if (text == null || !text.StartsWith("LW",StringComparison.Ordinal)) return null;
            string[] parts = text.Split(new[] { ';' }); float d, ore;
            int revision = parts[0] == "LW1" ? 1 : parts[0] == "LW2" ? 2 : parts[0] == "LW3" ? 3 : parts[0] == "LW4" ? 4 : 0;
            if (revision == 0 || parts.Length != (revision >= 3 ? 7 : 4) || (parts[1] != "0" && parts[1] != "1") ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out d) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out ore) ||
                float.IsNaN(d) || float.IsInfinity(d) || d < .4f || d > 1.5f ||
                float.IsNaN(ore) || float.IsInfinity(ore) || ore < 0f || ore > .5f)
                throw new InvalidOperationException("Invalid saved DiverseBiomes generation policy; refusing to regenerate a different surface.");
            float share=.08f,width=1.75f,height=1.45f;
            if (revision >= 3 && (!ReadRange(parts[4],0f,.30f,out share) || !ReadRange(parts[5],1f,2f,out width) || !ReadRange(parts[6],1f,1.6f,out height)))
                throw new InvalidOperationException("Invalid saved DiverseBiomes ground cover settings.");
            return new GenerationPolicy(parts[1] == "1", d, ore, revision,share,width,height);
        }
        private static bool ReadRange(string text,float min,float max,out float value) =>
            float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !float.IsNaN(value) && !float.IsInfinity(value) && value>=min && value<=max;
        private static GenerationPolicy SavedPolicy() => WorldEcology.ActivePolicy;
        internal static GenerationPolicy ConfiguredPolicy() => new GenerationPolicy(
            communities?.Value ?? true, density?.Value ?? 1f,
            (oreTerrain?.Value ?? true) ? oreStrength?.Value ?? .4f : 0f,
            4, GroundCover.LargeShare, GroundCover.WidthLimit, GroundCover.HeightLimit);
        public static void Configure(ConfigFile config)
        {
            const string section = "Ecology / 生态";
            const string next = "Captured into each newly generated galaxy; saved settings keep plant IDs and deposits stable. / 新星区创建时固定设置，读档保留植物编号和矿点。";
            communities = config.Bind(section, "Terrain communities / 地形群落", true, next);
            density = config.Bind(section, "Vegetation density / 植物密度", 1f, new ConfigDescription(next, new AcceptableValueRange<float>(.4f, 1.5f)));
            oreTerrain = config.Bind(section, "Ore terrain preference / 矿脉地形偏好", true, "Requires the optional Galactic Scale adapter. / 仅在 GS2 可选适配中生效。" + next);
            oreStrength = config.Bind(section, "Ore preference strength / 矿脉偏好强度", .40f, new ConfigDescription("0 keeps original positions; 40% of clusters attempt terrain refinement by default, at most 12m on radius 300. / 默认四成矿团尝试地形微调，300 星最大 12 米。" + next, new AcceptableValueRange<float>(0f, .5f)));
        }
        internal static GenerationPolicy SettingsFor(PlanetData planet) => EcologyAdapters.PolicyFor(planet);
        internal static bool UsesCommunities(PlanetData planet) => SettingsFor(planet)?.Communities == true;
        internal static ThemeEcology ThemeProfile(PlanetData planet) => EcologyAdapters.Current?.AppliesTo(planet) == true ? EcologyAdapters.Current.ThemeFor(planet) : ProfiledCommunities.ForVanilla(planet);
        internal static int DesignRevision => SavedPolicy()?.Revision ?? 0;
        internal static int RevisionFor(PlanetData planet) => SettingsFor(planet)?.Revision ?? 0;
        internal static GenerationPolicy GroundCoverPolicy => SavedPolicy();
        internal static CommunitySample CommunityAt(PlanetData planet, Vector3 pos)
        {
            var policy=SettingsFor(planet);
            if (policy?.Revision >= 3) {
                var profile=ThemeProfile(planet);
                return ProfiledCommunities.Choose(profile,SampleTerrain(planet,pos,profile:profile));
            }
            return CommunityDesign.Choose(SampleTerrain(planet,pos),policy?.Revision>=2);
        }

        // Only trees are ecological plants. Rocks, ice, effects and the landing capsule keep their original generation.
        internal static bool IsPlant(short protoId) => LDB.veges?.Select(protoId)?.Type == EVegeType.Tree;
        internal static void Prune(PlanetRawData raw, List<int> removed)
        {
            if (removed == null || removed.Count == 0 || raw?.vegePool == null) return;
            // Allocate all original IDs first, then thin without compacting the pool.
            // Other surviving plants retain their deterministic IDs and positions.
            foreach (int id in removed)
                if (id > 0 && id < Math.Min(raw.vegeCursor, raw.vegePool.Length)) raw.vegePool[id].id = 0;
            if (raw.vegeIds == null) return;
            for (int i = 0; i < raw.vegeIds.Length; i++) {
                int id = raw.vegeIds[i];
                if (id > 0 && (id >= raw.vegePool.Length || raw.vegePool[id].id != id)) raw.vegeIds[i] = 0;
            }
        }

        internal static TerrainTraits SampleTerrain(PlanetData planet, Vector3 pos, Func<Vector3, float> query = null, ThemeEcology profile = null)
        {
            Vector3 n = pos.normalized;
            float radius = planet.realRadius;
            Func<Vector3, float> height = query ?? planet.data.QueryHeight;
            float h = height(n);
            float delta = 2.5f / radius;
            Vector3 right = Vector3.Cross(n, Math.Abs(n.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 forward = Vector3.Cross(n, right).normalized;
            float dx = height((n + right * delta).normalized) - height((n - right * delta).normalized);
            float dy = height((n + forward * delta).normalized) - height((n - forward * delta).normalized);
            return new TerrainTraits {
                Elevation = h - (radius + planet.waterHeight), Slope = (float)Math.Sqrt(dx * dx + dy * dy) / 5f,
                Latitude = Math.Abs(n.y), HasWater = planet.waterItemId != 0,
                Cold = planet.type == EPlanetType.Ice || planet.temperatureBias < -1f,
                Dry = profile?.Organic == true ? false : planet.type == EPlanetType.Desert || planet.type == EPlanetType.Vocano || planet.waterItemId == 0,
                Patch = CommunityDesign.PatchNoise(n, planet.seed, Math.Max(3f, radius / 45f) * (profile?.Organic == true ? profile.PatchFrequency : 1f)),
                Moisture = CommunityDesign.PatchNoise(n, planet.seed ^ 0x4d6f6973, 3.5f),
                Detail = profile != null ? CommunityDesign.PatchNoise(n,planet.seed ^ 0x65636f33,Math.Max(7f,radius/17f)) : 0f
            };
        }

        internal static bool PreparePlant(PlanetData planet, ref VegeData vegetation, uint key, PlanetVegetationProfile baseProfile, out float multiplier)
        {
            bool grass = GroundCover.IsGrass(vegetation.protoId);
            if (vegetation.protoId >= 9999 || vegetation.protoId == 1044 || (!grass && !IsPlant(vegetation.protoId))) {
                multiplier = 1f;
                var decorationSettings = SettingsFor(planet);
                var decorationPolicy = decorationSettings;
                if (decorationPolicy?.Revision >= 4 && decorationPolicy.Communities &&
                    !ThemeProfile(planet).Organic && ProfiledCommunities.IsDecoration(vegetation.protoId)) {
                    var surface = CommunityAt(planet, vegetation.pos);
                    multiplier = surface.DecorationScale;
                    return CommunityDesign.Unit(key ^ 0xd729c865u) < Math.Min(1f, surface.Retention * decorationPolicy.Density);
                }
                return true;
            }
            multiplier = grass ? GroundCover.Sample(key) : VegetationDistribution.SampleMultiplier(baseProfile, key);
            var gs = SettingsFor(planet);
            if (gs == null || !gs.Communities) return true;
            var policy=SettingsFor(planet);
            if (policy.Revision >= 3) {
                var themeProfile=ThemeProfile(planet);
                if (!themeProfile.Organic) {
                    multiplier=1f;
                    if (!ProfiledCommunities.IsDecoration(vegetation.protoId)) return true;
                    var mineral=CommunityAt(planet,vegetation.pos);
                    multiplier=mineral.DecorationScale;
                    return CommunityDesign.Unit(key ^ 0xd729c865u)<Math.Min(1f,mineral.Retention*policy.Density);
                }
                if (vegetation.protoId>=9999 || (!IsPlant(vegetation.protoId) && !grass)) { multiplier=1f;return true; }
            } else if (!IsPlant(vegetation.protoId) && !(grass && policy.Revision>=2)) return true;
            var sample = CommunityAt(planet, vegetation.pos);
            // Always use three peaks for ecological trees; birth retains its existing configured peaks.
            PlanetVegetationProfile profile = baseProfile.Mode == VegetationSizeMode.TriplePeak ? baseProfile :
                VegetationDistribution.CreateProfileWithBirthOverride(planet.seed, planet.id, VegetationGenerationPatches.SettingsFor(planet), true);
            multiplier = grass ? (policy.Revision>=3 ? GroundCover.Sample(key,sample,policy.GrassShare) : GroundCover.Sample(key)) : CommunityDesign.SizeMultiplier(profile, sample, key);
            float retention = Math.Min(1f, sample.Retention * policy.Density);
            if (policy.Revision >= 3) {
                retention=grass ? Math.Min(1f,sample.GrassRetention*policy.Density) :
                    retention*ProfiledCommunities.SpeciesRetention(vegetation.protoId,sample);
            } else if (grass && policy.Revision >= 2) {
                // Meadows have abundant low ground cover, forests have a quieter understory.
                float cover = sample.Kind == CommunityKind.MeadowWoodland ? .90f :
                    sample.Kind == CommunityKind.RiparianForest ? .68f :
                    sample.Kind == CommunityKind.AncientGrove ? .48f :
                    sample.Kind == CommunityKind.HighlandScrub ? .36f : .26f;
                retention = Math.Min(1f, cover * policy.Density);
            }
            // Leave a clearing around the arrival point, while retaining nearby starting vegetation.
            float startDistance = (vegetation.pos.normalized - planet.birthPoint.normalized).magnitude * planet.realRadius;
            if (BirthVegetationProfile.IsBirth(planet) && startDistance < 18f) retention *= .55f;
            return CommunityDesign.Unit(key ^ 0xd729c865u) < retention;
        }

        internal static float TreeMultiplier(PlanetData planet, VegeData vegetation, uint key, PlanetVegetationProfile profile)
        {
            float result;
            PreparePlant(planet, ref vegetation, key, profile, out result);
            return result;
        }

        // Testable with an analytical surface; the game uses its already-generated height field.
        public static int AdjustClusters(PlanetData planet, IList<OreCluster> clusters, float strength, float padding, Func<Vector3, float> height = null, int revision = 1)
        {
            if (planet == null || clusters == null || strength <= 0f) return 0;
            strength = Math.Min(.5f, strength);
            float radius = planet.realRadius;
            int changed = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                var group = clusters[i];
                if (group.position == Vector3.zero) continue; // Failed placement stays failed, no new resource budget.
                Vector3 origin = group.position.normalized;
                if (BirthVegetationProfile.IsBirth(planet) &&
                    (i < 2 || (origin - planet.birthPoint.normalized).magnitude * radius < 45f)) continue;
                uint key = unchecked((uint)planet.seed ^ (uint)i * 0x9e3779b9u ^ (uint)group.type * 0x85ebca6bu);
                if (CommunityDesign.Unit(key) >= strength) continue;
                float bestScore = OreScore(group.type, SampleTerrain(planet, origin, height), revision);
                Vector3 best = origin;
                Vector3 tangent = Vector3.Cross(origin, Math.Abs(origin.y) < .9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(origin, tangent).normalized;
                float travel = (revision >= 2 ? Math.Min(12f, radius * .04f) : Math.Min(8f, radius * .035f)) / radius;
                int attempts = revision >= 2 ? 12 : 6;
                for (int k = 0; k < attempts; k++)
                {
                    double angle = Math.PI * 2 * (k / 6.0 + CommunityDesign.Unit(key ^ 0xa731b9edu));
                    float ring = k >= 6 ? .55f : 1f;
                    Vector3 candidate = (origin + travel * ring * (tangent * (float)Math.Cos(angle) + other * (float)Math.Sin(angle))).normalized;
                    var terrain = SampleTerrain(planet, candidate, height);
                    float minimumElevation = group.type == EVeinType.Oil ? .6f : .1f;
                    if (terrain.Elevation < minimumElevation || terrain.Slope > .65f) continue;
                    // Check the surrounding cluster footprint, not only its center.
                    float footprint = Math.Min(7f, 2.1f * (float)Math.Sqrt(Math.Max(1, group.count))) / radius;
                    bool dryFootprint = true;
                    foreach (Vector3 offset in new[] { tangent, -tangent, other, -other })
                        if (SampleTerrain(planet, (candidate + offset * footprint).normalized, height).Elevation < minimumElevation) { dryFootprint = false; break; }
                    if (!dryFootprint) continue;
                    if (BirthVegetationProfile.IsBirth(planet) && (candidate - planet.birthPoint.normalized).magnitude * radius < 45f) continue;
                    float clearance = (float)Math.Pow(2.1f / radius, 2) * Math.Max(100f, padding * 196f);
                    bool collision = false;
                    for (int j = 0; j < clusters.Count; j++)
                        if (j != i && clusters[j].position != Vector3.zero && (candidate - clusters[j].position.normalized).sqrMagnitude < clearance) { collision = true; break; }
                    if (collision) continue;
                    float score = OreScore(group.type, terrain, revision);
                    if (score > bestScore + .01f) { bestScore = score; best = candidate; }
                }
                if ((best - origin).sqrMagnitude > 1e-10f) { group.position = best; changed++; }
            }
            return changed;
        }
        public static float OreScore(EVeinType type, TerrainTraits terrain, int revision = 1)
        {
            if (revision >= 2) {
                float preferred = type == EVeinType.Oil ? 1.1f : type == EVeinType.Coal ? 1.8f :
                    type == EVeinType.Stone ? 3f : type == EVeinType.Titanium ? 4.5f :
                    type == EVeinType.Silicium ? 3.8f : 3.2f;
                float elevationWeight = type == EVeinType.Oil ? 1f : .55f;
                return -Math.Abs(terrain.Elevation - preferred) * elevationWeight - terrain.Slope * 3f;
            }
            if (type == EVeinType.Oil) return -Math.Abs(terrain.Elevation - 1.1f) - terrain.Slope * 3f;
            if (type == EVeinType.Coal) return -Math.Abs(terrain.Elevation - 1.8f) * .5f - terrain.Slope * 2f;
            // Exposed but buildable uplands; rare ores use the same mild preference.
            return Math.Min(terrain.Elevation, 5f) * .35f - terrain.Slope * 2f;
        }
    }
}
