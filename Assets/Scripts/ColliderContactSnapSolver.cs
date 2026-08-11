using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Rigid-body surface placement: positions a "mecha" object against a "target" object's real collider
    /// surface — starting at a caller-supplied seed (typically a host's Pivot_Top/Bottom/Left/Right, which
    /// already sits right at that collider's edge) and guaranteeing zero/near-zero penetration via
    /// Physics.ComputePenetration. Deterministic and closed-form: no iterative best-fit search. An earlier
    /// version tried to also multi-sample the surface and iteratively solve for a best-fit rotation and a
    /// percentile-based position correction; with only a handful of valid samples per pass on a spread-limb
    /// character, that normal/gap estimate was noisy enough to make the fit oscillate or drift away instead
    /// of converging. Trading that ambition for reliability: the mecha keeps its natural imported
    /// orientation and is placed exactly where the (now correctly-positioned) pivot says, nudged only the
    /// minimum needed to clear any overlap.
    /// </summary>
    public static class ColliderContactSnapSolver
    {
        [System.Serializable]
        public struct SnapSettings
        {
            [Tooltip("Sampling grid resolution (density x density rays) used for the post-placement contact/gap report.")]
            public int surfaceSampleDensity;
            [Tooltip("Desired final gap between the two surfaces, in world units.")]
            public float targetGap;
            [Tooltip("Any measured penetration deeper than this gets pushed out via Physics.ComputePenetration.")]
            public float penetrationTolerance;

            public static SnapSettings Default => new SnapSettings
            {
                surfaceSampleDensity = 9,
                targetGap = 0.004f,
                penetrationTolerance = 0.0015f,
            };
        }

        public struct SnapResult
        {
            public bool success;
            public Vector3 approachDirection;
            public int sampleCount;
            public int contactSampleCount;
            public int penetratingSampleCount;
            public float minGap;
            public float maxGap;
            public float averageGap;
            public float maxPenetration;
            public int iterations;
            public Vector3 finalPosition;
            public Quaternion finalRotation;
        }

        private struct ContactSample
        {
            public Vector3 mechaPoint;
            public Vector3 mechaNormal;
            public Vector3 targetPoint;
            public Vector3 targetNormal;
            public float gap; // >0 = separated, <0 = penetrating, measured along approach direction
        }

        public static bool DebugDrawEnabled = true;
        public static float DebugDrawDuration = 4f;
        public static bool LogResultEnabled = true;

        /// <summary>
        /// Places <paramref name="mecha"/> against <paramref name="target"/>. Both must already have their
        /// final colliders built. Sets mecha's world position directly (safe under any parent, any parent
        /// scale); rotation is left untouched (the mecha's natural imported/standing orientation).
        /// <paramref name="seedPositionWorld"/>, if given, is used as the target surface point (e.g. a
        /// host's Pivot_Top/Bottom/Left/Right); otherwise a generic point on the target's bounds along
        /// <paramref name="approachDirectionWorld"/> is used instead.
        /// </summary>
        public static SnapResult Solve(GameObject mecha, GameObject target, Vector3 approachDirectionWorld, SnapSettings settings, Vector3? seedPositionWorld = null)
        {
            SnapResult result = default;
            if (mecha == null || target == null) return result;

            Vector3 direction = approachDirectionWorld.sqrMagnitude > 1e-6f ? approachDirectionWorld.normalized : Vector3.up;
            result.approachDirection = direction;

            Collider[] mechaColliders = mecha.GetComponentsInChildren<Collider>(true);
            Collider[] targetColliders = target.GetComponentsInChildren<Collider>(true);
            if (mechaColliders.Length == 0 || targetColliders.Length == 0)
            {
                Debug.LogWarning($"[ColliderContactSnapSolver] '{mecha.name}' or '{target.name}' has no collider to fit against.");
                return result;
            }

            // 1. Seed: place mecha at the explicit surface pivot location if provided,
            // or compute a surface seed point along approach direction.
            Bounds mechaBounds = CombinedColliderBounds(mechaColliders);
            Bounds targetBounds = CombinedColliderBounds(targetColliders);

            if (seedPositionWorld.HasValue)
            {
                mecha.transform.position = seedPositionWorld.Value;
                Physics.SyncTransforms();
            }
            else
            {
                Vector3 surfacePoint = targetBounds.center + direction * ProjectExtent(targetBounds, direction);
                float mechaReach = ProjectExtent(mechaBounds, direction);
                Vector3 seedPos = surfacePoint + direction * (mechaReach + settings.targetGap);
                mecha.transform.position += seedPos - mechaBounds.center;
                RecenterOverTarget(mecha, mechaColliders, targetBounds, direction);
                Physics.SyncTransforms();
            }

            // 2. Hard constraint: guarantee zero/near-zero penetration using PhysX's own exact geometry
            // (not a sampling estimate) — the sole correction step, so there is nothing left to drift or
            // oscillate.
            ResolveAllPenetrations(mecha.transform, mechaColliders, targetColliders, settings.penetrationTolerance, maxPasses: 10);

            // Final measurement pass for reporting/debug only — does not feed back into positioning.
            List<ContactSample> samples = SampleGrid(mecha, target, mechaColliders, direction, settings.surfaceSampleDensity);
            BuildMetrics(ref result, samples, settings);
            result.iterations = 1;
            result.finalPosition = mecha.transform.position;
            result.finalRotation = mecha.transform.rotation;
            result.success = true;

            if (DebugDrawEnabled) DrawDebug(mecha, target, direction, samples);
            if (LogResultEnabled) LogResult(mecha, target, result);

            return result;
        }

        private static List<ContactSample> SampleGrid(GameObject mecha, GameObject target, Collider[] mechaColliders, Vector3 direction, int density)
        {
            density = Mathf.Max(1, density);
            List<ContactSample> results = new List<ContactSample>(density * density);

            Bounds mechaBounds = CombinedColliderBounds(mechaColliders);
            Bounds targetBoundsForMargin = CombinedColliderBounds(target.GetComponentsInChildren<Collider>(true));

            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(direction, arbitrary).normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;

            float halfW = ProjectExtent(mechaBounds, right) + 0.01f;
            float halfH = ProjectExtent(mechaBounds, up) + 0.01f;
            Vector3 gridCenter = mechaBounds.center;

            float margin = mechaBounds.size.magnitude + targetBoundsForMargin.size.magnitude + 1f;

            Physics.SyncTransforms();

            for (int iy = 0; iy < density; iy++)
            {
                float v = density == 1 ? 0f : (iy / (float)(density - 1) - 0.5f) * 2f;
                for (int ix = 0; ix < density; ix++)
                {
                    float u = density == 1 ? 0f : (ix / (float)(density - 1) - 0.5f) * 2f;
                    Vector3 lateral = right * (u * halfW) + up * (v * halfH);
                    Vector3 rayOrigin = gridCenter + lateral + direction * margin;

                    RaycastHit[] hits = Physics.RaycastAll(rayOrigin, -direction, margin * 2f, ~0, QueryTriggerInteraction.Collide);
                    if (hits.Length == 0) continue;
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    Vector3? mechaPoint = null, mechaNormal = null, targetPoint = null, targetNormal = null;
                    foreach (RaycastHit h in hits)
                    {
                        if (h.collider == null) continue;
                        if (!mechaPoint.HasValue && h.collider.transform.IsChildOf(mecha.transform))
                        {
                            mechaPoint = h.point;
                            mechaNormal = h.normal;
                        }
                        else if (!targetPoint.HasValue && h.collider.transform.IsChildOf(target.transform))
                        {
                            targetPoint = h.point;
                            targetNormal = h.normal;
                        }
                        if (mechaPoint.HasValue && targetPoint.HasValue) break;
                    }

                    if (mechaPoint.HasValue && targetPoint.HasValue)
                    {
                        float gap = Vector3.Dot(mechaPoint.Value, direction) - Vector3.Dot(targetPoint.Value, direction);
                        results.Add(new ContactSample
                        {
                            mechaPoint = mechaPoint.Value,
                            mechaNormal = mechaNormal.Value,
                            targetPoint = targetPoint.Value,
                            targetNormal = targetNormal.Value,
                            gap = gap
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Shifts mecha so its collider bounds centroid sits directly over the target's centroid, keeping
        /// only the component of separation along <paramref name="direction"/>.
        /// </summary>
        private static void RecenterOverTarget(GameObject mecha, Collider[] mechaColliders, Bounds targetBounds, Vector3 direction)
        {
            Bounds mechaBounds = CombinedColliderBounds(mechaColliders);
            Vector3 offset = mechaBounds.center - targetBounds.center;
            Vector3 lateralOffset = offset - Vector3.Project(offset, direction);
            mecha.transform.position -= lateralOffset;
        }

        /// <summary>
        /// Repeatedly finds the worst-overlapping (mecha collider, target collider) pair via
        /// Physics.ComputePenetration and pushes the mecha out along that pair's separation vector, looping
        /// until nothing overlaps by more than <paramref name="tolerance"/> or <paramref name="maxPasses"/>
        /// is hit. A single pass only ever fixes one pair; with ~10 separate per-limb mecha colliders,
        /// several can be independently overlapping the target at once (or fixing one can reveal/worsen
        /// another), so this is the hard "never end up inside the target" guarantee, not a single nudge.
        /// </summary>
        private static float ResolveAllPenetrations(Transform mecha, Collider[] mechaColliders, Collider[] targetColliders, float tolerance, int maxPasses = 6)
        {
            float lastWorst = 0f;
            bool movedAny = false;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                Vector3 worstDir = Vector3.zero;
                float worstDist = 0f;
                bool any = false;

                foreach (Collider mc in mechaColliders)
                {
                    if (mc == null) continue;
                    foreach (Collider tc in targetColliders)
                    {
                        if (tc == null) continue;
                        bool overlapping = Physics.ComputePenetration(
                            mc, mc.transform.position, mc.transform.rotation,
                            tc, tc.transform.position, tc.transform.rotation,
                            out Vector3 dir, out float dist);

                        if (overlapping && dist > worstDist)
                        {
                            worstDist = dist;
                            worstDir = dir;
                            any = true;
                        }
                    }
                }

                lastWorst = any ? worstDist : 0f;
                if (!any || worstDist <= tolerance) break;

                mecha.position += worstDir * (worstDist + tolerance);
                movedAny = true;
                Physics.SyncTransforms();
            }

            if (movedAny) Physics.SyncTransforms();
            return lastWorst;
        }

        private static void BuildMetrics(ref SnapResult result, List<ContactSample> samples, SnapSettings settings)
        {
            result.sampleCount = samples.Count;
            if (samples.Count == 0)
            {
                result.minGap = 0f;
                result.maxGap = 0f;
                result.averageGap = 0f;
                result.maxPenetration = 0f;
                result.contactSampleCount = 0;
                result.penetratingSampleCount = 0;
                return;
            }

            float min = float.MaxValue, max = float.MinValue, sum = 0f, maxPen = 0f;
            int contact = 0, penetrating = 0;
            foreach (ContactSample s in samples)
            {
                min = Mathf.Min(min, s.gap);
                max = Mathf.Max(max, s.gap);
                sum += s.gap;
                if (s.gap < -settings.penetrationTolerance)
                {
                    penetrating++;
                    maxPen = Mathf.Max(maxPen, -s.gap);
                }
                else if (s.gap <= settings.targetGap + 0.01f)
                {
                    contact++;
                }
            }

            result.minGap = min;
            result.maxGap = max;
            result.averageGap = sum / samples.Count;
            result.maxPenetration = maxPen;
            result.contactSampleCount = contact;
            result.penetratingSampleCount = penetrating;
        }

        private static Bounds CombinedColliderBounds(Collider[] colliders)
        {
            Bounds b = default;
            bool has = false;
            foreach (Collider c in colliders)
            {
                if (c == null) continue;
                if (!has) { b = c.bounds; has = true; }
                else b.Encapsulate(c.bounds);
            }
            return has ? b : new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        private static float ProjectExtent(Bounds b, Vector3 axis)
        {
            return Mathf.Abs(b.extents.x * axis.x) + Mathf.Abs(b.extents.y * axis.y) + Mathf.Abs(b.extents.z * axis.z);
        }

        private static void DrawDebug(GameObject mecha, GameObject target, Vector3 direction, List<ContactSample> samples)
        {
            Bounds mechaBounds = CombinedColliderBounds(mecha.GetComponentsInChildren<Collider>(true));
            Debug.DrawRay(mechaBounds.center, direction * 0.3f, Color.yellow, DebugDrawDuration);

            const float mark = 0.01f;
            foreach (ContactSample s in samples)
            {
                Debug.DrawLine(s.mechaPoint - Vector3.right * mark, s.mechaPoint + Vector3.right * mark, Color.cyan, DebugDrawDuration);
                Debug.DrawLine(s.mechaPoint - Vector3.up * mark, s.mechaPoint + Vector3.up * mark, Color.cyan, DebugDrawDuration);
                Debug.DrawRay(s.mechaPoint, s.mechaNormal * 0.05f, Color.blue, DebugDrawDuration);

                Debug.DrawLine(s.targetPoint - Vector3.right * mark, s.targetPoint + Vector3.right * mark, Color.green, DebugDrawDuration);
                Debug.DrawLine(s.targetPoint - Vector3.up * mark, s.targetPoint + Vector3.up * mark, Color.green, DebugDrawDuration);
                Debug.DrawRay(s.targetPoint, s.targetNormal * 0.05f, Color.magenta, DebugDrawDuration);

                Color linkColor = s.gap < 0f ? Color.red : new Color(1f, 1f, 1f, 0.3f);
                Debug.DrawLine(s.mechaPoint, s.targetPoint, linkColor, DebugDrawDuration);
            }
        }

        private static void LogResult(GameObject mecha, GameObject target, SnapResult r)
        {
            Debug.Log(
                "[SurfaceSnap]\n" +
                $"Target: {target.name}\n" +
                $"Mecha: {mecha.name}\n" +
                $"Approach: {r.approachDirection}\n" +
                $"Samples: {r.sampleCount}\n" +
                $"Contact Samples: {r.contactSampleCount}\n" +
                $"Average Gap: {r.averageGap:F4}\n" +
                $"Max Gap: {r.maxGap:F4}\n" +
                $"Penetration: {r.maxPenetration:F4}\n" +
                $"Iterations: {r.iterations}\n" +
                $"Final Position: {r.finalPosition}\n" +
                $"Final Rotation: {r.finalRotation.eulerAngles}");
        }
    }
}
