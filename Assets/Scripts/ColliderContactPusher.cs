using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Active physics contact repulsion component. When colliders touch or overlap,
    /// applies immediate repulsive forces along the collision normal to push objects apart,
    /// completely preventing objects from clipping or interpenetrating into each other in the pile.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
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
            if (collision.rigidbody == null) return;
            ApplyRepulsion(collision, impactImpulse, ForceMode.Impulse);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.rigidbody == null) return;
            ApplyRepulsion(collision, repulsionForce, ForceMode.Acceleration);
        }

        private void ApplyRepulsion(Collision collision, float forceAmount, ForceMode mode)
        {
            int contacts = collision.contactCount;
            if (contacts == 0) return;

            for (int i = 0; i < contacts; i++)
            {
                ContactPoint contact = collision.GetContact(i);

                // Keep repulsion purely horizontal (XZ plane) so stacked objects don't get pushed upward into the air
                Vector3 pushDir = new Vector3(contact.normal.x, 0f, contact.normal.z);
                if (pushDir.sqrMagnitude < 1e-4f) continue;

                pushDir.Normalize();

                // Push this object away horizontally
                rb.AddForceAtPosition(pushDir * forceAmount, contact.point, mode);
            }
        }
    }
}
