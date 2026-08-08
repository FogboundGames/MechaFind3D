using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Builds a physics ragdoll at runtime on a Kenney "blocky character" model.
    /// Expected hierarchy: root/{leg-left, leg-right, torso/{arm-left, arm-right, head}}.
    /// Adds a Rigidbody + BoxCollider (sized from each part's mesh bounds) to every body part
    /// and CharacterJoints linking the head/arms/legs to the torso, so the character flops
    /// naturally when dropped and comes to rest in a sprawled lying pose.
    /// </summary>
    public static class RagdollBuilder
    {
        [System.Serializable]
        public struct Settings
        {
            [Tooltip("Heavier torso keeps the ragdoll stable; lighter limbs flop more freely.")]
            public float torsoMass;
            public float headMass;
            public float armMass;
            public float legMass;

            [Tooltip("How far limbs can swing away from their rest direction (degrees).")]
            public float swingLimit;
            [Tooltip("How far limbs can twist around their own axis (degrees).")]
            public float twistLimit;

            public static Settings Default => new Settings
            {
                torsoMass = 3f,
                headMass = 1f,
                armMass = 0.6f,
                legMass = 1f,
                swingLimit = 45f,
                twistLimit = 25f
            };
        }

        /// <summary>Builds the ragdoll using default tuning. Returns false if the hierarchy isn't a Kenney character.</summary>
        public static bool Build(GameObject character) => Build(character, Settings.Default);

        /// <summary>Builds the ragdoll with custom tuning. Idempotent: re-running reuses existing components.</summary>
        public static bool Build(GameObject character, Settings settings)
        {
            if (character == null) return false;

            Transform root = character.transform.Find("root");
            if (root == null) return false;

            Transform torso = root.Find("torso");
            if (torso == null) return false;

            Transform head = torso.Find("head");
            Transform armL = torso.Find("arm-left");
            Transform armR = torso.Find("arm-right");
            Transform legL = root.Find("leg-left");
            Transform legR = root.Find("leg-right");

            Rigidbody torsoRb = AddBody(torso, settings.torsoMass);
            AddBody(head, settings.headMass);
            AddBody(armL, settings.armMass);
            AddBody(armR, settings.armMass);
            AddBody(legL, settings.legMass);
            AddBody(legR, settings.legMass);

            // Head, arms and legs all hang off the torso, which is the ragdoll's anchor body.
            AddJoint(head, torsoRb, settings);
            AddJoint(armL, torsoRb, settings);
            AddJoint(armR, torsoRb, settings);
            AddJoint(legL, torsoRb, settings);
            AddJoint(legR, torsoRb, settings);

            return true;
        }

        private static Rigidbody AddBody(Transform t, float mass)
        {
            if (t == null) return null;

            BoxCollider col = t.GetComponent<BoxCollider>();
            if (col == null) col = t.gameObject.AddComponent<BoxCollider>();

            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // Local mesh bounds; the BoxCollider inherits the transform's scale automatically.
                col.center = mf.sharedMesh.bounds.center;
                col.size = mf.sharedMesh.bounds.size;
            }

            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.1f, mass);   // guard against a zero-mass part
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete; // continuous fights joints
            return rb;
        }

        private static void AddJoint(Transform t, Rigidbody connectedTo, Settings s)
        {
            if (t == null || connectedTo == null) return;

            CharacterJoint cj = t.GetComponent<CharacterJoint>();
            if (cj == null) cj = t.gameObject.AddComponent<CharacterJoint>();

            cj.connectedBody = connectedTo;

            SoftJointLimit low = cj.lowTwistLimit;   low.limit = -s.twistLimit; cj.lowTwistLimit = low;
            SoftJointLimit high = cj.highTwistLimit; high.limit = s.twistLimit;  cj.highTwistLimit = high;
            SoftJointLimit swing1 = cj.swing1Limit;  swing1.limit = s.swingLimit; cj.swing1Limit = swing1;
            SoftJointLimit swing2 = cj.swing2Limit;  swing2.limit = s.swingLimit; cj.swing2Limit = swing2;

            cj.enableProjection = true; // pulls parts back together instead of letting joints stretch
        }
    }
}
