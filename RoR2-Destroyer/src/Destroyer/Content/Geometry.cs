using RoR2;
using UnityEngine;

namespace Destroyer.Content
{
    /// <summary>Расстояния до тел — общее для предметов "вплотную" (чоризо, лисий хвост).</summary>
    internal static class Geometry
    {
        /// <summary>
        /// Ближайшая к точке поверхность тела: по хитбоксам (у червей их много), иначе — центр минус радиус.
        /// Для коробок/сфер/капсул/выпуклых мешей — точно, для остальных — по габаритной коробке.
        /// </summary>
        public static Vector3 ClosestSurfacePoint(CharacterBody body, Vector3 from)
        {
            var group = body.hurtBoxGroup;
            if (group && group.hurtBoxes != null && group.hurtBoxes.Length > 0)
            {
                Vector3 best = body.corePosition;
                float bestSqr = float.MaxValue;
                foreach (var hurtBox in group.hurtBoxes)
                {
                    if (!hurtBox) continue;
                    var collider = hurtBox.collider;
                    if (!collider || !collider.enabled) continue;
                    var meshCollider = collider as MeshCollider;
                    var point = meshCollider && !meshCollider.convex ? collider.bounds.ClosestPoint(from) : collider.ClosestPoint(from);
                    float sqr = (point - from).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = point;
                    }
                }
                if (bestSqr < float.MaxValue) return best;
            }

            var center = body.corePosition;
            var direction = from - center;
            float distance = direction.magnitude;
            float radius = body.bestFitRadius;
            return distance <= radius || distance < 0.001f ? from : center + direction / distance * radius;
        }

        /// <summary>Расстояние от точки до поверхности тела.</summary>
        public static float DistanceToSurface(CharacterBody body, Vector3 from) =>
            Vector3.Distance(ClosestSurfacePoint(body, from), from);

        /// <summary>Враги ли эти два тела (разные команды, обе известны).</summary>
        public static bool AreEnemies(CharacterBody a, CharacterBody b) =>
            a && b && a.teamComponent && b.teamComponent && a.teamComponent.teamIndex != b.teamComponent.teamIndex
            && a.teamComponent.teamIndex != TeamIndex.None && b.teamComponent.teamIndex != TeamIndex.None;
    }
}
