using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace DiverseBiomes
{
    internal static class VegetationGenerationPatches
    {
        internal static DistributionSettings Settings { get; private set; }
        [ThreadStatic] private static Stack<Session> sessions;
        private sealed class Session
        {
            internal PlanetData World;
            internal PlanetVegetationProfile Size;
            internal List<int> Removed = new List<int>();
            internal int Depth;
        }
        internal static int Apply(Harmony harmony, DistributionSettings settings)
        {
            Settings = settings;
            var methods = new HashSet<MethodInfo>();
            var candidates = new List<Type> { typeof(PlanetAlgorithm) };
            for (int index = 0; index <= 14; index++) candidates.Add(typeof(PlanetAlgorithm).Assembly.GetType("PlanetAlgorithm" + index));
            foreach (var type in candidates) {
                var method = type?.GetMethod("GenerateVegetables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null && !method.IsAbstract && methods.Add(method))
                    harmony.Patch(method, prefix: Hook(nameof(Open)), finalizer: Hook(nameof(Close)));
            }
            if (methods.Count == 0) throw new MissingMethodException("Vegetation generation entry points are unavailable.");
            var allocate = AccessTools.Method(typeof(PlanetRawData), "AddVegeData", new[] { typeof(VegeData) });
            if (allocate == null || allocate.ReturnType != typeof(int)) throw new MissingMethodException("PlanetRawData.AddVegeData");
            harmony.Patch(allocate, prefix: Hook(nameof(Decorate)));
            return methods.Count;
        }
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(VegetationGenerationPatches), name);
        internal static void Register(Harmony harmony, Type type)
        {
            var method = type?.GetMethod("GenerateVegetables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null || method.IsAbstract) throw new MissingMethodException("Optional vegetation generator entry point is unavailable.");
            harmony.Patch(method, prefix: Hook(nameof(Open)), finalizer: Hook(nameof(Close)));
        }
        internal static DistributionSettings SettingsFor(PlanetData planet) => WorldEcology.SizesFor(planet);
        private static void Open(PlanetData ___planet, out bool __state)
        {
            __state = false;
            if (___planet == null) return;
            if (sessions == null) sessions = new Stack<Session>();
            if (sessions.Count > 0 && ReferenceEquals(sessions.Peek().World, ___planet)) sessions.Peek().Depth++;
            else sessions.Push(new Session { World = ___planet, Size = BirthVegetationProfile.Create(___planet, SettingsFor(___planet)), Depth = 1 });
            __state = true;
        }
        private static Exception Close(Exception __exception, bool __state)
        {
            if (!__state || sessions == null || sessions.Count == 0) return __exception;
            var finished = sessions.Peek();
            if (--finished.Depth != 0) return __exception;
            sessions.Pop();
            if (__exception == null && finished.World.data != null && EcologyAdapters.PolicyFor(finished.World) != null) {
                EcologyGeneration.Prune(finished.World.data, finished.Removed);
                GiantVegetationApplicator.Apply(finished.World, finished.World.data.vegePool, finished.World.data.vegeCursor, SettingsFor(finished.World));
            }
            return __exception;
        }
        private static void Decorate(PlanetRawData __instance, [HarmonyArgument(0)] ref VegeData vegetation)
        {
            if (sessions == null || sessions.Count == 0) return;
            Session session = sessions.Peek();
            if (!ReferenceEquals(session.World.data, __instance)) return;
            // Historical surfaces used another sampler. Preserve factory data;
            // do not feed that saved policy through the new implementation.
            var policy = EcologyAdapters.PolicyFor(session.World);
            if (policy == null) return;
            int revision = policy.Revision;
            if (revision > 0 && revision < 4) return;
            uint key = VegetationDistribution.CreatePlantKey(session.World.seed, session.World.id, __instance.vegeCursor,
                vegetation.protoId, vegetation.pos.x, vegetation.pos.y, vegetation.pos.z);
            float factor;
            if (!EcologyGeneration.PreparePlant(session.World, ref vegetation, key, session.Size, out factor)) session.Removed.Add(__instance.vegeCursor);
            vegetation.scl *= factor;
            GroundCover.Clamp(ref vegetation, policy);
        }
    }
}
