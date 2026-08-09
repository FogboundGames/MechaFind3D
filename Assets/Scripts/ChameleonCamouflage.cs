using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// The "chameleon" mechanic: repaints a character's body parts with the pile's appearance and textures,
    /// so the hidden mecha blends perfectly into the crowd of objects.
    /// Can also embed/attach the mecha INTO a host object (like a Cake 🍰 or Apple 🍎) as a hybrid disguise!
    /// </summary>
    public static class ChameleonCamouflage
    {
        /// The default toy pile palette — used as fallback if no custom materials/colors exist.
        public static readonly Color[] DefaultPalette =
        {
            new Color(0.95f, 0.20f, 0.20f), // Kırmızı
            new Color(0.20f, 0.55f, 0.95f), // Mavi
            new Color(0.20f, 0.85f, 0.35f), // Yeşil
            new Color(0.98f, 0.85f, 0.15f), // Sarı
            new Color(0.65f, 0.25f, 0.90f), // Mor
            new Color(0.98f, 0.50f, 0.15f), // Turuncu
            new Color(0.15f, 0.85f, 0.85f), // Turkuaz
            new Color(0.95f, 0.40f, 0.70f), // Pembe
        };

        /// <summary>
        /// Gives the mecha a fixed, light-white see-through glass look — the host object underneath stays
        /// clearly visible through it, while the mecha's own silhouette still reads as a subtle white shape.
        /// Applied unconditionally so every mecha gets this appearance regardless of which disguise/camouflage
        /// mode ran (or whether embedding into a host object even happened).
        /// </summary>
        public static void ApplyGlassMaterial(GameObject mecha, float opacity = 0.22f)
        {
            if (mecha == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material glassMat = new Material(shader);
            glassMat.name = "CrystalGlassMechaMat";

            // Transparent Glass Configuration.
            // NOTE: setting _Surface/_Mode alone only records the *intent* to be transparent — in the
            // Editor, toggling "Transparent" on a Lit material's Inspector also runs shader-GUI code that
            // translates that into the actual GPU blend state (_SrcBlend/_DstBlend/_ZWrite/keywords). That
            // translation never runs for materials built purely from script, so without setting these by
            // hand the material silently keeps rendering fully opaque no matter what alpha is used.
            if (glassMat.HasProperty("_Surface")) glassMat.SetFloat("_Surface", 1.0f); // URP: 1 = Transparent
            if (glassMat.HasProperty("_Blend")) glassMat.SetFloat("_Blend", 0.0f);     // URP: 0 = Alpha blend
            if (glassMat.HasProperty("_Mode")) glassMat.SetFloat("_Mode", 3.0f);       // Built-in Standard: 3 = Transparent
            if (glassMat.HasProperty("_AlphaClip")) glassMat.SetFloat("_AlphaClip", 0.0f);

            if (glassMat.HasProperty("_SrcBlend")) glassMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (glassMat.HasProperty("_DstBlend")) glassMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (glassMat.HasProperty("_ZWrite")) glassMat.SetFloat("_ZWrite", 1.0f); // Enable ZWrite so 3D glass contours stay 100% visible

            glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.DisableKeyword("_ALPHATEST_ON");
            glassMat.EnableKeyword("_ALPHABLEND_ON");
            glassMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // High Glass Glossiness & Specular Reflection
            if (glassMat.HasProperty("_Smoothness")) glassMat.SetFloat("_Smoothness", 0.95f); // High glass gloss
            if (glassMat.HasProperty("_Metallic")) glassMat.SetFloat("_Metallic", 0.15f);     // Glass edge specular shine

            // Clear, visible glass tint with guaranteed visibility alpha (0.50f)
            float fixedGlassAlpha = Mathf.Clamp(opacity < 0.10f ? 0.50f : opacity, 0.40f, 0.85f);
            Color crystalGlassTint = new Color(0.95f, 0.98f, 1.0f, fixedGlassAlpha);
            if (glassMat.HasProperty("_BaseColor")) glassMat.SetColor("_BaseColor", crystalGlassTint);
            if (glassMat.HasProperty("_Color")) glassMat.SetColor("_Color", crystalGlassTint);

            // Clear textures so the glass is crystal clear see-through
            if (glassMat.HasProperty("_BaseMap")) glassMat.SetTexture("_BaseMap", null);
            if (glassMat.HasProperty("_MainTex")) glassMat.SetTexture("_MainTex", null);
            glassMat.mainTexture = null;

            foreach (Renderer r in mecha.GetComponentsInChildren<Renderer>())
            {
                int slotCount = Mathf.Max(1, r.sharedMaterials.Length);
                Material[] newMats = new Material[slotCount];
                for (int m = 0; m < slotCount; m++)
                {
                    newMats[m] = glassMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        /// <summary>
        /// Embeds/Attaches the mecha INTO a host item (e.g. a Cake 🍰), positioning its upper body/limbs
        /// emerging from the top of the host object, repainted in the host item's exact texture/materials!
        /// </summary>
        public static void EmbedMechaInHostObject(GameObject mecha, GameObject hostObject, float scaleRatio = 0.85f, float opacity = 0.22f, Vector3 positionOffset = default, Vector3 rotationOffset = default)
        {
            if (mecha == null || hostObject == null) return;

            // 1. Keep host object (e.g. Cake) upright flat on table
            hostObject.transform.rotation = Quaternion.identity;

            Rigidbody hostRb = hostObject.GetComponent<Rigidbody>();
            if (hostRb != null)
            {
                // Freeze X and Z rotation so the cake never rolls sideways or upside down
                hostRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            // 2. Extract host item materials (e.g. Cake frosting and layer textures)
            Renderer[] hostRenderers = hostObject.GetComponentsInChildren<Renderer>();
            List<Material> hostMaterials = new List<Material>();
            Texture mainHostTex = null;

            foreach (Renderer hr in hostRenderers)
            {
                if (hr != null && hr.sharedMaterial != null)
                {
                    if (!hostMaterials.Contains(hr.sharedMaterial)) hostMaterials.Add(hr.sharedMaterial);

                    if (mainHostTex == null)
                    {
                        if (hr.sharedMaterial.HasProperty("_BaseMap") && hr.sharedMaterial.GetTexture("_BaseMap") != null)
                            mainHostTex = hr.sharedMaterial.GetTexture("_BaseMap");
                        else if (hr.sharedMaterial.HasProperty("_MainTex") && hr.sharedMaterial.GetTexture("_MainTex") != null)
                            mainHostTex = hr.sharedMaterial.GetTexture("_MainTex");
                        else if (hr.sharedMaterial.mainTexture != null)
                            mainHostTex = hr.sharedMaterial.mainTexture;
                    }
                }
            }

            // 3. Apply uniform CRYSTAL GLASS material (İçini %100 kristal netliğinde gösteren şeffaf cam)
            ApplyGlassMaterial(mecha, opacity);

            // 4. Spread arms and legs out in Vitruvian pose (matching user's drawing)
            ApplyVitruvianSpreadPose(mecha.transform);

            // 5. Compute bounds & position mecha FLAT ON TOP of the host object face
            Bounds hostBounds = GetCombinedBounds(hostObject);
            float hostExtent = hostBounds.size.magnitude;
            if (hostExtent < 1e-4f) hostExtent = 1.0f;

            // Measure unscaled mecha model size
            mecha.transform.localScale = Vector3.one;
            float rawMechaExtent = GetCombinedBounds(mecha).size.magnitude;
            if (rawMechaExtent < 1e-4f) rawMechaExtent = 2.0f;

            // Target mecha world size relative to host object
            float effectiveScaleRatio = Mathf.Clamp(scaleRatio, 0.25f, 1.20f);
            float targetMechaWorldSize = hostExtent * effectiveScaleRatio;
            float desiredWorldScale = targetMechaWorldSize / rawMechaExtent;

            // Parent mecha directly to host object transform for 100% synchronized movement
            mecha.transform.SetParent(hostObject.transform, false);

            // Cancel out parent local scale so mecha is NEVER shrunk to microscopic dot!
            Vector3 parentLossy = hostObject.transform.lossyScale;
            float px = Mathf.Abs(parentLossy.x) > 1e-4f ? Mathf.Abs(parentLossy.x) : 1f;
            float py = Mathf.Abs(parentLossy.y) > 1e-4f ? Mathf.Abs(parentLossy.y) : 1f;
            float pz = Mathf.Abs(parentLossy.z) > 1e-4f ? Mathf.Abs(parentLossy.z) : 1f;

            mecha.transform.localScale = new Vector3(desiredWorldScale / px, desiredWorldScale / py, desiredWorldScale / pz);

            // Position mecha lying FLAT (90 deg X-rotation) directly glued on the top surface of the host object
            Vector3 defaultRot = (rotationOffset == Vector3.zero) ? new Vector3(90f, 0f, 0f) : rotationOffset;
            mecha.transform.localPosition = new Vector3(0f, 0.05f, 0f) + positionOffset;
            mecha.transform.localRotation = Quaternion.Euler(defaultRot);

            // Remove all joints, colliders, and rigidbodies in proper dependency order
            // (Joints must be removed FIRST because CharacterJoint requires Rigidbody)
            foreach (Joint j in mecha.GetComponentsInChildren<Joint>())
            {
                Object.Destroy(j);
            }
            foreach (Collider col in mecha.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(col);
            }
            foreach (Rigidbody rb in mecha.GetComponentsInChildren<Rigidbody>())
            {
                Object.Destroy(rb);
            }
        }

        private static void ApplyVitruvianSpreadPose(Transform mechaRoot)
        {
            foreach (Transform t in mechaRoot.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("arm-left") || n.Contains("arm_l") || n.Contains("leftarm") || n.Contains("arm.l") || n.Contains("l_arm"))
                {
                    t.localRotation = Quaternion.Euler(0f, 0f, -40f);
                }
                else if (n.Contains("arm-right") || n.Contains("arm_r") || n.Contains("rightarm") || n.Contains("arm.r") || n.Contains("r_arm"))
                {
                    t.localRotation = Quaternion.Euler(0f, 0f, 40f);
                }
                else if (n.Contains("leg-left") || n.Contains("leg_l") || n.Contains("leftleg") || n.Contains("leg.l") || n.Contains("l_leg"))
                {
                    t.localRotation = Quaternion.Euler(0f, 0f, -20f);
                }
                else if (n.Contains("leg-right") || n.Contains("leg_r") || n.Contains("rightleg") || n.Contains("leg.r") || n.Contains("r_leg"))
                {
                    t.localRotation = Quaternion.Euler(0f, 0f, 20f);
                }
            }
        }

        private static Bounds GetCombinedBounds(GameObject obj)
        {
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.5f);

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                b.Encapsulate(rends[i].bounds);
            }
            return b;
        }

        /// <summary>
        /// Repaints every renderer under <paramref name="character"/> using flat colors from <paramref name="palette"/>.
        /// </summary>
        public static void Apply(GameObject character, IReadOnlyList<Color> palette = null,
                                 bool perPartColor = true, float smoothness = 0.65f)
        {
            if (character == null) return;
            if (palette == null || palette.Count == 0) palette = DefaultPalette;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Color bodyColor = palette[Random.Range(0, palette.Count)];

            foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
            {
                Color c = perPartColor ? palette[Random.Range(0, palette.Count)] : bodyColor;

                Material mat = new Material(shader) { name = "CamouflageMaterial" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

                r.material = mat;
            }
        }

        /// <summary>
        /// Real texture/material matching: copies the ACTUAL materials and textures worn by live pile objects
        /// (including food kit textures, patterns, and colors) onto each of the mecha's body parts.
        /// </summary>
        public static void ApplyTexturedFromPile(GameObject character)
        {
            if (character == null) return;

            List<Material> mats = SamplePileMaterials();

            if (mats.Count == 0) mats.AddRange(AppearanceLibrary.All);
            if (mats.Count == 0) { Apply(character); return; }

            foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mats[Random.Range(0, mats.Count)];
            }
        }

        /// <summary>
        /// Disguises the mecha to look like a SPECIFIC target object prefab (e.g. Burger, Apple, Toy Car).
        /// Applies the object's exact textures/materials to all limbs, and optionally attaches a mini
        /// 3D mascot costume item onto the mecha's head!
        /// </summary>
        public static void ApplyObjectDisguise(GameObject character, GameObject targetObjectPrefab, bool attachHeadMascot = true)
        {
            if (character == null) return;

            if (targetObjectPrefab != null)
            {
                Renderer[] targetRends = targetObjectPrefab.GetComponentsInChildren<Renderer>();
                List<Material> targetMats = new List<Material>();
                foreach (Renderer tr in targetRends)
                {
                    if (tr != null && tr.sharedMaterial != null)
                    {
                        targetMats.Add(tr.sharedMaterial);
                    }
                }

                if (targetMats.Count > 0)
                {
                    foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = targetMats[Random.Range(0, targetMats.Count)];
                    }
                }
                else
                {
                    ApplyTexturedFromPile(character);
                }

                if (attachHeadMascot)
                {
                    AttachMascotToHead(character, targetObjectPrefab);
                }
            }
            else
            {
                ApplyTexturedFromPile(character);
            }
        }

        /// <summary>
        /// Picks one random item from the live pile and disguises the mecha as that item type!
        /// </summary>
        public static void ApplyDisguiseFromLivePileItem(GameObject character, bool attachHeadMascot = true)
        {
            if (character == null) return;

            FindTargetObject[] pileObjects = Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            if (pileObjects != null && pileObjects.Length > 0)
            {
                List<FindTargetObject> available = new List<FindTargetObject>();
                foreach (var f in pileObjects)
                {
                    if (!f.isDocked) available.Add(f);
                }

                if (available.Count > 0)
                {
                    FindTargetObject chosen = available[Random.Range(0, available.Count)];
                    Renderer[] targetRends = chosen.GetComponentsInChildren<Renderer>();
                    List<Material> mats = new List<Material>();
                    foreach (Renderer r in targetRends)
                    {
                        if (r != null && r.sharedMaterial != null) mats.Add(r.sharedMaterial);
                    }

                    if (mats.Count > 0)
                    {
                        foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
                        {
                            r.sharedMaterial = mats[Random.Range(0, mats.Count)];
                        }
                    }

                    if (attachHeadMascot)
                    {
                        AttachMascotToHead(character, chosen.gameObject);
                    }
                    return;
                }
            }

            ApplyTexturedFromPile(character);
        }

        /// <summary>
        /// Attaches a mini 3D mascot costume decoration of <paramref name="prefabOrInstance"/> onto the mecha's head.
        /// </summary>
        public static void AttachMascotToHead(GameObject character, GameObject prefabOrInstance)
        {
            if (character == null || prefabOrInstance == null) return;

            Transform headTransform = FindHeadTransform(character.transform);
            if (headTransform == null) headTransform = character.transform;

            // Remove previous mascot if any
            Transform prevMascot = headTransform.Find("Mecha_Head_Mascot");
            if (prevMascot != null) Object.Destroy(prevMascot.gameObject);

            GameObject mascot = Object.Instantiate(prefabOrInstance, headTransform);
            mascot.name = "Mecha_Head_Mascot";

            // Strip physics scripts and colliders so it doesn't collide
            foreach (var col in mascot.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (var rb in mascot.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
            var findTarget = mascot.GetComponent<FindTargetObject>();
            if (findTarget != null) Object.Destroy(findTarget);

            // Scale down to mini helmet size
            mascot.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            mascot.transform.localRotation = Quaternion.identity;

            Renderer[] rends = mascot.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                float scaleNorm = maxDim > 1e-4f ? 0.18f / maxDim : 0.15f;
                mascot.transform.localScale = Vector3.one * scaleNorm;
            }
            else
            {
                mascot.transform.localScale = Vector3.one * 0.15f;
            }
        }

        public static List<Material> SamplePileMaterials()
        {
            List<Material> mats = new List<Material>();
            foreach (FindTargetObject f in Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None))
            {
                if (f.isDocked) continue;
                foreach (Renderer r in f.GetComponentsInChildren<Renderer>())
                {
                    if (r != null && r.sharedMaterial != null && !mats.Contains(r.sharedMaterial))
                    {
                        mats.Add(r.sharedMaterial);
                    }
                }
            }
            return mats;
        }

        public static List<Color> SamplePileColors()
        {
            var colors = new List<Color>();
            foreach (FindTargetObject f in Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None))
            {
                if (!f.isDocked) colors.Add(f.objectColor);
            }
            return colors;
        }

        private static Transform FindHeadTransform(Transform parent)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLowerInvariant().Contains("head"))
                {
                    return t;
                }
            }
            return parent;
        }
    }
}
