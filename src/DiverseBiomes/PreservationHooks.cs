using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DiverseBiomes
{
    internal static class PreservationHooks
    {
        [ThreadStatic] private static int playerCollisionDepth;
        [ThreadStatic] private static BuildScope building;
        [ThreadStatic] private static bool queryingExpandedArea;
        [ThreadStatic] private static int[] expandedCandidates;
        private static float prototypeReach = -1f;
        private static readonly ConditionalWeakTable<PlanetFactory, ScaleBound> scales = new ConditionalWeakTable<PlanetFactory, ScaleBound>();

        private sealed class ScaleBound
        {
            internal VegeData[] Pool;
            internal int Cursor;
            internal long Tick = long.MinValue;
            internal float Maximum = 15f;
        }

        private sealed class BuildScope
        {
            internal PlanetFactory Factory;
            internal BuildingOverlapGeometry.Shape[] Shapes;
            internal int TerrainDepth;
        }

        internal static void Apply(Harmony harmony)
        {
            Type collision = AccessTools.TypeByName("UnityEngine.Collision");
            if (collision == null) throw new TypeLoadException("UnityEngine.Collision is unavailable.");
            var playerContact = AccessTools.Method(typeof(PlayerController), "HandleCollision", new[] { collision, typeof(bool) });
            var collisionDamage = AccessTools.Method(typeof(SkillSystem), nameof(SkillSystem.DamageGroundObjectByLocalCaster),
                new[] { typeof(PlanetFactory), typeof(int), typeof(int), typeof(SkillTargetLocal).MakeByRefType(), typeof(SkillTargetLocal).MakeByRefType() });
            var build = AccessTools.Method(typeof(PlanetFactory), nameof(PlanetFactory.BuildFinally),
                new[] { typeof(Player), typeof(int), typeof(bool), typeof(bool) });
            var flatten = AccessTools.Method(typeof(PlanetFactory), nameof(PlanetFactory.FlattenTerrain),
                new[] { typeof(Vector3), typeof(Quaternion), typeof(Bounds), typeof(float), typeof(float),
                    typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(SkillTarget), typeof(Bounds) });
            var query = AccessTools.Method(typeof(NearColliderLogic), nameof(NearColliderLogic.GetVegetablesInAreaNonAlloc),
                new[] { typeof(Vector3), typeof(float), typeof(int[]) });
            if (playerContact == null || collisionDamage == null || build == null || flatten == null || query == null)
                throw new MissingMethodException("DiverseBiomes plant protection cannot bind to this game's collision/build API.");
            harmony.Patch(playerContact, prefix: Method(nameof(EnterPlayerCollision)), finalizer: Method(nameof(LeavePlayerCollision)));
            harmony.Patch(collisionDamage, prefix: Method(nameof(ScaleCollisionDamage)));
            harmony.Patch(build, prefix: Method(nameof(EnterBuilding)), finalizer: Method(nameof(LeaveBuilding)));
            harmony.Patch(flatten, prefix: Method(nameof(EnterTerrain)), finalizer: Method(nameof(LeaveTerrain)));
            harmony.Patch(query, prefix: Method(nameof(SelectBuildingCandidates)));
        }

        private static HarmonyMethod Method(string name) => new HarmonyMethod(typeof(PreservationHooks), name);

        private static void EnterPlayerCollision(out int __state)
        {
            __state = playerCollisionDepth;
            playerCollisionDepth++;
        }

        private static Exception LeavePlayerCollision(Exception __exception, int __state)
        {
            playerCollisionDepth = __state;
            return __exception;
        }

        private static void ScaleCollisionDamage([HarmonyArgument(1)] ref int damage, [HarmonyArgument(3)] ref SkillTargetLocal target)
        {
            if (playerCollisionDepth > 0 && target.type == ETargetType.Vegetable && target.id > 0 && damage > 0)
                damage = (int)Math.Floor(damage * (double)Math.Max(0f, Math.Min(1f, ModConfig.PlayerCollisionDamageMultiplier)));
        }

        private static void EnterBuilding(PlanetFactory __instance, [HarmonyArgument(1)] int prebuildId,
            [HarmonyArgument(3)] bool flattenTerrain, out BuildScope __state)
        {
            __state = building;
            building = null;
            if (!flattenTerrain || __instance?.prebuildPool == null || prebuildId <= 0 || prebuildId >= __instance.prebuildPool.Length) return;
            var prebuild = __instance.prebuildPool[prebuildId];
            if (prebuild.id != prebuildId) return;
            PrefabDesc desc = LDB.items?.Select(prebuild.protoId)?.prefabDesc;
            var geometry = PrebuildGeometry(__instance.planet?.physics, prebuild, desc);
            if (geometry.Length != 0) building = new BuildScope { Factory = __instance, Shapes = geometry };
        }

        private static Exception LeaveBuilding(Exception __exception, BuildScope __state)
        {
            building = __state;
            return __exception;
        }

        private static void EnterTerrain(PlanetFactory __instance, out BuildScope __state)
        {
            __state = building != null && ReferenceEquals(building.Factory, __instance) ? building : null;
            if (__state != null) __state.TerrainDepth++;
        }

        private static Exception LeaveTerrain(Exception __exception, BuildScope __state)
        {
            if (__state != null) __state.TerrainDepth--;
            return __exception;
        }

        private static bool SelectBuildingCandidates(NearColliderLogic __instance, [HarmonyArgument(0)] Vector3 center,
            [HarmonyArgument(1)] float radius, [HarmonyArgument(2)] int[] ids, ref int __result)
        {
            var context = building;
            if (queryingExpandedArea || context == null || context.TerrainDepth <= 0 || ids == null ||
                !ReferenceEquals(__instance, context.Factory.planet?.physics?.nearColliderLogic)) return true;
            // Filter before the game's removal, harvesting and effect loops. Keeping
            // a plant cannot accidentally award wood/stone while leaving it standing.
            if (expandedCandidates == null) expandedCandidates = new int[2048];
            int count;
            queryingExpandedArea = true;
            try
            {
                do
                {
                    float buildingReach = Math.Max(0f, radius);
                    foreach (var shape in context.Shapes)
                        buildingReach = Math.Max(buildingReach, (shape.Center - center).magnitude + shape.Reach);
                    count = __instance.GetVegetablesInAreaNonAlloc(center,
                        buildingReach + MaximumPrototypeReach() * MaximumLiveScale(context.Factory), expandedCandidates);
                    if (count < expandedCandidates.Length || expandedCandidates.Length >= 131072) break;
                    expandedCandidates = new int[expandedCandidates.Length * 2];
                } while (true);
            }
            finally { queryingExpandedArea = false; }
            int kept = 0;
            var pool = context.Factory.vegePool;
            if (pool == null) { __result = 0; return false; }
            // If the spatial query exhausts its largest buffer, scan the live pool
            // for this unusual build instead of silently losing nearby candidates.
            bool exhausted = count >= expandedCandidates.Length;
            int examined = exhausted ? Math.Min(context.Factory.vegeCursor, pool.Length) : Math.Min(count, expandedCandidates.Length);
            var seen = new HashSet<int>();
            for (int i = exhausted ? 1 : 0; i < examined && kept < ids.Length; i++)
            {
                int id = exhausted ? i : expandedCandidates[i];
                if (id <= 0 || id >= pool.Length || pool[id].id != id || !seen.Add(id)) continue;
                var plant = pool[id];
                if (plant.protoId == 9999) continue;
                var proto = LDB.veges?.Select(plant.protoId);
                bool known;
                bool overlaps = Touches(context.Shapes, proto?.prefabDesc?.colliders, plant, out known);
                if (!known)
                {
                    // Unknown geometry keeps the game's original candidate behavior.
                    if ((plant.pos - center).sqrMagnitude <= radius * radius) ids[kept++] = id;
                    continue;
                }
                if (overlaps) ids[kept++] = id;
            }
            __result = kept;
            return false;
        }

        internal static bool Touches(BuildingOverlapGeometry.Shape[] buildingShapes, ColliderData[] prototypes, VegeData plant, out bool known)
        {
            known = false;
            if (prototypes == null) return false;
            foreach (var prototype in prototypes)
            {
                if (!BuildingOverlapGeometry.Supported(prototype)) continue;
                known = true;
                var actual = BuildingOverlapGeometry.At(prototype, plant.pos, plant.rot, plant.scl);
                foreach (var body in buildingShapes)
                    if (BuildingOverlapGeometry.Overlaps(body, actual)) return true;
            }
            return false;
        }

        private static BuildingOverlapGeometry.Shape[] PrebuildGeometry(PlanetPhysics physics, PrebuildData prebuild, PrefabDesc desc)
        {
            var actual = new List<BuildingOverlapGeometry.Shape>();
            if (physics != null && prebuild.colliderId > 0)
            {
                var visited = new HashSet<int>();
                int colliderId = prebuild.colliderId;
                try
                {
                    while (colliderId > 0 && visited.Count < 64 && visited.Add(colliderId))
                    {
                        ColliderData collider = physics.GetColliderData(colliderId);
                        if (collider.objType != EObjectType.Prebuild || collider.objId != prebuild.id) break;
                        if (BuildingOverlapGeometry.Supported(collider))
                            actual.Add(new BuildingOverlapGeometry.Shape(collider.shape, collider.pos, collider.q, collider.ext, collider.radius));
                        colliderId = collider.link;
                    }
                }
                catch (IndexOutOfRangeException) { actual.Clear(); }
                catch (ArgumentOutOfRangeException) { actual.Clear(); }
                catch (NullReferenceException) { actual.Clear(); }
            }
            if (actual.Count != 0) return actual.ToArray();
            if (desc?.isInserter == true && desc.colliders != null && (prebuild.pos2 - prebuild.pos).sqrMagnitude > 1e-8f)
            {
                foreach (var collider in desc.colliders)
                    if (BuildingOverlapGeometry.Supported(collider))
                        actual.Add(BuildingOverlapGeometry.Spanning(collider, prebuild.pos, prebuild.rot, prebuild.pos2, prebuild.rot2));
                if (actual.Count != 0) return actual.ToArray();
            }
            return Shapes(desc, prebuild.pos, prebuild.rot, Vector3.one, true);
        }

        private static BuildingOverlapGeometry.Shape[] Shapes(PrefabDesc desc, Vector3 position, Quaternion rotation, Vector3 scale, bool buildingFallback = false)
        {
            if (desc == null) return new BuildingOverlapGeometry.Shape[0];
            ColliderData[] source = desc.colliders;
            if (buildingFallback && (source == null || source.Length == 0)) source = desc.buildColliders;
            var result = new List<BuildingOverlapGeometry.Shape>();
            if (source != null)
                foreach (var collider in source)
                    if (BuildingOverlapGeometry.Supported(collider))
                        result.Add(BuildingOverlapGeometry.At(collider, position, rotation, scale));
            if (buildingFallback && result.Count == 0 && desc.hasBuildCollider && BuildingOverlapGeometry.Supported(desc.buildCollider))
                result.Add(BuildingOverlapGeometry.At(desc.buildCollider, position, rotation, scale));
            return result.ToArray();
        }

        private static float MaximumPrototypeReach()
        {
            if (prototypeReach >= 0f) return prototypeReach;
            float maximum = 0f;
            var prototypes = LDB.veges?.dataArray;
            if (prototypes != null)
                foreach (var proto in prototypes)
                    if (proto != null && proto.ID != 9999)
                        foreach (var shape in Shapes(proto.prefabDesc, Vector3.zero, Quaternion.identity, Vector3.one))
                            maximum = Math.Max(maximum, shape.Center.magnitude + shape.Reach);
            prototypeReach = maximum;
            return maximum;
        }

        private static float MaximumLiveScale(PlanetFactory factory)
        {
            ScaleBound bound = scales.GetValue(factory, _ => new ScaleBound());
            var pool = factory.vegePool;
            if (pool == null) return bound.Maximum;
            // Cache within one construction tick, including blueprint batches.
            // Recheck on the next tick so gardening changes also update the reach.
            long tick = GameMain.gameTick;
            if (!ReferenceEquals(pool, bound.Pool) || bound.Cursor != factory.vegeCursor || bound.Tick != tick)
            {
                for (int id = 1; id < Math.Min(factory.vegeCursor, pool.Length); id++)
                    if (pool[id].id == id && pool[id].protoId != 9999)
                    {
                        float size = VegetationColliderGeometry.Largest(pool[id].scl);
                        if (!float.IsNaN(size) && !float.IsInfinity(size)) bound.Maximum = Math.Max(bound.Maximum, size);
                    }
                bound.Pool = pool;
                bound.Cursor = factory.vegeCursor;
                bound.Tick = tick;
            }
            return bound.Maximum;
        }
    }
}
