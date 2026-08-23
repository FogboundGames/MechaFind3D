using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Mıknatıs mecha: etrafındaki pile objelerini fizik kuvvetiyle üstüne çekerek
    /// kendini gizler. Oyuncu objeleri topladıkça kalkan azalır, mecha ortaya çıkar.
    /// </summary>
    public class MagnetMechaBehavior : MonoBehaviour
    {
        [Header("Config")]
        public float radius = 1.5f;
        public float force = 8f;
        public int maxObjects = 4;

        [Header("State")]
        public bool isActive = true;

        private readonly List<Rigidbody> attractedBodies = new List<Rigidbody>();
        private float scanTimer;
        private const float ScanInterval = 0.5f;

        public void Initialize(MechaSpawnEntry entry)
        {
            radius = entry.magnetRadius;
            force = entry.magnetForce;
            maxObjects = entry.magnetMaxObjects;
            isActive = true;
        }

        private void FixedUpdate()
        {
            if (!isActive) return;

            MechaRunnerBehavior runner = GetComponent<MechaRunnerBehavior>();
            if (runner != null && runner.currentState != MechaRunnerBehavior.MechaState.CamouflagedOnHost)
            {
                ReleaseAll();
                return;
            }

            // Mecha is child of host — use host (parent) position as magnet center
            Vector3 center = transform.parent != null ? transform.parent.position : transform.position;

            // Periodic scan for new objects to attract
            scanTimer -= Time.fixedDeltaTime;
            if (scanTimer <= 0f)
            {
                ScanForObjects(center);
                scanTimer = ScanInterval;
            }

            // Clean up destroyed or docked objects
            for (int i = attractedBodies.Count - 1; i >= 0; i--)
            {
                if (attractedBodies[i] == null)
                {
                    attractedBodies.RemoveAt(i);
                    continue;
                }

                FindTargetObject fto = attractedBodies[i].GetComponent<FindTargetObject>();
                if (fto != null && fto.isDocked)
                {
                    attractedBodies.RemoveAt(i);
                    continue;
                }
            }

            // Apply attraction force
            foreach (Rigidbody rb in attractedBodies)
            {
                if (rb == null || rb.isKinematic) continue;

                Vector3 dir = center - rb.position;
                float dist = dir.magnitude;

                if (dist > radius * 1.5f) continue;

                // Close range: snap hard to center, high drag to stick
                if (dist < 0.3f)
                {
                    rb.linearVelocity = dir.normalized * force * 0.5f;
                    rb.linearDamping = 8f;
                }
                else
                {
                    rb.linearDamping = 0.5f;
                    float strength = force * (1f + 2f / Mathf.Max(dist, 0.05f));
                    rb.AddForce(dir.normalized * strength, ForceMode.Acceleration);
                }
            }
        }

        private void ScanForObjects(Vector3 center)
        {
            if (attractedBodies.Count >= maxObjects) return;

            FindTargetObject[] pileItems = FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            if (pileItems == null) return;

            // Sort by distance, pick closest ones
            List<(FindTargetObject fto, float dist)> candidates = new List<(FindTargetObject, float)>();

            foreach (FindTargetObject fto in pileItems)
            {
                if (fto == null || fto.isDocked) continue;
                if (fto.GetComponentInChildren<MechaRunnerBehavior>() != null) continue;

                // Skip the host object this mecha is embedded in
                if (transform.parent != null && fto.transform == transform.parent) continue;

                Rigidbody rb = fto.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;

                // Skip if already attracted
                if (attractedBodies.Contains(rb)) continue;

                float dist = Vector3.Distance(center, fto.transform.position);
                if (dist <= radius)
                {
                    candidates.Add((fto, dist));
                }
            }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            int slotsLeft = maxObjects - attractedBodies.Count;
            for (int i = 0; i < Mathf.Min(slotsLeft, candidates.Count); i++)
            {
                Rigidbody rb = candidates[i].fto.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    attractedBodies.Add(rb);
                }
            }
        }

        private void ReleaseAll()
        {
            attractedBodies.Clear();
            isActive = false;
        }

        public void Stop()
        {
            ReleaseAll();
        }

        private void OnDestroy()
        {
            attractedBodies.Clear();
        }
    }
}
