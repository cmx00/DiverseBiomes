using System;
using UnityEngine;

namespace DiverseBiomes
{
    // Convex support mapping and a closest-simplex GJK search. A small inset
    // excludes mere contact, so a tree touching a building is left standing.
    internal static class BuildingOverlapGeometry
    {
        internal struct Shape
        {
            internal EColliderShape Kind;
            internal Vector3 Center, Extent;
            internal Quaternion Rotation;
            internal float Radius;

            internal Shape(EColliderShape kind, Vector3 center, Quaternion rotation, Vector3 extent, float radius)
            {
                Kind = kind; Center = center; Rotation = rotation;
                Extent = kind == EColliderShape.Capsule ? extent : new Vector3(Math.Abs(extent.x), Math.Abs(extent.y), Math.Abs(extent.z));
                Radius = Math.Max(0f, radius);
            }

            internal float Reach => Kind == EColliderShape.Sphere ? Radius :
                Kind == EColliderShape.Box ? Extent.magnitude : Extent.magnitude + Radius;
        }

        internal static bool Supported(ColliderData collider)
        {
            return collider.shape == EColliderShape.Sphere || collider.shape == EColliderShape.Capsule || collider.shape == EColliderShape.Box;
        }

        internal static Shape At(ColliderData prototype, Vector3 location, Quaternion rotation, Vector3 scale)
        {
            Vector3 absolute = new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
            return new Shape(prototype.shape, location + rotation * Vector3.Scale(prototype.pos, scale),
                rotation * prototype.q, Vector3.Scale(prototype.ext, absolute),
                prototype.radius * VegetationColliderGeometry.Largest(scale));
        }

        internal static Shape Spanning(ColliderData prototype, Vector3 first, Quaternion firstRotation,
            Vector3 second, Quaternion secondRotation)
        {
            Vector3 span = second - first;
            if (span.sqrMagnitude < 1e-8f) return At(prototype, first, firstRotation, Vector3.one);
            Vector3 forward = span.normalized;
            Vector3 up = firstRotation * Vector3.up + secondRotation * Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 1e-8f)
                right = Vector3.Cross(Math.Abs(forward.y) < .9f ? Vector3.up : Vector3.right, forward);
            right = right.normalized;
            up = Vector3.Cross(forward, right).normalized;
            Quaternion frame = Frame(right, up, forward);
            Shape shape = At(prototype, (first + second) * .5f, frame, Vector3.one);
            shape.Extent.z += span.magnitude * .5f;
            return shape;
        }

