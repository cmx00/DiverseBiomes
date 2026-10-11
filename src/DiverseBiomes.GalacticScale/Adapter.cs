using System;
using System.Collections.Generic;
using GalacticScale;
using HarmonyLib;

namespace DiverseBiomes.GalacticScale
{
    // This assembly is loaded only when Galactic Scale is present. The core has
    // no reference to it or to any GS2 class.
    public sealed class Adapter : IEcologyAdapter
    {
        private static readonly Dictionary<GSTheme, ThemeEcology> profiles = new Dictionary<GSTheme, ThemeEcology>();
        public bool AppliesTo(PlanetData planet) => planet?.galaxy != null && !GS2.Vanilla && GS2.GetGSPlanet(planet) != null;
        public EcologyGeneration.GenerationPolicy LegacyPolicy()
        {
            if (GS2.Vanilla || GSSettings.ThemeLibrary == null || !GSSettings.ThemeLibrary.ContainsKey(EcologyGeneration.Marker)) return null;
            return EcologyGeneration.DecodePolicy(GSSettings.ThemeLibrary[EcologyGeneration.Marker].BriefIntroduction);
        }
        public ThemeEcology ThemeFor(PlanetData planet)
        {
            var theme = GS2.GetGSPlanet(planet)?.GsTheme;
            if (theme == null) return ProfiledCommunities.Mineral;
            lock (profiles) {
                ThemeEcology profile;
                if (profiles.TryGetValue(theme, out profile)) return profile;
                profile = Resolve(theme);
                string parent = theme.BaseName;
                for (int depth = 0; depth < 8 && (profile.Key == "Mineral" || profile.Key == "Organic") && !string.IsNullOrEmpty(parent); depth++) {
                    if (GSSettings.ThemeLibrary == null || !GSSettings.ThemeLibrary.ContainsKey(parent)) break;
                    var ancestor = GSSettings.ThemeLibrary[parent];
                    var inherited = Resolve(ancestor);
                    if (inherited.Key != "Mineral" || profile.Key == "Mineral") profile = inherited;
                    if (ancestor.BaseName == parent) break;
                    parent = ancestor.BaseName;
                }
                if (profiles.Count > 256) profiles.Clear();
                profiles[theme] = profile; return profile;
            }
        }
        private static ThemeEcology Resolve(GSTheme theme) => ProfiledCommunities.Resolve(theme.Name, theme.BaseName,
            ProfiledCommunities.HasLivingModels(new[] { theme.Vegetables0, theme.Vegetables1, theme.Vegetables2,
                theme.Vegetables3, theme.Vegetables4, theme.Vegetables5 }));
        public void Attach(Harmony harmony)
        {
            EcologyAdapters.RegisterVegetationGenerator(harmony, typeof(GS2PlanetAlgorithm));
            var vectors = AccessTools.Method(typeof(VeinAlgorithms), "CalculateVectorsGS2");
            if (vectors == null) throw new MissingMethodException("GS2 vein-vector generation is unavailable.");
            harmony.Patch(vectors, postfix: new HarmonyMethod(typeof(Adapter), nameof(PreferOreTerrain)));
        }
        private static void PreferOreTerrain(GSPlanet gsPlanet, List<GSVeinDescriptor> __result, bool sketchOnly)
        {
            var planet = gsPlanet?.planetData;
            var policy = EcologyAdapters.PolicyFor(planet);
            if (sketchOnly || __result == null || planet?.data == null || policy == null || policy.Revision < 4 || policy.OreStrength <= 0) return;
            var clusters = new List<OreCluster>(__result.Count);
            foreach (var descriptor in __result) clusters.Add(new OreCluster { position = descriptor.position, count = descriptor.count, type = descriptor.type });
            EcologyGeneration.AdjustClusters(planet, clusters, policy.OreStrength, gsPlanet.veinSettings.VeinPadding, revision: policy.Revision);
            for (int i = 0; i < clusters.Count; i++) __result[i].position = clusters[i].position;
        }
    }
}
