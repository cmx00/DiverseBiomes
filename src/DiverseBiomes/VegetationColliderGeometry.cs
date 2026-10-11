using System;
using UnityEngine;

namespace DiverseBiomes
{
    internal static class VegetationColliderGeometry
    {
        internal static void CorrectScaledRadius(ColliderData source, Vector3 scale, ref ColliderData bound)
        {
            if (source.shape != EColliderShape.Sphere && source.shape != EColliderShape.Capsule) return;
            bound.radius = source.radius * Largest(scale);
        }

        internal static void LimitPhysicsColliderScale(ColliderData original, Vector3 visualScale,
            float maximumScale, ref ColliderData bound)
        {
            float cap = Math.Max(1f, maximumScale);
            if (bound.shape == EColliderShape.Box)
            {
                Vector3 ratios = new Vector3(Ratio(visualScale.x, cap), Ratio(visualScale.y, cap), Ratio(visualScale.z, cap));
                bound.ext = Vector3.Scale(bound.ext, ratios);
            }
            else if (bound.shape == EColliderShape.Sphere || bound.shape == EColliderShape.Capsule)
            {
                float size = Largest(visualScale);
                float ratio = size > cap ? cap / size : 1f;
                bound.ext *= ratio;
                bound.radius = original.radius * Math.Min(size, cap);
            }
        }

        internal static float Largest(Vector3 scale)
        {
            return Math.Max(Math.Abs(scale.x), Math.Max(Math.Abs(scale.y), Math.Abs(scale.z)));
        }

        private static float Ratio(float size, float cap)
        {
            size = Math.Abs(size);
            return size > cap ? cap / size : 1f;
        }
    }
}