        private static Quaternion Frame(Vector3 x, Vector3 y, Vector3 z)
        {
            float trace = x.x + y.y + z.z;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(1f + trace) * 2f;
                return new Quaternion((y.z - z.y) / s, (z.x - x.z) / s, (x.y - y.x) / s, s * .25f);
            }
            if (x.x > y.y && x.x > z.z)
            {
                float s = (float)Math.Sqrt(1f + x.x - y.y - z.z) * 2f;
                return new Quaternion(s * .25f, (y.x + x.y) / s, (z.x + x.z) / s, (y.z - z.y) / s);
            }
            if (y.y > z.z)
            {
                float s = (float)Math.Sqrt(1f + y.y - x.x - z.z) * 2f;
                return new Quaternion((y.x + x.y) / s, s * .25f, (z.y + y.z) / s, (z.x - x.z) / s);
            }
            else
            {
                float s = (float)Math.Sqrt(1f + z.z - x.x - y.y) * 2f;
                return new Quaternion((z.x + x.z) / s, (z.y + y.z) / s, s * .25f, (x.y - y.x) / s);
            }
        }

        internal static bool Overlaps(Shape first, Shape second)
        {
            float reach = first.Reach + second.Reach;
            if ((first.Center - second.Center).sqrMagnitude >= reach * reach) return false;
            Vector3 direction = first.Center - second.Center;
            if (direction.sqrMagnitude < 1e-10f) direction = new Vector3(1f, .37f, .19f);
            Vector3[] simplex = new Vector3[4];
            simplex[0] = Difference(first, second, direction);
            int count = 1;
            Vector3 closest = simplex[0];
            for (int iteration = 0; iteration < 40; iteration++)
            {
                float distance = closest.sqrMagnitude;
                if (distance < 1e-8f) return true;
                Vector3 next = Difference(first, second, -closest);
                if (distance - Vector3.Dot(closest, next) <= 1e-7f * Math.Max(1f, distance)) return false;
                bool duplicate = false;
                for (int i = 0; i < count; i++) if ((simplex[i] - next).sqrMagnitude < 1e-12f) duplicate = true;
                if (duplicate || count == 4) return false;
                simplex[count++] = next;
                float best = float.PositiveInfinity;
                int bestMask = 0;
                Vector3 bestPoint = default(Vector3);
                for (int mask = 1; mask < (1 << count); mask++)
                {
                    Vector3 candidate;
                    if (!Project(simplex, count, mask, out candidate)) continue;
                    float error = candidate.sqrMagnitude;
                    if (error < best) { best = error; bestMask = mask; bestPoint = candidate; }
                }
                if (bestMask == 0) return false;
                int kept = 0;
                for (int i = 0; i < count; i++) if ((bestMask & (1 << i)) != 0) simplex[kept++] = simplex[i];
                count = kept;
                closest = bestPoint;
            }
            return false;
        }

        private static Vector3 Difference(Shape first, Shape second, Vector3 direction)
        {
            return Support(first, direction) - Support(second, -direction);
        }

        private static Vector3 Support(Shape shape, Vector3 direction)
        {
            const float inset = .0005f;
            float length = direction.magnitude;
            if (length < 1e-10f) return shape.Center;
            if (shape.Kind == EColliderShape.Sphere)
                return shape.Center + direction * (Math.Max(0f, shape.Radius - inset) / length);
            if (shape.Kind == EColliderShape.Capsule)
            {
                Vector3 spine = shape.Rotation * shape.Extent;
                return shape.Center + (Vector3.Dot(spine, direction) >= 0f ? spine : -spine) +
                    direction * (Math.Max(0f, shape.Radius - inset) / length);
            }
            Quaternion q = shape.Rotation;
            float norm = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            Quaternion inverse = norm < 1e-12f ? Quaternion.identity : new Quaternion(-q.x / norm, -q.y / norm, -q.z / norm, q.w / norm);
            Vector3 local = inverse * direction;
            Vector3 corner = new Vector3(
                Signed(Math.Max(0f, shape.Extent.x - inset), local.x),
                Signed(Math.Max(0f, shape.Extent.y - inset), local.y),
                Signed(Math.Max(0f, shape.Extent.z - inset), local.z));
            return shape.Center + shape.Rotation * corner;
        }

        private static float Signed(float size, float direction) => direction < 0f ? -size : size;

        private static bool Project(Vector3[] points, int count, int mask, out Vector3 projection)
        {
            int a = -1, b = -1, c = -1, d = -1;
            for (int i = 0; i < count; i++) if ((mask & (1 << i)) != 0)
            {
                if (a < 0) a = i; else if (b < 0) b = i; else if (c < 0) c = i; else d = i;
            }
            Vector3 origin = points[a];
            projection = origin;
            if (b < 0) return true;
            Vector3 x = points[b] - origin, y = c < 0 ? default(Vector3) : points[c] - origin,
                z = d < 0 ? default(Vector3) : points[d] - origin;
            double xx = Vector3.Dot(x, x), xy = Vector3.Dot(x, y), xz = Vector3.Dot(x, z),
                yy = Vector3.Dot(y, y), yz = Vector3.Dot(y, z), zz = Vector3.Dot(z, z);
            double rx = -Vector3.Dot(x, origin), ry = -Vector3.Dot(y, origin), rz = -Vector3.Dot(z, origin);
            double u, v = 0, w = 0;
            if (c < 0)
            {
                if (xx < 1e-18) return false;
                u = rx / xx;
            }
            else if (d < 0)
            {
                double determinant = xx * yy - xy * xy;
                if (Math.Abs(determinant) < 1e-12 * Math.Max(1.0, xx * yy)) return false;
                u = (rx * yy - ry * xy) / determinant;
                v = (ry * xx - rx * xy) / determinant;
            }
            else
            {
                double determinant = xx * (yy * zz - yz * yz) - xy * (xy * zz - yz * xz) + xz * (xy * yz - yy * xz);
                if (Math.Abs(determinant) < 1e-12 * Math.Max(1.0, xx * yy * zz)) return false;
                u = (rx * (yy * zz - yz * yz) - xy * (ry * zz - yz * rz) + xz * (ry * yz - yy * rz)) / determinant;
                v = (xx * (ry * zz - yz * rz) - rx * (xy * zz - yz * xz) + xz * (xy * rz - ry * xz)) / determinant;
                w = (xx * (yy * rz - ry * yz) - xy * (xy * rz - ry * xz) + rx * (xy * yz - yy * xz)) / determinant;
            }
            if (u < 0 || v < 0 || w < 0 || u + v + w > 1.0) return false;
            projection = d >= 0 ? Vector3.zero : origin + x * (float)u + y * (float)v;
            return true;
        }
    }
}
