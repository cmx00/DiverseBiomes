using System;
using System.Globalization;
using HarmonyLib;

namespace DiverseBiomes
{
    internal static class PlanetInfoUiPatches
    {
        private static DistributionSettings settings;
        private const int Marker = -73520;
        internal static void Apply(Harmony harmony, DistributionSettings values)
        {
            settings = values;
            var size = AccessTools.Method(typeof(UIPlanetDetail), "SetResCount", new[] { typeof(int) });
            if (size == null || AccessTools.Method(typeof(UIPlanetDetail), "GetEntry") == null)
                throw new MissingMethodException("Planet detail entries changed.");
            harmony.Patch(size, prefix: new HarmonyMethod(typeof(PlanetInfoUiPatches), nameof(Describe)));
        }
        private static void Describe(UIPlanetDetail __instance, ref int count, UIResAmountEntry ___tipEntry)
        {
            var list = __instance.entries;
            var planet = __instance.planet;
            if (list == null || planet == null || planet.type == EPlanetType.Gas) return;
            string[] labels = { "植物尺度 / Plant scale", "地表群落 / Communities", "古木上限 / Grove limit" };
            string[] values = { GetVegetationScaleText(planet, settings), CommunityText(planet), GetGiantVegetationCount(planet, settings).ToString() };
            bool tipPresent = ___tipEntry != null && list.Remove(___tipEntry);
            int inserted = 0;
            for (int row = 0; row < values.Length; row++) {
                UIResAmountEntry entry = list.Find(item => item.refId == Marker - row);
                if (entry == null) {
                    entry = (UIResAmountEntry)AccessTools.Method(typeof(UIPlanetDetail), "GetEntry").Invoke(__instance, null);
                    entry.SetInfo(list.Count, labels[row], null, "", false, false, "");
                    entry.refId = Marker - row; list.Add(entry); inserted++;
                }
                entry.valueString = values[row];
            }
            if (tipPresent) { ___tipEntry.SetIndex(list.Count); list.Add(___tipEntry); }
            count += inserted;
        }
        internal static string GetVegetationScaleText(PlanetData planet, DistributionSettings values)
        {
            if (planet == null || planet.type == EPlanetType.Gas) return "—";
            if (EcologyAdapters.PolicyFor(planet) == null) return "原有地表 / Existing surface";
            if (EcologyGeneration.RevisionFor(planet) > 0 && EcologyGeneration.RevisionFor(planet) < 4) return "已保存 / Saved";
            if (EcologyGeneration.UsesCommunities(planet)) {
                var theme = EcologyGeneration.ThemeProfile(planet);
                if (!theme.Organic) return "—";
                if (theme.GrassOnly) return "草 / Grass";
            }
            var profile = VegetationDistribution.CreateProfileWithBirthOverride(planet.seed, planet.id, WorldEcology.SizesFor(planet),
                BirthVegetationProfile.IsBirth(planet) || EcologyGeneration.UsesCommunities(planet));
            Func<float, string> number = n => n.ToString("0.##", CultureInfo.InvariantCulture);
            return profile.Mode == VegetationSizeMode.TriplePeak ? number(profile.FirstPeak) + " / " + number(profile.SecondPeak) + " / " + number(profile.ThirdPeak) + "×" : number(profile.FirstPeak) + "×";
        }
        internal static int GetGiantVegetationCount(PlanetData planet, DistributionSettings values)
        {
            if (planet == null || planet.type == EPlanetType.Gas || planet.radius + .5f < WorldEcology.MinimumGiantRadius(planet) ||
                EcologyAdapters.PolicyFor(planet) == null ||
                (EcologyGeneration.RevisionFor(planet) > 0 && EcologyGeneration.RevisionFor(planet) < 4) ||
                !VegetationDistribution.ShouldGenerateGiantVegetation(planet.seed, planet.id, WorldEcology.SizesFor(planet))) return 0;
            if (EcologyGeneration.UsesCommunities(planet)) {
                var theme = EcologyGeneration.ThemeProfile(planet);
                if (!theme.Organic || theme.GrassOnly) return 0;
            }
            return VegetationDistribution.GetGiantVegetationCount(planet.seed, planet.id, WorldEcology.SizesFor(planet));
        }
        private static string CommunityText(PlanetData planet)
            => EcologyGeneration.RevisionFor(planet) >= 4 && EcologyGeneration.UsesCommunities(planet) ?
                ProfiledCommunities.Summary(EcologyGeneration.ThemeProfile(planet)) : "原有地表 / Existing surface";
    }
}
