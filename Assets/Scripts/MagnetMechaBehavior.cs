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

        private float originalHostMass = 1f;

        public void Initialize(MechaSpawnEntry entry)
        {
            radius = entry.magnetRadius;
            force = entry.magnetForce;
            maxObjects = entry.magnetMaxObjects;
            isActive = true;
        }

        private void Start()
        {
            // Host objeyi ağırlaştır — çekilen objelerin tepki kuvvetiyle fırlamasını önler
            if (transform.parent != null)
            {
                Rigidbody hostRb = transform.parent.GetComponent<Rigidbody>();
                if (hostRb != null)
                {
                    originalHostMass = hostRb.mass;
                    hostRb.mass = originalHostMass * 5f;
                }
            }
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
                    attractedBodies[i].linearDamping = 0.5f;
                    attractedBodies.RemoveAt(i);
                    continue;
                }

                float distCheck = Vector3.Distance(center, attractedBodies[i].position);
                if (distCheck > radius * 1.5f)
                {
                    attractedBodies[i].linearDamping = 0.5f;
                    attractedBodies.RemoveAt(i);
                    continue;
                }

                // Obje merkezden uzaklaşıyorsa (oyuncu sürüklüyorsa) bırak
                Vector3 toCenter = (center - attractedBodies[i].position).normalized;
                float awaySpeed = -Vector3.Dot(attractedBodies[i].linearVelocity, toCenter);
                if (awaySpeed > 1.5f)
                {
                    attractedBodies[i].linearDamping = 0.5f;
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
            Vector3 center = transform.parent != null ? transform.parent.position : transform.position;

            foreach (Rigidbody rb in attractedBodies)
            {
                if (rb == null) continue;
                rb.linearDamping = 0.5f;
                Vector3 away = (rb.position - center).normalized;
                if (away.sqrMagnitude < 0.01f)
                    away = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)).normalized;
                rb.AddForce((away + Vector3.up * 0.5f) * force * 1.5f, ForceMode.Impulse);
            }

            attractedBodies.Clear();
            isActive = false;

            // Host mass'ini normale döndür
            if (transform.parent != null)
            {
                Rigidbody hostRb = transform.parent.GetComponent<Rigidbody>();
                if (hostRb != null) hostRb.mass = originalHostMass;
            }
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
