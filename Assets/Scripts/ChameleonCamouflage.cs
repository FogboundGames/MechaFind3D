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
        /// Extracts the primary/dominant color of the host object (from FindTargetObject or material properties)
        /// so the mecha's camouflage glass tint can dynamically match the host item's color.
        /// </summary>
        public static Color GetHostDominantColor(GameObject hostObject)
        {
            if (hostObject == null) return Color.white;

            // 1. Check FindTargetObject component
            FindTargetObject target = hostObject.GetComponent<FindTargetObject>();
            if (target == null) target = hostObject.GetComponentInChildren<FindTargetObject>();
            if (target != null && target.objectColor != Color.clear && target.objectColor.a > 0.01f)
            {
                return target.objectColor;
            }

            // 2. Check Renderer materials
            Renderer[] renderers = hostObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                Material mat = r.sharedMaterial;

                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    if (c.a > 0.01f && (c.r > 0.02f || c.g > 0.02f || c.b > 0.02f)) return c;
                }
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.GetColor("_Color");
                    if (c.a > 0.01f && (c.r > 0.02f || c.g > 0.02f || c.b > 0.02f)) return c;
                }
            }

            return Color.white;
        }

        /// <summary>
        /// Gives the mecha a see-through glass look tinted dynamically by the host object's dominant color.
        /// The host object underneath stays clearly visible through it, while the mecha blends subtly into the host.
        /// </summary>
        public static void ApplyGlassMaterial(GameObject mecha, float opacity = 0.22f, Color hostColor = default)
        {
            if (mecha == null) return;

            // Prioritize built-in pipeline shaders to avoid magenta compilation errors
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Unlit/Transparent");

            Material glassMat = new Material(shader);
            glassMat.name = "CrystalGlassMechaMat";

            // Transparent Blend & Z-Buffer Configuration
            if (glassMat.HasProperty("_Surface")) glassMat.SetFloat("_Surface", 1.0f); // URP: 1 = Transparent
            if (glassMat.HasProperty("_Blend")) glassMat.SetFloat("_Blend", 0.0f);     // URP: 0 = Alpha blend
            if (glassMat.HasProperty("_Mode")) glassMat.SetFloat("_Mode", 3.0f);       // Built-in Standard: 3 = Transparent
            if (glassMat.HasProperty("_AlphaClip")) glassMat.SetFloat("_AlphaClip", 0.0f);

            if (glassMat.HasProperty("_SrcBlend")) glassMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (glassMat.HasProperty("_DstBlend")) glassMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            
            // ZWrite = 0 ensures depth buffer does NOT block or clip host object (watermelon, cake, etc.) rendering underneath
            if (glassMat.HasProperty("_ZWrite")) glassMat.SetFloat("_ZWrite", 0.0f);

            glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.DisableKeyword("_ALPHATEST_ON");
            glassMat.EnableKeyword("_ALPHABLEND_ON");
            glassMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            // Derive host-matched glass tint color
            Color baseTint = new Color(0.90f, 0.96f, 1.0f);
            bool hasHostColor = hostColor != default && hostColor != Color.clear && (hostColor.r > 0.02f || hostColor.g > 0.02f || hostColor.b > 0.02f);

            if (hasHostColor)
            {
                // Blend host color into glass tint so it takes on the host item's dominant color
                baseTint = Color.Lerp(new Color(0.92f, 0.95f, 1.0f), hostColor, 0.75f);
            }

            float glassAlpha = Mathf.Clamp(opacity * 0.75f, 0.08f, 0.25f);
            baseTint.a = glassAlpha;

            if (glassMat.HasProperty("_BaseColor")) glassMat.SetColor("_BaseColor", baseTint);
            if (glassMat.HasProperty("_Color")) glassMat.SetColor("_Color", baseTint);

            // Specular & Glossiness
            if (glassMat.HasProperty("_Smoothness")) glassMat.SetFloat("_Smoothness", 0.95f);
            if (glassMat.HasProperty("_Metallic")) glassMat.SetFloat("_Metallic", 0f);

            // Subtle emission glow matching host color tone for soft silhouette reading
            if (glassMat.HasProperty("_EmissionColor"))
            {
                glassMat.EnableKeyword("_EMISSION");
                Color emissionColor = hasHostColor ? hostColor * 0.35f : new Color(0.15f, 0.50f, 0.85f) * 0.35f;
                emissionColor.a = 1.0f;
                glassMat.SetColor("_EmissionColor", emissionColor);
            }

            // Clear any main textures so material remains transparent glass
            if (glassMat.HasProperty("_BaseMap")) glassMat.SetTexture("_BaseMap", null);
            if (glassMat.HasProperty("_MainTex")) glassMat.SetTexture("_MainTex", null);
            glassMat.mainTexture = null;

            foreach (Renderer r in mecha.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;

                // Skip host item renderers if mecha is parented under a FindTargetObject host item!
                FindTargetObject fto = r.GetComponent<FindTargetObject>() ?? r.GetComponentInParent<FindTargetObject>();
                if (fto != null && fto.gameObject != mecha) continue;

                int slotCount = Mathf.Max(1, r.sharedMaterials.Length);
                Material[] newMats = new Material[slotCount];
                for (int m = 0; m < slotCount; m++)
                {
                    newMats[m] = glassMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        private static Material cachedWhiteMat;

        /// <summary>
        /// Drops the camouflage: repaints the mecha in a solid white material.
        /// </summary>
        public static void ApplyRevealedMaterial(GameObject mecha)
        {
            if (mecha == null) return;

            if (cachedWhiteMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                cachedWhiteMat = new Material(shader) { name = "RevealedMechaMat" };

                if (cachedWhiteMat.HasProperty("_Surface")) cachedWhiteMat.SetFloat("_Surface", 0f);
                if (cachedWhiteMat.HasProperty("_Blend")) cachedWhiteMat.SetFloat("_Blend", 0f);
                if (cachedWhiteMat.HasProperty("_Mode")) cachedWhiteMat.SetFloat("_Mode", 0f);
                if (cachedWhiteMat.HasProperty("_SrcBlend")) cachedWhiteMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (cachedWhiteMat.HasProperty("_DstBlend")) cachedWhiteMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                if (cachedWhiteMat.HasProperty("_ZWrite")) cachedWhiteMat.SetFloat("_ZWrite", 1f);

                cachedWhiteMat.SetOverrideTag("RenderType", "Opaque");
                cachedWhiteMat.DisableKeyword("_ALPHABLEND_ON");
                cachedWhiteMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                cachedWhiteMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                cachedWhiteMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

                if (cachedWhiteMat.HasProperty("_Smoothness")) cachedWhiteMat.SetFloat("_Smoothness", 0.45f);
                if (cachedWhiteMat.HasProperty("_Metallic")) cachedWhiteMat.SetFloat("_Metallic", 0f);

                Color white = Color.white;
                if (cachedWhiteMat.HasProperty("_BaseColor")) cachedWhiteMat.SetColor("_BaseColor", white);
                if (cachedWhiteMat.HasProperty("_Color")) cachedWhiteMat.SetColor("_Color", white);
                if (cachedWhiteMat.HasProperty("_BaseMap")) cachedWhiteMat.SetTexture("_BaseMap", null);
                if (cachedWhiteMat.HasProperty("_MainTex")) cachedWhiteMat.SetTexture("_MainTex", null);
                cachedWhiteMat.mainTexture = null;
            }

            foreach (Renderer r in mecha.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;

                // Skip host item renderers if mecha is parented under a FindTargetObject host item!
                FindTargetObject fto = r.GetComponent<FindTargetObject>() ?? r.GetComponentInParent<FindTargetObject>();
                if (fto != null && fto.gameObject != mecha) continue;

                int slotCount = Mathf.Max(1, r.sharedMaterials.Length);
                Material[] newMats = new Material[slotCount];
                for (int m = 0; m < slotCount; m++) newMats[m] = cachedWhiteMat;
                r.sharedMaterials = newMats;
            }
        }

        /// <summary>
        /// Embeds/Attaches the mecha INTO a host item (e.g. a Cake 🍰), positioning its upper body/limbs
        /// emerging from the top of the host object, repainted in the host item's exact texture/materials!
        /// </summary>
        public static void EmbedMechaInHostObject(GameObject mecha, GameObject hostObject, float scaleRatio = 0.85f, float opacity = 0.22f, Vector3 positionOffset = default, Vector3 rotationOffset = default, float absoluteWorldSize = 0f, MechaPivotSelection pivotPreference = MechaPivotSelection.Auto, float wrapAmount = 0f)
        {
            if (mecha == null || hostObject == null) return;

            // 1. Keep host object (e.g. Cake) upright flat on table
            hostObject.transform.rotation = Quaternion.identity;

            Rigidbody hostRb = hostObject.GetComponent<Rigidbody>();
            if (hostRb != null)
            {
                // Set mass to 1.0f matching all other pile objects for equal push force responsiveness
                hostRb.constraints = RigidbodyConstraints.None;
                hostRb.mass = 1.0f;
                hostRb.interpolation = RigidbodyInterpolation.Interpolate;
                hostRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Ensure child rigidbodies on mecha figure are kinematic so they don't add extra physics weight
            foreach (Rigidbody childRb in mecha.GetComponentsInChildren<Rigidbody>())
            {
                if (childRb != null && childRb != hostRb)
                {
                    childRb.isKinematic = true;
                }
            }

            // Ensure active contact pusher is attached to the host object
            ColliderContactPusher hostPusher = hostObject.GetComponent<ColliderContactPusher>();
            if (hostPusher == null) hostObject.AddComponent<ColliderContactPusher>();

            // 2. Extract host item materials and dominant color
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

            // 3. Apply CRYSTAL GLASS material tinted with the host object's dominant color
            Color hostColor = GetHostDominantColor(hostObject);
            ApplyGlassMaterial(mecha, opacity, hostColor);

            // 4. Pose is left at the model's imported T-pose here. A fixed "Vitruvian spread" used to be
            // forced on every mecha at this point, which meant the limbs were never actually straight and
            // it fought the wrap pose applied later. Limb posing is now driven solely by wrapAmount:
            // 0 = untouched T-pose, higher = curled around the host.

            // 5. Compute bounds & position mecha FLAT ON TOP of the host object face
            Bounds hostBounds = GetCombinedBounds(hostObject);
            float hostExtent = hostBounds.size.magnitude;
            if (hostExtent < 1e-4f) hostExtent = 1.0f;

            // Measure unscaled mecha model size. For skinned meshes use localBounds (the serialized
            // bind-pose bounds), NOT renderer.bounds: the latter is pose-dependent and stale in edit
            // mode, so it desynced the tool preview size from gameplay. localBounds is identical in
            // edit and play, making the mecha exactly the same size in both.
            mecha.transform.localScale = Vector3.one;
            float rawMechaExtent;
            SkinnedMeshRenderer skinned = mecha.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                Vector3 worldSize = Vector3.Scale(skinned.localBounds.size, skinned.transform.lossyScale);
                rawMechaExtent = worldSize.magnitude;
            }
            else
            {
                rawMechaExtent = GetCombinedBounds(mecha).size.magnitude;
            }
            if (rawMechaExtent < 1e-4f) rawMechaExtent = 2.0f;

            // Target mecha world size: ABSOLUTE (identical in tool preview and gameplay, and tunable from
            // one place) when provided; otherwise fall back to host-relative scaleRatio.
            float targetMechaWorldSize = (absoluteWorldSize > 0f)
                ? absoluteWorldSize
                : hostExtent * Mathf.Clamp(scaleRatio, 0.25f, 1.20f);
            float desiredWorldScale = targetMechaWorldSize / rawMechaExtent;

            bool hasPivots = TryPickApproachPivot(hostObject.transform, out ApproachSide side, out Transform pivot, pivotPreference);

            // The mecha lands on whichever side we picked, but the host's yaw is arbitrary (it tumbled in
            // the pile), so that side can end up facing away from the player — leaving the mecha hidden
            // behind its own host and the level unwinnable. Yaw the host so the chosen side faces the
            // camera. Only meaningful for sideways approaches; Top/Bottom are vertical and unaffected.
            if (hasPivots && side != ApproachSide.Top && side != ApproachSide.Bottom)
            {
                FaceSideTowardCamera(hostObject.transform, side);
            }

            Vector3 approachDirection = hasPivots ? ApproachDirectionForSide(hostObject.transform, side, pivot) : hostObject.transform.up;

            mecha.transform.SetParent(hostObject.transform, false);

            // Cancel out parent world scale so the mecha keeps its intended size (never a microscopic dot).
            Vector3 parentLossy = hostObject.transform.lossyScale;
            float px = Mathf.Abs(parentLossy.x) > 1e-4f ? Mathf.Abs(parentLossy.x) : 1f;
            float py = Mathf.Abs(parentLossy.y) > 1e-4f ? Mathf.Abs(parentLossy.y) : 1f;
            float pz = Mathf.Abs(parentLossy.z) > 1e-4f ? Mathf.Abs(parentLossy.z) : 1f;
            mecha.transform.localScale = new Vector3(desiredWorldScale / px, desiredWorldScale / py, desiredWorldScale / pz);

            // 6. Build exact trigger colliders on mecha for interaction/docking
            AddExactMeshColliderToMecha(mecha);

            // 7. Surface Skin Mapping: Place mecha flat on host surface and offset outwards so lowest point rests on top
            Vector3 surfacePoint = pivot != null ? pivot.position : GetCombinedBounds(hostObject).center + approachDirection * GetCombinedBounds(hostObject).extents.y;
            Vector3 surfaceNormal = approachDirection.sqrMagnitude > 1e-4f ? approachDirection.normalized : hostObject.transform.up;

            bool isHandAuthoredAnchor = pivot != null &&
                (pivot.name.StartsWith("MechaAnchor", System.StringComparison.OrdinalIgnoreCase) ||
                 pivot.name.StartsWith("Anchor", System.StringComparison.OrdinalIgnoreCase));

            if (isHandAuthoredAnchor)
            {
                // Hand-placed anchor (MechaAnchorTool): its own rotation was deliberately authored to define
                // orientation directly, so use it as-is.
                mecha.transform.rotation = pivot.rotation * Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // Auto-generated edge pivots (Pivot_Top/Bottom/Left/Right) all have identity local rotation —
                // they carry no orientation info of their own. Derive the flattening rotation from the actual
                // approach direction instead, so the mecha's back faces INTO the surface from whichever side
                // was picked (this reduces to exactly the old fixed (90,0,0) for Top, and correctly
                // generalizes it for Bottom/Left/Right — without this, every side used the Top-only rotation
                // and only a stray limb/corner ever reached the surface).
                mecha.transform.rotation = Quaternion.FromToRotation(Vector3.forward, -surfaceNormal);
            }

            // Start mecha at surfacePoint to evaluate its mesh extent
            mecha.transform.position = surfacePoint;
            Physics.SyncTransforms();

            // Body thickness measured in the UNWRAPPED pose. How deep to sink the mecha has to come from
            // this, not from the wrapped pose: curling the arms sweeps them around the approach axis, which
            // inflates the measured depth, and sinking by a fraction of that inflated number buried the
            // whole body inside the host as wrapAmount went up.
            float flatDepth = MeasureDepthAlongAxis(mecha, surfacePoint, surfaceNormal);

            // Curl the limbs around the host. Done before the centring/extent work below so those see the
            // final silhouette.
            ApplyWrapPose(mecha.transform, wrapAmount);
            Physics.SyncTransforms();

            // The model's origin sits at its FEET, not in the middle of the body, so dropping the transform
            // on the pivot lands the feet on the host's centre and throws the whole figure off to one side.
            // Slide it so the BODY's centre is over the pivot instead. Only the two axes across the surface
            // are corrected — depth along surfaceNormal is what the sink step below is for.
            Bounds centeringBounds;
            if (TryGetBodyBounds(mecha, out centeringBounds))
            {
                Vector3 toPivot = surfacePoint - centeringBounds.center;
                Vector3 lateral = toPivot - Vector3.Project(toPivot, surfaceNormal);
                mecha.transform.position += lateral;
                Physics.SyncTransforms();
            }

            // How far the mecha reaches along the approach axis. TORSO ONLY — arms and legs are excluded
            // on purpose. They are what wraps around the host, so they reach past its surface by design;
            // letting them drive this measurement pushed the whole figure back until the torso floated
            // clear of the object with only the limbs near it. Measuring the torso alone beds the body
            // against the surface and leaves the limbs free to curl around it.
            // Colliders (per-limb, tight) are preferred over the skinned mesh's own padded bounds.
            float minProj = float.MaxValue;
            float maxProj = float.MinValue;

            Collider[] mechaExtentCols = mecha.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in mechaExtentCols)
            {
                if (col == null || !col.enabled) continue;
                if (!IsTorsoPart(col.gameObject.name)) continue;
                AccumulateProjection(col.bounds, surfacePoint, surfaceNormal, ref minProj, ref maxProj);
            }

            // No torso colliders identified (unknown rig naming): fall back to every collider, then renderers.
            if (minProj == float.MaxValue)
            {
                foreach (Collider col in mechaExtentCols)
                {
                    if (col == null || !col.enabled) continue;
                    AccumulateProjection(col.bounds, surfacePoint, surfaceNormal, ref minProj, ref maxProj);
                }
            }

            if (minProj == float.MaxValue)
            {
                Renderer[] mechaRends = mecha.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in mechaRends)
                {
                    if (r == null || !r.enabled) continue;
                    AccumulateProjection(r.bounds, surfacePoint, surfaceNormal, ref minProj, ref maxProj);
                }
            }

            if (minProj == float.MaxValue) { minProj = -0.2f; maxProj = 0.2f; }

            const float embedFraction = 0.45f;
            float depthRange = maxProj - minProj;
            // Sink depth comes from the unwrapped body thickness (see flatDepth above) so it stays constant
            // as the mecha curls; depthRange is still what the fallback path needs.
            float sinkDepth = (flatDepth > 1e-4f ? flatDepth : depthRange) * embedFraction;

            // Sink the mecha into the host by a fraction of its own depth along surfaceNormal, instead of
            // stopping flush at the surface: since the mecha is fully transparent glass, the sunken portion
            // just gets naturally hidden behind the host's opaque mesh (normal depth occlusion), while the
            // rest keeps poking out — reads as "hiding inside" the object rather than a separate shape
            // hovering awkwardly beside/above it with a visible gap.
            //
            // Measured off the RENDERED mesh corners on purpose. Driving this from collider contact instead
            // (ColliderContactSnapSolver, or an axis-constrained binary search against the same colliders)
            // was tried and looked worse: the mecha's per-limb convex hulls sit wider than the visible mesh,
            // so contact triggers early and leaves it hovering with a gap on rounded hosts.
            float shiftAmount = -minProj + 0.01f - sinkDepth;
            mecha.transform.position += surfaceNormal * shiftAmount;
            Physics.SyncTransforms();

            // Apply positionOffset/rotationOffset on top of the mapped surface pose
            if (positionOffset != Vector3.zero)
            {
                mecha.transform.position += mecha.transform.TransformDirection(positionOffset);
            }
            if (rotationOffset != Vector3.zero)
            {
                mecha.transform.rotation = mecha.transform.rotation * Quaternion.Euler(rotationOffset);
            }

            foreach (MechaFind3D.PhysicsInteraction.ColliderContactPusher pusher in mecha.GetComponentsInChildren<MechaFind3D.PhysicsInteraction.ColliderContactPusher>())
            {
                Object.Destroy(pusher);
            }
            foreach (Joint j in mecha.GetComponentsInChildren<Joint>())
            {
                Object.Destroy(j);
            }
            foreach (Rigidbody rb in mecha.GetComponentsInChildren<Rigidbody>())
            {
                Object.Destroy(rb);
            }
        }

        /// <summary>
        /// Curls the mecha's limbs around the host so it clings to it (a soda can, a bottle) instead of
        /// lying flat on the surface. Bones are rotated about the mecha's OWN up/right axes in world space
        /// rather than their local axes, because local bone axes differ per rig and per limb — using the
        /// root's axes keeps left/right symmetric and works on any humanoid import.
        /// <paramref name="amount"/> 0 = untouched flat pose, 1 = full hug.
        /// </summary>
        private static void ApplyWrapPose(Transform mechaRoot, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0.001f) return;

            // Around the host: the mecha faces the surface, so its up axis runs along a standing host's
            // axis. Swinging the arms about that axis carries them around the object's sides.
            Vector3 around = mechaRoot.up;
            Vector3 forwardAxis = mechaRoot.right;

            // Sign chosen so the limbs sweep TOWARD the host and around its sides. Checked from a top-down
            // view, which is the only angle that shows unambiguously whether an arm curls around the object
            // or off into open space — from the front both directions look similar.
            // The totals are also kept moderate: a shoulder+elbow adding up to much more than ~135° stops
            // following the surface and drives the arm straight through the object instead.
            const float upperArmSwing = 70f;   // shoulder brings the arm around the object
            const float forearmCurl = 55f;     // elbow continues the curve along the surface
            const float legCurl = 50f;         // knees/thighs tuck in underneath
            const float hugTilt = 10f;         // slight lean into the surface

            foreach (Transform t in mechaRoot.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                bool isLeft = n.EndsWith(".l") || n.Contains("_l") || n.Contains("left");
                float side = isLeft ? 1f : -1f;

                if (n.Contains("upper_arm"))
                {
                    t.Rotate(around, side * upperArmSwing * amount, Space.World);
                    t.Rotate(forwardAxis, hugTilt * amount, Space.World);
                }
                else if (n.Contains("forearm"))
                {
                    t.Rotate(around, side * forearmCurl * amount, Space.World);
                }
                else if (n.Contains("thigh"))
                {
                    t.Rotate(around, side * legCurl * amount, Space.World);
                    t.Rotate(forwardAxis, hugTilt * amount, Space.World);
                }
                else if (n.Contains("shin"))
                {
                    t.Rotate(around, side * legCurl * 0.8f * amount, Space.World);
                }
            }
        }

        /// <summary>
        /// Builds tight per-limb convex hull colliders that hug the mecha's actual body silhouette
        /// (see MechaColliderBuilder) instead of a single whole-body hull or bounding box, so the body
        /// makes contact along its real surface without a large invisible box clipping into the host item.
        /// </summary>
        public static void AddExactMeshColliderToMecha(GameObject mecha)
        {
            if (mecha == null) return;
            MechaColliderBuilder.BuildTightBodyColliders(mecha, isTrigger: true);
        }

        private enum ApproachSide { Top, Bottom, Left, Right, Front, Back }

        /// <summary>
        /// Picks a designated pivot/anchor child on the host with priority:
        /// 0. Active selection in Unity Editor (if user clicked a child pivot in Hierarchy)
        /// 1. Explicit designer preference / anchors (MechaAnchor..., Anchor..., Pivot_BaseContact)
        /// 2. Top surface pivots (Pivot_Top, Pivot_Front, Pivot_Back)
        /// 3. Side pivots (Pivot_Left, Pivot_Right)
        /// 4. Bottom pivot (Pivot_Bottom - last resort only)
        /// </summary>
        private static bool TryPickApproachPivot(Transform host, out ApproachSide side, out Transform pivot, MechaPivotSelection preference = MechaPivotSelection.Auto)
        {
            side = ApproachSide.Top;
            pivot = null;
            if (host == null) return false;

#if UNITY_EDITOR
            // Active Unity Editor Selection check: If the user clicked a child pivot in Hierarchy (e.g. Pivot_Top), use it directly!
            if (UnityEditor.Selection.activeTransform != null && UnityEditor.Selection.activeTransform.IsChildOf(host) && UnityEditor.Selection.activeTransform != host)
            {
                Transform activeT = UnityEditor.Selection.activeTransform;
                string activeName = activeT.name.ToLowerInvariant();
                if (activeName.Contains("top")) side = ApproachSide.Top;
                else if (activeName.Contains("bottom")) side = ApproachSide.Bottom;
                else if (activeName.Contains("left")) side = ApproachSide.Left;
                else if (activeName.Contains("right")) side = ApproachSide.Right;
                else side = ApproachSide.Top;

                pivot = activeT;
                return true;
            }
#endif

            List<(ApproachSide side, Transform t)> explicitAnchors = new List<(ApproachSide, Transform)>();
            List<(ApproachSide side, Transform t)> topPivots = new List<(ApproachSide, Transform)>();
            List<(ApproachSide side, Transform t)> facePivots = new List<(ApproachSide, Transform)>();
            List<(ApproachSide side, Transform t)> sidePivots = new List<(ApproachSide, Transform)>();
            List<(ApproachSide side, Transform t)> bottomPivots = new List<(ApproachSide, Transform)>();

            foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
            {
                if (t == host) continue;
                string name = t.name;

                if (name.StartsWith("MechaAnchor", System.StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Anchor", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Pivot_BaseContact", System.StringComparison.OrdinalIgnoreCase))
                {
                    explicitAnchors.Add((ApproachSide.Top, t));
                }
                else if (name.Equals("Pivot_Top", System.StringComparison.OrdinalIgnoreCase))
                {
                    topPivots.Add((ApproachSide.Top, t));
                }
                // Front/Back were previously lumped in with Top, which handed them the Top approach
                // direction (host.up) — so the mecha was flattened against the wrong axis and only a
                // stray limb reached the broad face. They now carry their own side/direction.
                else if (name.Equals("Pivot_Front", System.StringComparison.OrdinalIgnoreCase))
                {
                    facePivots.Add((ApproachSide.Front, t));
                }
                else if (name.Equals("Pivot_Back", System.StringComparison.OrdinalIgnoreCase))
                {
                    facePivots.Add((ApproachSide.Back, t));
                }
                else if (name.Equals("Pivot_Left", System.StringComparison.OrdinalIgnoreCase))
                {
                    sidePivots.Add((ApproachSide.Left, t));
                }
                else if (name.Equals("Pivot_Right", System.StringComparison.OrdinalIgnoreCase))
                {
                    sidePivots.Add((ApproachSide.Right, t));
                }
                else if (name.Equals("Pivot_Bottom", System.StringComparison.OrdinalIgnoreCase))
                {
                    bottomPivots.Add((ApproachSide.Bottom, t));
                }
            }

            // Honor explicitly requested pivot preference from LevelDataSO if specified
            if (preference == MechaPivotSelection.PivotTop && topPivots.Count > 0)
            {
                (side, pivot) = topPivots[0];
                return true;
            }
            if (preference == MechaPivotSelection.PivotBottom && bottomPivots.Count > 0)
            {
                (side, pivot) = bottomPivots[0];
                return true;
            }
            if (preference == MechaPivotSelection.PivotLeft && sidePivots.Count > 0)
            {
                (side, pivot) = sidePivots[0];
                return true;
            }
            if (preference == MechaPivotSelection.PivotRight && sidePivots.Count > 0)
            {
                (side, pivot) = sidePivots[0];
                return true;
            }
            if (preference == MechaPivotSelection.PivotFront || preference == MechaPivotSelection.PivotBack)
            {
                ApproachSide wanted = preference == MechaPivotSelection.PivotFront ? ApproachSide.Front : ApproachSide.Back;
                foreach (var fp in facePivots)
                {
                    if (fp.side == wanted) { (side, pivot) = fp; return true; }
                }
                if (facePivots.Count > 0) { (side, pivot) = facePivots[0]; return true; }
            }
            if (preference == MechaPivotSelection.MechaAnchor && explicitAnchors.Count > 0)
            {
                (side, pivot) = explicitAnchors[0];
                return true;
            }

            if (explicitAnchors.Count > 0)
            {
                (side, pivot) = explicitAnchors[Random.Range(0, explicitAnchors.Count)];
                return true;
            }
            // Auto: prefer the broad face on thin/flat items (a slice's front face dwarfs its edges), so
            // the mecha lies against real surface area instead of balancing on a narrow rim.
            if (facePivots.Count > 0 && IsFlatObject(host))
            {
                (side, pivot) = facePivots[Random.Range(0, facePivots.Count)];
                return true;
            }
            if (topPivots.Count > 0)
            {
                (side, pivot) = topPivots[Random.Range(0, topPivots.Count)];
                return true;
            }
            if (facePivots.Count > 0)
            {
                (side, pivot) = facePivots[Random.Range(0, facePivots.Count)];
                return true;
            }
            if (sidePivots.Count > 0)
            {
                (side, pivot) = sidePivots[Random.Range(0, sidePivots.Count)];
                return true;
            }
            if (bottomPivots.Count > 0)
            {
                (side, pivot) = bottomPivots[Random.Range(0, bottomPivots.Count)];
                return true;
            }

            return false;
        }

        /// <summary>World-space approach direction for a side, derived fresh from the host's live axes or pivot orientation.</summary>
        /// <summary>
        /// True for the mecha's trunk (spine/torso/chest/head/pelvis) and false for anything that hangs
        /// off it. Limb colliders are named after their bone (BodyCollider_forearm.L, ...), so this is a
        /// name test. Used to decide what actually beds against a host surface: the trunk does, while the
        /// arms and legs are free to reach past it and wrap around.
        /// </summary>
        private static bool IsTorsoPart(string objectName)
        {
            string n = objectName.ToLowerInvariant();

            // Defined by exclusion: name the limbs, treat everything else as trunk. Generated colliders
            // are all prefixed "BodyCollider_", so an inclusion list keyed on words like "body" would
            // match every part; this way an unrecognised bone (neck, tail) still counts as trunk.
            return !(n.Contains("arm") || n.Contains("hand") || n.Contains("shoulder") ||
                     n.Contains("thigh") || n.Contains("shin") || n.Contains("leg") ||
                     n.Contains("foot") || n.Contains("toe") || n.Contains("heel"));
        }

        /// <summary>
        /// Thickness of the mecha's body along <paramref name="axis"/>, from its current pose.
        /// </summary>
        private static float MeasureDepthAlongAxis(GameObject mecha, Vector3 origin, Vector3 axis)
        {
            float min = float.MaxValue, max = float.MinValue;

            // Trunk only, to match the placement measurement — the sink depth should describe how thick
            // the body is, not how far an outstretched arm happens to reach along this axis.
            foreach (Collider c in mecha.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled) continue;
                if (!IsTorsoPart(c.gameObject.name)) continue;
                AccumulateProjection(c.bounds, origin, axis, ref min, ref max);
            }
            if (min == float.MaxValue)
            {
                foreach (Renderer r in mecha.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !r.enabled) continue;
                    AccumulateProjection(r.bounds, origin, axis, ref min, ref max);
                }
            }

            return min == float.MaxValue ? 0f : max - min;
        }

        /// <summary>
        /// World-space bounds of the mecha's actual body, preferring its per-limb colliders (tight) and
        /// falling back to renderers. Used to centre the figure on a pivot, since the model's own transform
        /// origin is at its feet rather than in the middle of the body.
        /// </summary>
        private static bool TryGetBodyBounds(GameObject mecha, out Bounds bounds)
        {
            bounds = default;
            bool has = false;

            foreach (Collider c in mecha.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled) continue;
                if (!has) { bounds = c.bounds; has = true; }
                else bounds.Encapsulate(c.bounds);
            }
            if (has) return true;

            foreach (Renderer r in mecha.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        /// <summary>
        /// Projects a world-space AABB's 8 corners onto the approach axis, widening the running min/max.
        /// </summary>
        private static void AccumulateProjection(Bounds worldBounds, Vector3 surfacePoint, Vector3 surfaceNormal, ref float minProj, ref float maxProj)
        {
            Vector3 c = worldBounds.center;
            Vector3 e = worldBounds.extents;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);

                float proj = Vector3.Dot(corner - surfacePoint, surfaceNormal);
                if (proj < minProj) minProj = proj;
                if (proj > maxProj) maxProj = proj;
            }
        }

        /// <summary>
        /// Spins the host around Y so the given side's outward normal points horizontally at the camera,
        /// then locks its rotation so the pile can't tumble the mecha out of view again.
        /// </summary>
        private static void FaceSideTowardCamera(Transform host, ApproachSide side)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            // Where the chosen side currently points, flattened to the horizontal plane.
            Vector3 current = ApproachDirectionForSide(host, side);
            current.y = 0f;
            if (current.sqrMagnitude < 1e-6f) return;

            // Where we want it to point: from the host back toward the camera, also flattened.
            Vector3 desired = cam.transform.position - host.position;
            desired.y = 0f;
            if (desired.sqrMagnitude < 1e-6f) return;

            host.rotation = Quaternion.FromToRotation(current.normalized, desired.normalized) * host.rotation;

            Rigidbody rb = host.GetComponent<Rigidbody>();
            if (rb != null) rb.constraints = RigidbodyConstraints.None;

            Physics.SyncTransforms();
        }

        /// <summary>
        /// True when the host is noticeably thinner along one axis than the other two (a slice, a wafer,
        /// a flat piece of bread). For those, the broad face — not the top rim — is the surface a mecha
        /// can convincingly lie against.
        /// </summary>
        private static bool IsFlatObject(Transform host)
        {
            Renderer[] rends = host.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return false;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            // Compare the thinnest axis against the MIDDLE one, not the longest. Measuring against the
            // longest also flags merely elongated shapes: a sausage is round in cross-section but long,
            // so min/max is small even though it has no broad face to lie on. min/mid is small only when
            // one axis is genuinely squashed relative to the other two — an actual slice/wafer.
            Vector3 s = b.size;
            float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float mid = s.x + s.y + s.z - min - max;
            if (mid < 1e-4f) return false;

            return (min / mid) < 0.5f;
        }

        private static Vector3 ApproachDirectionForSide(Transform host, ApproachSide side, Transform pivot = null)
        {
            if (pivot != null)
            {
                if (pivot.name.StartsWith("MechaAnchor", System.StringComparison.OrdinalIgnoreCase) ||
                    pivot.name.StartsWith("Anchor", System.StringComparison.OrdinalIgnoreCase))
                {
                    return pivot.up;
                }
            }

            switch (side)
            {
                case ApproachSide.Top: return host.up;
                case ApproachSide.Bottom: return -host.up;
                case ApproachSide.Left: return -host.right;
                case ApproachSide.Right: return host.right;
                case ApproachSide.Front: return -host.forward;
                case ApproachSide.Back: return host.forward;
                default: return host.up;
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
            foreach (var pusher in mascot.GetComponentsInChildren<MechaFind3D.PhysicsInteraction.ColliderContactPusher>()) Object.Destroy(pusher);
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
