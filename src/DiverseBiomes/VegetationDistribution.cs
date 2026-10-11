using System;

namespace DiverseBiomes
{
    internal enum VegetationSizeMode { SuperLarge, Large, TriplePeak }

    // Value contracts kept for configuration and ecology integration. The sampler
    // is implemented here with stateless streams and triangular peak jitter.
    internal sealed class DistributionSettings
    {
        internal const float HardMaximumMultiplier = 7f;
        internal const float GiantHardMaximumMultiplier = 15f;
        private readonly float[] bounds;
        internal DistributionSettings(params float[] values)
        {
            if (values == null || values.Length != 15) throw new ArgumentException("Expected 15 distribution settings.");
            bounds = (float[])values.Clone();
            for (int pair = 0; pair < 10; pair += 2) Normalize(pair, 1, 7);
            bounds[10] = Clamp(bounds[10], 0, 1);
            Normalize(11, 0, 100);
            Normalize(13, 1, 15);
        }
        private void Normalize(int at, float minimum, float maximum)
        {
            float a = Clamp(bounds[at], minimum, maximum), b = Clamp(bounds[at + 1], minimum, maximum);
            bounds[at] = Math.Min(a, b); bounds[at + 1] = Math.Max(a, b);
        }
        internal float SuperLargeMinimum => bounds[0];
        internal float SuperLargeMaximum => bounds[1];
        internal float LargeMinimum => bounds[2];
        internal float LargeMaximum => bounds[3];
        internal float TripleSmallMinimum => bounds[4];
        internal float TripleSmallMaximum => bounds[5];
        internal float TripleMediumMinimum => bounds[6];
        internal float TripleMediumMaximum => bounds[7];
        internal float TripleLargeMinimum => bounds[8];
        internal float TripleLargeMaximum => bounds[9];
        internal float GiantPlanetChance => bounds[10];
        internal int GiantMinimumCount => (int)bounds[11];
        internal int GiantMaximumCount => (int)bounds[12];
        internal float GiantMinimumMultiplier => bounds[13];
        internal float GiantMaximumMultiplier => bounds[14];
        internal float[] ExportValues() => (float[])bounds.Clone();
        internal static DistributionSettings CreateDefaults() => new DistributionSettings(
            1.75f, 2.5f, 1.25f, 1.75f, 1f, 1f, 1.25f, 1.75f, 2.375f, 3f, .5f, 5, 15, 4, 7.5f);
        internal static float Clamp(float value, float low, float high)
            => float.IsNaN(value) || float.IsInfinity(value) ? low : Math.Max(low, Math.Min(high, value));
    }

    internal struct PlanetVegetationProfile
    {
        internal readonly VegetationSizeMode Mode;
        internal readonly float FirstPeak, SecondPeak, ThirdPeak;
        internal PlanetVegetationProfile(VegetationSizeMode mode, float first, float second, float third)
        { Mode = mode; FirstPeak = first; SecondPeak = second; ThirdPeak = third; }
    }

    internal static class VegetationDistribution
    {
        // FNV-1a combines the public identity fields; independent streams use a
        // xorshift finalizer. No shared RNG is advanced while modeling a planet.
        private static uint Append(uint hash, int value)
        {
            unchecked {
                for (int shift = 0; shift < 32; shift += 8) hash = (hash ^ (byte)(value >> shift)) * 16777619u;
                return hash;
            }
        }
        private static uint Stream(uint key, uint salt)
        {
            uint state = key ^ salt;
            if (state == 0) state = 0x6d2b79f5u;
            unchecked { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        }
        private static float Draw(uint key, uint salt) => (Stream(key, salt) >> 8) / 16777216f;
        private static uint WorldKey(int seed, int planet) => Append(Append(2166136261u, seed), planet);
        internal static uint CreatePlantKey(int seed, int planet, int id, int prototype, float x, float y, float z)
        {
            uint key = Append(Append(WorldKey(seed, planet), id), prototype);
            key = Append(key, (int)Math.Round(x * 256)); key = Append(key, (int)Math.Round(y * 256));
            return Append(key, (int)Math.Round(z * 256));
        }
        private static float Peak(float lo, float hi, uint key, uint salt)
        {
            if (hi <= lo) return lo;
            int first = (int)Math.Ceiling(lo * 10), last = (int)Math.Floor(hi * 10);
            return last < first ? (lo + hi) / 2 : (first + Stream(key, salt) % (uint)(last - first + 1)) / 10f;
        }
        internal static PlanetVegetationProfile CreateProfile(int seed, int id, DistributionSettings settings)
            => CreateProfileWithBirthOverride(seed, id, settings, false);
        internal static PlanetVegetationProfile CreateProfileWithBirthOverride(int seed, int id, DistributionSettings settings, bool birth)
        {
            settings = settings ?? DistributionSettings.CreateDefaults();
            uint key = WorldKey(seed, id);
            var mode = birth ? VegetationSizeMode.TriplePeak : (VegetationSizeMode)(Stream(key, 0x517cc1b7u) % 3);
            if (mode != VegetationSizeMode.TriplePeak) {
                float one = mode == VegetationSizeMode.Large ? Peak(settings.LargeMinimum, settings.LargeMaximum, key, 1) :
                    Peak(settings.SuperLargeMinimum, settings.SuperLargeMaximum, key, 2);
                return new PlanetVegetationProfile(mode, one, one, one);
            }
            float[] peaks = { Peak(settings.TripleSmallMinimum, settings.TripleSmallMaximum, key, 3),
                Peak(settings.TripleMediumMinimum, settings.TripleMediumMaximum, key, 4),
                Peak(settings.TripleLargeMinimum, settings.TripleLargeMaximum, key, 5) };
            Array.Sort(peaks);
            return new PlanetVegetationProfile(mode, peaks[0], peaks[1], peaks[2]);
        }
        internal static float SampleMultiplier(PlanetVegetationProfile profile, uint key)
        {
            float center = profile.FirstPeak;
            if (profile.Mode == VegetationSizeMode.TriplePeak) {
                float selector = Draw(key, 0xa24baed5u);
                center = selector < .45f ? profile.FirstPeak : selector < .8f ? profile.SecondPeak : profile.ThirdPeak;
            }
            float jitter = Draw(key, 0x9fb21c65u) - Draw(key, 0xe17a1465u);
            return DistributionSettings.Clamp(center * (1 + .08f * jitter), 1, 7);
        }
        internal static bool ShouldGenerateGiantVegetation(int seed, int id, DistributionSettings settings)
            => Draw(WorldKey(seed, id), 0xbd1e0a97u) < settings.GiantPlanetChance;
        internal static int GetGiantVegetationCount(int seed, int id, DistributionSettings settings)
            => settings.GiantMinimumCount + (int)(Stream(WorldKey(seed, id), 0x1edb3191u) %
                (uint)(settings.GiantMaximumCount - settings.GiantMinimumCount + 1));
        internal static uint GetGiantSelectionScore(uint key) => Stream(key, 0x4290ad73u);
        internal static float SampleGiantMultiplier(uint key, DistributionSettings settings)
            => SampleGiantMultiplier(key, settings.GiantMinimumMultiplier, settings.GiantMaximumMultiplier);
        internal static float SampleGiantMultiplier(uint key, float lo, float hi)
            => DistributionSettings.Clamp(lo + (hi - lo) * Draw(key, 0x67285a53u), 1, 15);
    }
}
