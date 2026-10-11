using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiverseBiomes
{
    // LW3 catalogue. Cells have real density/size/ground-cover differences, not just labels.
    // Slots: shore, wet hollow, meadow, edge woodland, canopy, ancient core, dry patch, ridge.
    public sealed class ThemeEcology
    {
        public readonly string Key, Label;
        public readonly bool Organic, GrassOnly;
        public readonly float MoistureBias, ForestEdge, CoastHeight, TreeStature, PatchFrequency;
        public readonly string[] Names;
        public readonly float[] Trees, Grass, LargeGrass;
        internal ThemeEcology(string key, string label, bool grassOnly, float moisture, float edge, float coast,
            float stature, float frequency, string names, float[] trees, float[] grass, float[] large)
        {
            Key = key; Label = label; Organic = true; GrassOnly = grassOnly;
            MoistureBias = moisture; ForestEdge = edge; CoastHeight = coast;
            TreeStature = stature; PatchFrequency = frequency;
            Names = names.Split(new[]{'|'}); Trees = trees; Grass = grass; LargeGrass = large;
        }
        internal ThemeEcology() { Key = "Mineral"; Label = "无机地貌"; Organic = false; }
    }

    public static class ProfiledCommunities
    {
        public static readonly ThemeEcology Mineral = new ThemeEcology();
        public static readonly ThemeEcology[] OrganicProfiles = {
            new ThemeEcology("Prairie", "草原生态", false, -.04f, .64f, 1.50f, .95f, 1.10f,
                "湿岸草甸|低地丰草甸|花草旷野|草甸林缘|深草海|丰草核心|金色短草坡|风剪草坡",
                new[]{.52f,.56f,.22f,.48f,.42f,.62f,.18f,.22f},
                new[]{.84f,.96f,.88f,.80f,.98f,.90f,.54f,.64f},
                new[]{1.25f,1.70f,1.25f,1.15f,2.10f,1.60f,.45f,.75f}),
            new ThemeEcology("Mediterranean", "温带生态", false, .02f, .48f, 1.65f, 1f, 1f,
                "海风岸林|湿谷阔叶林|阳坡草甸|温带林缘|浓荫林|古木林岛|干坡疏灌|高地矮林",
                new[]{.88f,.92f,.36f,.64f,.91f,.97f,.28f,.38f},
                new[]{.70f,.65f,.90f,.76f,.42f,.34f,.46f,.42f},
                new[]{.65f,.85f,1.65f,1.30f,.40f,.30f,.75f,.60f}),
            new ThemeEcology("OceanicJungle", "热带雨林", false, .13f, .34f, 1.85f, 1.08f, .85f,
                "滨海雨林|湿洼密丛|林窗草丛|热带林缘|热带冠层|古木雨林|暖坡疏林|高地云林",
                new[]{.94f,.98f,.42f,.78f,.98f,1f,.52f,.70f},
                new[]{.62f,.60f,.88f,.68f,.34f,.28f,.60f,.50f},
                new[]{.70f,.80f,1.75f,1.35f,.40f,.25f,.80f,.60f}),
            new ThemeEcology("RedStone", "赤土生态", false, -.08f, .55f, 1.40f, .96f, 1.20f,
                "红岩岸丛|赤土湿谷|赤原高草|风化林缘|赤冠密林|红岩古林|红砂灌丛|风蚀矮林",
                new[]{.68f,.78f,.30f,.58f,.82f,.89f,.30f,.34f},
                new[]{.62f,.72f,.86f,.68f,.48f,.36f,.46f,.40f},
                new[]{.80f,1.05f,1.80f,1.25f,.50f,.35f,.80f,.55f}),
            new ThemeEcology("Sakura", "樱林生态", false, .07f, .42f, 1.60f, .92f, .95f,
                "湖滨花甸|花谷湿林|落英草甸|樱林边缘|繁花密林|古樱林岛|晴坡疏樱|海风矮樱",
                new[]{.82f,.92f,.40f,.75f,.97f,.92f,.38f,.42f},
                new[]{.80f,.72f,.92f,.84f,.54f,.44f,.60f,.52f},
                new[]{.90f,.90f,1.60f,1.45f,.55f,.35f,.90f,.65f}),
            new ThemeEcology("Savanna", "稀树草原", false, -.16f, .67f, 1.30f, 1f, 1.05f,
                "湿岸高草|雨季洼地|稀树草原|金色林缘|湿谷树林|孤木林岛|旱季短草|高草风坡",
                new[]{.60f,.70f,.22f,.46f,.67f,.72f,.16f,.26f},
                new[]{.86f,.98f,.95f,.88f,.72f,.60f,.46f,.76f},
                new[]{1.60f,1.90f,2.20f,1.65f,.80f,.55f,.60f,1.40f}),
            new ThemeEcology("PandoraSwamp", "异星沼泽", false, .23f, .32f, 1.70f, 1.12f, .90f,
                "异星低地|湿洼密林|异星蕨甸|孢子林缘|巨冠湿林|异星古林|孢子疏丛|高原异林",
                new[]{.88f,.99f,.48f,.80f,.98f,1f,.50f,.58f},
                new[]{.76f,.80f,.92f,.78f,.42f,.30f,.62f,.52f},
                new[]{1f,1.20f,1.80f,1.50f,.50f,.30f,.90f,.65f}),
            new ThemeEcology("PandoraSwamp2", "青翠异林", false, .15f, .39f, 1.45f, 1.02f, 1.15f,
                "冷色低地|青翠洼地林|孢子草甸|青翠林缘|青冠密林|静谧古林|风干孢丛|青翠高原林",
                new[]{.76f,.94f,.36f,.72f,.90f,.95f,.38f,.54f},
                new[]{.70f,.78f,.90f,.82f,.48f,.38f,.56f,.50f},
                new[]{.90f,1.05f,1.60f,1.55f,.60f,.40f,.80f,.55f}),
            new ThemeEcology("GiganticForest", "巨冠森林", false, .18f, .28f, 1.80f, 1.28f, .72f,
                "巨林岸带|巨林湿谷|巨冠林窗|巨木林缘|层叠巨冠林|远古巨木林|疏巨木坡|山脊巨林",
                new[]{.90f,.96f,.36f,.70f,.98f,1f,.42f,.60f},
                new[]{.50f,.50f,.90f,.65f,.24f,.20f,.50f,.38f},
                new[]{.60f,.65f,1.80f,1.30f,.30f,.20f,.75f,.45f}),
            new ThemeEcology("RedForest", "赤色森林", false, .05f, .44f, 1.55f, 1.06f, 1.15f,
                "赤林岸带|赤木谷林|红林草甸|赤树斑块|红冠密林|赤木古林|红叶疏林|岩坡低林",
                new[]{.78f,.88f,.34f,.72f,.92f,.96f,.36f,.42f},
                new[]{.64f,.68f,.90f,.80f,.44f,.32f,.55f,.44f},
                new[]{.80f,.90f,1.70f,1.40f,.45f,.30f,.85f,.60f}),
            new ThemeEcology("Pandora", "水岸异星林", false, .20f, .35f, 1.95f, 1.10f, .88f,
                "异星水岸林|湿地异林|水岸蕨甸|异星林缘|异星冠层|水泽古林|暖坡异丛|高地异林",
                new[]{.96f,.98f,.44f,.78f,.96f,.99f,.46f,.60f},
                new[]{.86f,.84f,.94f,.82f,.46f,.34f,.62f,.52f},
                new[]{1.10f,1.30f,1.85f,1.50f,.55f,.35f,.85f,.60f}),
            new ThemeEcology("Beach", "海岸草甸", true, -.02f, .55f, 2.10f, 1f, 1.30f,
                "潮上草甸|礁背丰草|开阔沙草地|海风草丛|深草岛|丰草坪|裸砂稀草|礁脊矮草",
                new[]{0f,0f,0f,0f,0f,0f,0f,0f},
                new[]{.72f,.90f,.64f,.80f,.94f,.86f,.24f,.42f},
                new[]{1.10f,1.70f,1.30f,1.50f,1.90f,1.50f,.35f,.50f})
        };
        private static readonly ThemeEcology Generic = new ThemeEcology("Organic", "通用有机生态", false,
            0f, .50f, 1.60f, 1f, 1f, "岸带植被|低地植被|开阔草甸|斑块林缘|茂密植被|古木斑块|干地疏丛|高地低丛",
            new[]{.80f,.88f,.36f,.64f,.90f,.96f,.30f,.38f},
            new[]{.70f,.76f,.90f,.78f,.44f,.34f,.48f,.42f},
            new[]{.80f,1f,1.60f,1.30f,.45f,.30f,.75f,.60f});
        private static readonly Dictionary<ThemeProto, ThemeEcology> Cache = new Dictionary<ThemeProto, ThemeEcology>();
        private static readonly float[] Small = { .10f,.12f,.68f,.44f,.16f,.04f,.88f,.85f };
        private static readonly float[] Medium = { .60f,.50f,.30f,.44f,.54f,.22f,.11f,.14f };
        private static readonly float[] Stature = { .98f,1.04f,.95f,1f,1.06f,1.16f,.86f,.88f };
        private static readonly CommunityKind[] Kinds = { CommunityKind.RiparianForest, CommunityKind.RiparianForest,
            CommunityKind.MeadowWoodland, CommunityKind.MeadowWoodland, CommunityKind.AncientGrove,
            CommunityKind.AncientGrove, CommunityKind.AridScrub, CommunityKind.HighlandScrub };

        public static ThemeEcology Resolve(string name, string baseName = null, bool hasLivingModels = false)
        {
            foreach (var profile in OrganicProfiles) if (profile.Key == name) return profile;
            foreach (var profile in OrganicProfiles) if (profile.Key == baseName) return profile;
            return hasLivingModels ? Generic : Mineral;
        }
        internal static ThemeEcology ForVanilla(PlanetData planet)
        {
            if (planet == null || planet.type == EPlanetType.Gas) return Mineral;
            return ForThemeProto(LDB.themes?.Select(planet.theme));
        }
        public static ThemeEcology ForThemeProto(ThemeProto theme)
        {
            if (theme == null) return Mineral;
            lock (Cache) {
                ThemeEcology profile;
                if (Cache.TryGetValue(theme, out profile)) return profile;
                string key;
                switch (theme.ID) {
                    case 1: key = "Mediterranean"; break;
                    case 8: key = "OceanicJungle"; break;
                    case 14: key = "RedStone"; break;
                    case 15: key = "Prairie"; break;
                    case 18: key = "Sakura"; break;
                    case 22: key = "Savanna"; break;
                    case 25: key = "PandoraSwamp"; break;
                    default: key = theme.Name; break;
                }
                profile = Resolve(key, hasLivingModels: HasLivingModels(new[] { theme.Vegetables0, theme.Vegetables1,
                    theme.Vegetables2, theme.Vegetables3, theme.Vegetables4, theme.Vegetables5 }));
                if (Cache.Count > 256) Cache.Clear();
                Cache[theme] = profile; return profile;
            }
        }
        public static bool HasLivingModels(IEnumerable<int[]> groups)
        {
            foreach (var group in groups)
                if (group != null) foreach (int id in group)
                    if (GroundCover.IsGrass((short)id) || EcologyGeneration.IsPlant((short)id)) return true;
            return false;
        }
        public static CommunitySample Choose(ThemeEcology profile, TerrainTraits terrain)
        {
            if (!profile.Organic) return Inorganic(terrain);
            if (terrain.Cold || terrain.Latitude > .86f)
                return Common("寒地低丛", CommunityKind.PolarTundra, .24f,.95f,.045f,.22f,.35f,.85f);
            if (terrain.Slope > .68f)
                return Common("风剪矮丛", CommunityKind.HighlandScrub, .20f,.95f,.045f,.24f,.35f,.82f);
            if (terrain.Elevation > 8f)
                return Common("岩坡过渡", CommunityKind.HighlandScrub, .32f,.88f,.11f,.36f,.60f,.90f);
            float moisture = Clamp(terrain.Moisture + profile.MoistureBias);
            if (moisture < .12f)
                return Common("裸地稀丛", CommunityKind.AridScrub, .15f,.97f,.025f,.18f,.30f,.85f);

            if (terrain.HasWater && terrain.Elevation < profile.CoastHeight)
                return Blend(Cell(profile, 0), Cell(profile, 1), Smooth((terrain.Elevation / profile.CoastHeight - .35f) / .65f), terrain.Detail);
            if (terrain.Slope > .34f || terrain.Elevation > 4.8f)
                return Blend(Cell(profile, 3), Cell(profile, 7), Smooth((terrain.Slope - .20f) / .27f + (terrain.Elevation - 3.8f) * .25f), terrain.Detail);
            if (terrain.Elevation < 2.2f && moisture > .62f)
                return Blend(Cell(profile, 1), Cell(profile, 4), Smooth((terrain.Elevation - .8f) / 1.8f), terrain.Detail);
            if (moisture < .34f)
                return Blend(Cell(profile, 6), Cell(profile, 2), Smooth((moisture - .14f) / .20f), terrain.Detail);
            if (terrain.Patch > .77f && moisture > .57f)
                return Blend(Cell(profile, 4), Cell(profile, 5), Smooth((terrain.Patch - .72f) / .18f), terrain.Detail);
            float woodland = Smooth((terrain.Patch - profile.ForestEdge + .09f) / .18f);
            if (terrain.Patch < profile.ForestEdge + .09f)
                return Blend(Cell(profile, 2), Cell(profile, 3), woodland, terrain.Detail);
            return Blend(Cell(profile, 3), Cell(profile, 4), Smooth((terrain.Patch - profile.ForestEdge - .05f) / .22f), terrain.Detail);
        }
        private static CommunitySample Cell(ThemeEcology profile, int slot)
        {
            return new CommunitySample(Kinds[slot], profile.Trees[slot], Small[slot], Medium[slot], 3) {
                BiomeName = profile.Names[slot], TreeScale = profile.TreeStature * Stature[slot],
                GrassRetention = profile.Grass[slot], GrassLargeFactor = profile.LargeGrass[slot]
            };
        }
        private static CommunitySample Common(string name, CommunityKind kind, float cover, float small, float medium,
            float grass, float large, float stature)
        {
            return new CommunitySample(kind,cover,small,medium,3) {
                BiomeName=name,TreeScale=stature,GrassRetention=grass,GrassLargeFactor=large
            };
        }
        private static CommunitySample Blend(CommunitySample a, CommunitySample b, float blend, float detail)
        {
            // Names/kinds select one cell; all numerical characteristics blend continuously at its boundary.
            var sample = detail < blend ? b : a;
            sample.Retention = Lerp(a.Retention,b.Retention,blend);
            sample.SmallShare = Lerp(a.SmallShare,b.SmallShare,blend);
            sample.MediumShare = Lerp(a.MediumShare,b.MediumShare,blend);
            sample.TreeScale = Lerp(a.TreeScale,b.TreeScale,blend);
            sample.GrassRetention = Lerp(a.GrassRetention,b.GrassRetention,blend);
            sample.GrassLargeFactor = Lerp(a.GrassLargeFactor,b.GrassLargeFactor,blend);
            return sample;
        }
        private static CommunitySample Inorganic(TerrainTraits t)
        {
            string name; float cover, size;
            if (t.Cold) { name="冻土冰屑带";cover=.65f+.20f*t.Patch;size=1.05f+.20f*t.Detail; }
            else if (t.Slope>.36f || t.Elevation>5f) { name="裸岩碎屑坡";cover=.42f+.24f*t.Patch;size=.95f+.20f*t.Detail; }
            else if (t.HasWater && t.Elevation<1.6f) { name="岸缘沉积带";cover=.58f+.20f*t.Patch;size=.90f+.15f*t.Detail; }
            else if (t.Patch>.60f) { name="砾石聚集地";cover=.84f;size=1.15f+.25f*t.Detail; }
            else { name="风蚀稀疏原";cover=.30f+.20f*t.Patch;size=.85f+.15f*t.Detail; }
            return new CommunitySample(CommunityKind.AridScrub,cover,1f,0f,3) {
                BiomeName=name,GrassRetention=0f,GrassLargeFactor=0f,DecorationScale=size
            };
        }
        internal static bool IsDecoration(short id) =>
            id>=1 && id<=12 || id>=19 && id<=21 || id>=48 && id<=59 || id>=66 && id<=90 ||
            id>=601 && id<=605 || id>=611 && id<=647 || id>=651 && id<=671 || id>=681 && id<=685 ||
            id>=691 && id<=713 || id>=721 && id<=750 || id>=1005 && id<=1007 ||
            id>=1041 && id<=1043 || id>=1051 && id<=1055 || id>=1061 && id<=1074;
        internal static float SpeciesRetention(short protoId, CommunitySample sample)
        {
            // Stable local species preferences, using only models generated by the original theme.
            uint salt = 2166136261u;
            if (sample.BiomeName != null) unchecked { foreach (char c in sample.BiomeName) salt=(salt^c)*16777619u; }
            return .85f+.15f*CommunityDesign.Unit(unchecked((uint)protoId*0x9e3779b9u)^salt);
        }
        public static string Summary(ThemeEcology profile) => profile.Organic ?
            profile.Label + " · " + profile.Names[2] + "/" + profile.Names[4] : "统一荒原 · 岩屑/冻土";
        private static float Clamp(float value) => Math.Max(0f,Math.Min(1f,value));
        private static float Smooth(float value) { value=Clamp(value);return value*value*(3f-2f*value); }
        private static float Lerp(float a,float b,float t) => a+(b-a)*t;
    }
}
