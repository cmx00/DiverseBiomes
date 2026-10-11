using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;

namespace DiverseBiomes
{
    public static class ReleaseChecks
    {
        private static void Require(bool success, string message) { if (!success) throw new Exception(message); }
        public static void Run()
        {
            var defaults = DistributionSettings.CreateDefaults();
            var profile = VegetationDistribution.CreateProfileWithBirthOverride(42, 502, defaults, true);
            Require(profile.Mode == VegetationSizeMode.TriplePeak && profile.FirstPeak < profile.SecondPeak && profile.SecondPeak < profile.ThirdPeak, "Birth profile must retain three distinct peaks.");
            var fixedProfile = new PlanetVegetationProfile(VegetationSizeMode.TriplePeak, 1, 1.5f, 2.5f);
            int[] counts = new int[3]; var identities = new HashSet<uint>();
            for (int i = 1; i <= 10000; i++) {
                uint key = VegetationDistribution.CreatePlantKey(42, 502, i, 61, i * .1f, 50, 100);
                Require(key == VegetationDistribution.CreatePlantKey(42, 502, i, 61, i * .1f, 50, 100), "Plant identity is nondeterministic.");
                identities.Add(key);
                float value = VegetationDistribution.SampleMultiplier(fixedProfile, key);
                Require(value >= 1 && value <= 7, "Normal plant scale exceeds bounds.");
                counts[value < 1.2f ? 0 : value < 2 ? 1 : 2]++;
                int giants = VegetationDistribution.GetGiantVegetationCount(i, 502, defaults);
                Require(giants >= 5 && giants <= 15, "Giant count leaves configured bounds.");
            }
            Require(identities.Count > 9980 && counts[0] > 2000 && counts[1] > 1500 && counts[2] > 1000, "Sampler loses identity or one of the three size populations.");

            var policy = new EcologyGeneration.GenerationPolicy(true, 1, .4f);
            string encoded = EcologyGeneration.EncodePolicy(policy);
            Require(encoded.StartsWith("LW4;"), "A new galaxy must identify the new sampler.");
            var decoded = EcologyGeneration.DecodePolicy(encoded);
            Require(decoded.Revision == 4 && decoded.GrassShare == .08f && decoded.GrassHeight == 1.45f, "LW4 policy failed to round-trip.");
            foreach (string old in new[] { "LW1;1;1;0.4", "LW2;1;1;0.4", "LW3;1;1;0.4;0.08;1.75;1.45" })
                Require(EcologyGeneration.DecodePolicy(old).Revision < 4, "Historical policy must remain identifiable.");
            bool rejected = false;
            try { EcologyGeneration.DecodePolicy("LW5;1;1;0.4;0.08;1.75;1.45"); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Unknown future policy must be rejected.");

            var limited = GroundCover.Limit(new Vector3(9, 12, 9), new Vector4(0, 0, 0, 0), 1.75f, 1.45f);
            Require(limited.x <= 1.75f && limited.y <= 1.45f && GroundCover.Limit(limited, new Vector4(0, 0, 0, 0), 1.75f, 1.45f) == limited, "Ground-cover cap grows or repeatedly shrinks grass.");
            Require(GroundCover.IsGrass(1081) && !GroundCover.IsGrass(1044) && !GroundCover.IsGrass(9999), "Effects or landing capsules classified as grass.");
            foreach (short prototype in new short[] { 1044, 9999 }) {
                var special = new VegeData { protoId = prototype, scl = Vector3.one };
                float factor;
                Require(EcologyGeneration.PreparePlant(null, ref special, 123, fixedProfile, out factor) && factor == 1, "Special objects were resized without a community policy.");
            }

            var saved = new VegeData[5]; var fresh = new VegeData[5];
            for (int id = 1; id < 5; id++) {
                saved[id] = new VegeData { id = id, protoId = 61, pos = new Vector3(id, 2, 3), scl = Vector3.one };
                fresh[id] = saved[id]; fresh[id].scl = new Vector3(1.2f, 1.2f, 1.2f);
            }
            saved[2].id = 0; fresh[3].protoId = 62; fresh[4].pos.x += 1;
            Require(ExistingVegetationMigration.SyncMatchingPlants(saved, 5, fresh, 5) == 1, "Migration changed cleared, replaced or moved vegetation.");
            Require(saved[2].id == 0 && saved[3].scl == Vector3.one && saved[4].pos.x == 4, "Migration changed identity or location.");
            ExistingVegetationMigration.SyncMatchingPlants(saved, 5, fresh, 5);
            Require(saved[1].scl == fresh[1].scl, "Repeated scale reconciliation accumulated a multiplier.");

            string[] themes = { "Prairie", "Mediterranean", "OceanicJungle", "RedStone", "Sakura", "Savanna", "PandoraSwamp", "PandoraSwamp2", "GiganticForest", "RedForest", "Pandora", "Beach" };
            foreach (string name in themes) {
                var theme = ProfiledCommunities.Resolve(name);
                Require(theme.Organic && theme.Names.Length == 8, "Organic-theme community coverage changed: " + name);
            }
            Require(!ProfiledCommunities.Resolve("OceanWorld").Organic && ProfiledCommunities.Resolve("Beach").GrassOnly, "Empty ocean or beach classification changed.");
            Console.WriteLine("PASS: deterministic sampler, three size populations, LW4/legacy policies, grass bounds, special objects, saved-ID reconciliation and 12 theme profiles.");
            StandaloneChecks();
        }
        private static void StandaloneChecks()
        {
            foreach (int id in new[] { 1, 8, 14, 15, 18, 22, 25 })
                Require(ProfiledCommunities.ForThemeProto(new ThemeProto { ID = id }).Organic, "Vanilla organic theme lacks a profile: " + id);
            Require(ProfiledCommunities.ForThemeProto(new ThemeProto { ID = 999, Name = "Barren" }) == ProfiledCommunities.Mineral,
                "Barren vanilla theme incorrectly gains living communities.");
            var coast = new TerrainTraits { HasWater = true, Elevation = .6f, Slope = .05f, Moisture = .8f, Patch = .4f };
            var ridge = coast; ridge.Slope = .9f;
            var profile = ProfiledCommunities.Resolve("Mediterranean");
            Require(ProfiledCommunities.Choose(profile, coast).Kind != ProfiledCommunities.Choose(profile, ridge).Kind,
                "Vanilla communities ignore shoreline and slope.");
            var desc = new GameDesc { creationTime = new DateTime(638000000000000000, DateTimeKind.Utc), galaxySeed = 42, starCount = 64 };
            var a = new GalaxyData(); var b = new GalaxyData();
            var record = WorldEcology.FreshRecord();
            WorldEcology.Bind(a, record, desc);
            byte[] bytes;
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) WorldEcology.WriteRecord(writer, record);
                bytes = stream.ToArray();
            }
            WorldEcology.Record loaded;
            using (var reader = new BinaryReader(new MemoryStream(bytes))) loaded = WorldEcology.ReadRecord(reader);
            WorldEcology.ValidateIdentity(loaded, desc);
            WorldEcology.Bind(b, loaded, desc);
            Require(loaded.Policy.Revision == 4 && loaded.Sizes.TripleLargeMaximum == record.Sizes.TripleLargeMaximum,
                "Independent save lost the sampler or its size settings.");
            Require(EcologyGeneration.UsesCommunities(new PlanetData { galaxy = a }) && EcologyGeneration.UsesCommunities(new PlanetData { galaxy = b }),
                "Vanilla galaxies cannot enable communities without GS2.");
            bool mismatched = false;
            try { WorldEcology.ValidateIdentity(loaded, new GameDesc { creationTime = desc.creationTime, galaxySeed = 43, starCount = 64 }); }
            catch (InvalidDataException) { mismatched = true; }
            Require(mismatched, "One save inherited another save's biome settings.");
            bool truncated = false;
            try { using (var reader = new BinaryReader(new MemoryStream(new byte[2]))) WorldEcology.ReadRecord(reader); }
            catch (EndOfStreamException) { truncated = true; }
            Require(truncated, "Truncated biome data silently became a new policy.");

            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            Action<string, object[]> invoke = (name, args) => typeof(WorldEcology).GetMethod(name, flags).Invoke(null, args);
            var loadState = new object[] { null };
            invoke("BeginLoad", loadState);
            using (var reader = new BinaryReader(new MemoryStream(bytes))) WorldEcology.Import(reader);
            invoke("GalaxyCreated", new object[] { desc, b });
            invoke("EndLoad", new object[] { null, true, loadState[0] });
            Require(WorldEcology.ActivePolicy?.Revision == 4, "Preloaded policy was not attached before modeling.");
            loadState = new object[] { null }; invoke("BeginLoad", loadState);
            WorldEcology.IntoOtherSave();
            invoke("GalaxyCreated", new object[] { desc, new GalaxyData() });
            invoke("EndLoad", new object[] { null, true, loadState[0] });
            Require(WorldEcology.ActivePolicy == null, "A historical vanilla save inherited the previous save's new ecology.");
            var newState = new object[] { null }; invoke("BeginNew", newState);
            invoke("GalaxyCreated", new object[] { desc, new GalaxyData() });
            WorldEcology.IntoOtherSave();
            Require(WorldEcology.ActivePolicy?.Revision == 4, "DSPModSave NewGame reset removed fresh ecology.");
            invoke("EndNew", new object[] { null, true, newState[0] });
            var previous = WorldEcology.ActivePolicy;
            loadState = new object[] { null }; invoke("BeginLoad", loadState);
            WorldEcology.IntoOtherSave();
            invoke("EndLoad", new object[] { null, false, loadState[0] });
            Require(ReferenceEquals(WorldEcology.ActivePolicy, previous), "A failed load lost the current world's policy.");
            newState = new object[] { null }; invoke("BeginNew", newState);
            invoke("EndNew", new object[] { new IOException("Simulated failure"), false, newState[0] });
            Require(ReferenceEquals(WorldEcology.ActivePolicy, previous), "A failed new game lost the current world's policy.");
            Console.WriteLine("PASS: vanilla theme/terrain profiles, independent settings round-trip, save identity, corrupt data, load switching, new-game lifecycle and failed-load recovery.");
        }
    }
}
