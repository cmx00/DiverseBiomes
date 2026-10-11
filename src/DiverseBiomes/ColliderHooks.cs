using System;
using HarmonyLib;
using UnityEngine;

namespace DiverseBiomes
{
    internal static class ColliderHooks
    {
        internal static void Apply(Harmony harmony)
        {
            var bind = AccessTools.Method(typeof(ColliderData), nameof(ColliderData.BindToObject),
                new[] { typeof(int), typeof(int), typeof(EObjectType), typeof(Vector3), typeof(Quaternion), typeof(Vector3) });
            var register = AccessTools.Method(typeof(PlanetPhysics), nameof(PlanetPhysics.AddColliderData), new[] { typeof(ColliderData) });
            if (bind == null || bind.ReturnType != typeof(ColliderData) || register == null || register.ReturnType != typeof(int))
                throw new MissingMethodException("Vegetation collider registration API changed.");
            harmony.Patch(bind, prefix: new HarmonyMethod(typeof(ColliderHooks), nameof(UsePhysicalScale)),
                postfix: new HarmonyMethod(typeof(ColliderHooks), nameof(BoundVegetation)));
            harmony.Patch(register, prefix: new HarmonyMethod(typeof(ColliderHooks), nameof(RegisterVegetation)));
        }

        private static void BoundVegetation(ColliderData __instance, [HarmonyArgument(2)] EObjectType kind,
            [HarmonyArgument(5)] Vector3 scale, ref ColliderData __result)
        {
            if (kind != EObjectType.Vegetable || __result.objId <= 0) return;
            ColliderData source = __instance;
            VegetationColliderGeometry.CorrectScaledRadius(source, scale, ref __result);
        }

        private static bool RegisterVegetation([HarmonyArgument(0)] ColliderData collider, ref int __result)
        {
            if (!ModConfig.DisablePlayerVegetationCollision || collider.objType != EObjectType.Vegetable) return true;
            __result = 0;
            return false;
        }

        private static void UsePhysicalScale([HarmonyArgument(2)] EObjectType kind,
            [HarmonyArgument(5)] ref Vector3 scale)
        {
            if (kind != EObjectType.Vegetable) return;
            float cap = Math.Max(1f, ModConfig.MaximumPlayerColliderScale);
            scale = new Vector3(Capped(scale.x, cap), Capped(scale.y, cap), Capped(scale.z, cap));
        }

        private static float Capped(float value, float cap)
        {
            return value < 0f ? -Math.Min(-value, cap) : Math.Min(value, cap);
        }
    }
}
