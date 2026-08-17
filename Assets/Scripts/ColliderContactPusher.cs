using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Active physics contact repulsion component. When colliders touch or overlap,
    /// applies immediate repulsive forces along the collision normal to push objects apart,
    /// completely preventing objects from clipping or interpenetrating into each other in the pile.
    /// </summary>
    public class ColliderContactPusher : MonoBehaviour
    {
        [Header("Contact Repulsion Settings")]
        [Tooltip("Force multiplier applied along contact normal to push touching colliders apart.")]
        [SerializeField] private float repulsionForce = 5.0f;

        [Tooltip("Extra impulse nudge applied on initial contact impact.")]
        [SerializeField] private float impactImpulse = 0.25f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (rb == null || collision.rigidbody == null) return;
            ApplyRepulsion(collision, impactImpulse, ForceMode.Impulse);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (rb == null || collision.rigidbody == null || rb.isKinematic) return;
            if (rb.IsSleeping()) return; // Don't wake sleeping rigidbodies!
            ApplyRepulsion(collision, repulsionForce, ForceMode.Acceleration);
        }

        private void ApplyRepulsion(Collision collision, float forceAmount, ForceMode mode)
        {
            if (rb == null || collision.contactCount == 0) return;

            // Use primary contact point to eliminate memory allocations and multi-point force spikes
            ContactPoint contact = collision.GetContact(0);

            // Only apply repulsion if objects are actually interpenetrating
            if (mode == ForceMode.Acceleration && contact.separation >= -0.001f) return;

            Vector3 pushDir = new Vector3(contact.normal.x, 0f, contact.normal.z);
            if (pushDir.sqrMagnitude < 1e-4f) return;

            pushDir.Normalize();
            rb.AddForceAtPosition(pushDir * forceAmount, contact.point, mode);
        }
    }
}
