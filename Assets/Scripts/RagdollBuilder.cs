using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Builds a physics ragdoll at runtime on character models (Kenney characters or custom GLB/FBX mechas like meccha chameleon).
    /// Dynamically finds body parts (head, torso, arms, legs) and adds Rigidbodies, BoxColliders, and CharacterJoints
    /// so the character flops naturally when dropped into the pile.
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

        /// <summary>Builds the ragdoll using default tuning.</summary>
        public static bool Build(GameObject character) => Build(character, Settings.Default);

        /// <summary>Builds the ragdoll with custom tuning. Idempotent: re-running reuses existing components.</summary>
        public static bool Build(GameObject character, Settings settings)
        {
            if (character == null) return false;

            Transform root = character.transform.Find("root");
            Transform torso = root != null ? root.Find("torso") : null;
            Transform head = torso != null ? torso.Find("head") : null;
            Transform armL = torso != null ? torso.Find("arm-left") : null;
            Transform armR = torso != null ? torso.Find("arm-right") : null;
            Transform legL = root != null ? root.Find("leg-left") : null;
            Transform legR = root != null ? root.Find("leg-right") : null;

            // Flexible search fallback for custom mecha models (like meccha chameleon.glb)
            if (torso == null) torso = FindChildByKeywords(character.transform, "torso", "spine", "chest", "hips", "body");
            if (head == null) head = FindChildByKeywords(character.transform, "head", "neck");
            if (armL == null) armL = FindChildByKeywords(character.transform, "arm-left", "arm_l", "leftarm", "arm.l", "l_arm");
            if (armR == null) armR = FindChildByKeywords(character.transform, "arm-right", "arm_r", "rightarm", "arm.r", "r_arm");
            if (legL == null) legL = FindChildByKeywords(character.transform, "leg-left", "leg_l", "leftleg", "leg.l", "l_leg");
            if (legR == null) legR = FindChildByKeywords(character.transform, "leg-right", "leg_r", "rightleg", "leg.r", "r_leg");

            // If still no torso found, use main mesh renderer transform as torso anchor
            if (torso == null)
            {
                Renderer r = character.GetComponentInChildren<Renderer>();
                if (r != null) torso = r.transform;
                else torso = character.transform;
            }

            Rigidbody torsoRb = AddBody(torso, settings.torsoMass);
            if (head != null) { AddBody(head, settings.headMass); AddJoint(head, torsoRb, settings); }
            if (armL != null) { AddBody(armL, settings.armMass); AddJoint(armL, torsoRb, settings); }
            if (armR != null) { AddBody(armR, settings.armMass); AddJoint(armR, torsoRb, settings); }
            if (legL != null) { AddBody(legL, settings.legMass); AddJoint(legL, torsoRb, settings); }
            if (legR != null) { AddBody(legR, settings.legMass); AddJoint(legR, torsoRb, settings); }

            // If no child limbs were found by bone name, build joints on all child mesh renderers
            if (head == null && armL == null && armR == null && legL == null && legR == null)
            {
                foreach (Renderer rend in character.GetComponentsInChildren<Renderer>())
                {
                    if (rend.transform != torso)
                    {
                        AddBody(rend.transform, settings.armMass);
                        AddJoint(rend.transform, torsoRb, settings);
                    }
                }
            }

            return true;
        }

        private static Transform FindChildByKeywords(Transform parent, params string[] keywords)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            {
                string nameLower = t.name.ToLowerInvariant();
                foreach (string kw in keywords)
                {
                    if (nameLower.Contains(kw)) return t;
                }
            }
            return null;
        }

        private static Rigidbody AddBody(Transform t, float mass)
        {
            if (t == null) return null;

            BoxCollider col = t.GetComponent<BoxCollider>();
            if (col == null) col = t.gameObject.AddComponent<BoxCollider>();

            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                col.center = mf.sharedMesh.bounds.center;
                col.size = mf.sharedMesh.bounds.size;
            }

            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.1f, mass);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
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

            cj.enableProjection = true;
        }
    }
}
