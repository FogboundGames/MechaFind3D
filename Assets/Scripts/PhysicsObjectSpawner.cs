using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Spawns a rich 3D pile of plastic toys for Match Factory style Search & Find gameplay.
    /// Configured with low friction (0.18) and smooth drag (2.2) so toys part effortlessly.
    /// </summary>
    public class PhysicsObjectSpawner : MonoBehaviour
    {
        [Header("Match Factory Toy Pile Config")]
        [Tooltip("Total number of physics objects to spawn in the pile.")]
        [SerializeField] private int totalObjectCount = 30;

        [Tooltip("Dimensions of the spawn area plate surface (inside container tray).")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(5.5f, 5.5f);

        [Tooltip("Drop height range to create a stacked 3D pile.")]
        [SerializeField] private float spawnHeightMin = 0.05f;
        [SerializeField] private float spawnHeightMax = 0.15f;

        [Tooltip("Scale range for spawned objects.")]
        [SerializeField] private float minScale = 0.20f;
        [SerializeField] private float maxScale = 0.25f;

        [Header("Match Factory Smooth Toy Physics")]
        [SerializeField] private float objectMass = 1.0f;
        [SerializeField] private float linearDrag = 2.5f;
        [SerializeField] private float angularDrag = 3.0f;
        [SerializeField] private float bounciness = 0.0f;
        [SerializeField] private float friction = 0.35f;

        [Header("Material & Visuals")]
        [SerializeField] private Shader objectShader;
        [Tooltip("Give pile objects procedural pattern textures from AppearanceLibrary (color/match-3 identity is preserved). Turn off for plain flat colors. Ignored when food models are used.")]
        [SerializeField] private bool useTexturedAppearance = true;

        [Header("Food Models (Match Factory items)")]
        [Tooltip("When on, the pile spawns these food models instead of primitive cubes/spheres. Match-3 identity becomes the food TYPE. Keep the set small (~6-10) so 3-of-a-kind matches happen.")]
        [SerializeField] private bool useFoodModels = true;
        public GameObject[] foodModels;
        [Tooltip("Every food is scaled so its largest dimension is about this many world units, so wildly different source sizes (a berry vs a cake) become a consistent pile.")]
        [SerializeField] private float foodTargetSize = 0.22f;

        private struct NamedColor
        {
            public string name;
            public Color color;

            public NamedColor(string n, Color c)
            {
                name = n;
                color = c;
            }
        }

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        public static PhysicsObjectSpawner Instance { get; private set; }

        private PhysicsMaterial physicsMaterial;
        private List<NamedColor> namedColors;
        private List<Material> colorMaterials;

        private void Awake()
        {
            Instance = this;

            // Frame Rate & Mobile VSync Optimization
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            Physics.gravity = new Vector3(0f, -15.0f, 0f);
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.defaultContactOffset = 0.008f;
            InitializeNamedColors();
            InitializePhysicsMaterial();
            CreateColorMaterials();
        }

        private void Start()
        {
            if (LevelManager.Instance == null)
            {
                SpawnObjects();
                SettleObjectsOnFloor();
            }
        }

        public void SettleObjectsOnFloor()
        {
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj == null) continue;
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.None;
                    rb.WakeUp();
                }
            }
        }

        private void InitializeNamedColors()
        {
            namedColors = new List<NamedColor>
            {
                new NamedColor("Kırmızı", new Color(0.95f, 0.2f, 0.2f)),
                new NamedColor("Mavi", new Color(0.2f, 0.55f, 0.95f)),
                new NamedColor("Yeşil", new Color(0.2f, 0.85f, 0.35f)),
                new NamedColor("Sarı", new Color(0.98f, 0.85f, 0.15f)),
                new NamedColor("Mor", new Color(0.65f, 0.25f, 0.9f)),
                new NamedColor("Turuncu", new Color(0.98f, 0.5f, 0.15f)),
                new NamedColor("Turkuaz", new Color(0.15f, 0.85f, 0.85f)),
                new NamedColor("Pembe", new Color(0.95f, 0.4f, 0.7f))
            };
        }

        private void InitializePhysicsMaterial()
        {
            physicsMaterial = new PhysicsMaterial("MatchFactoryToyPhysics")
            {
                dynamicFriction = friction * 0.75f,
                staticFriction = friction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        private void CreateColorMaterials()
        {
            colorMaterials = new List<Material>();
            Shader shaderToUse = objectShader;
            if (shaderToUse == null)
            {
                shaderToUse = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            for (int i = 0; i < namedColors.Count; i++)
            {
                Material mat = new Material(shaderToUse)
                {
                    name = $"ToyMaterial_{namedColors[i].name}"
                };

                Color col = namedColors[i].color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);

                colorMaterials.Add(mat);
            }
        }

        public void ClearCurrentPile()
        {
            Transform container = transform.Find("SpawnedObjectsContainer");
            if (container != null)
            {
                int childCount = container.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    GameObject child = container.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }

            // Also clear any spawned mecha ragdoll instances
            GameObject[] oldMechas = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var go in oldMechas)
            {
                if (go.name.Contains("MechaRagdoll") || go.name.Contains("Mecha_3D_Preview_Instance"))
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
        }
        public void SpawnObjects()
        {
            ClearObjects();

            Transform container = new GameObject("Spawned_Physics_Objects").transform;
            container.SetParent(transform);

            List<ItemDataSO> levelItems = null;
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                levelItems = LevelManager.Instance.ActiveLevelData.BuildSpawnItemList();
            }

            bool useLevelItems = levelItems != null && levelItems.Count > 0;
            int countToSpawn = useLevelItems ? levelItems.Count : totalObjectCount;
            bool spawnFood = !useLevelItems && useFoodModels && foodModels != null && foodModels.Length > 0;

            // The configured target size is a per-level/inspector wish, but it only makes sense relative to
            // how many objects actually have to share the spawn area — the same size that looks right for
            // 20 objects will bury the tray in overlap at 80. Cap it so the pile always fits the area this
            // level is actually spawning into, instead of a single fixed size that only works by coincidence.
            float autoMaxItemSize = ComputeAutoMaxItemSize(countToSpawn);

            for (int i = 0; i < countToSpawn; i++)
            {
                GameObject obj;
                ObjectShapeType shapeType;
                string itemId;
                Color itemColor;
                float scale;
                float massSize;
                bool isCustomModel = false;

                if (useLevelItems)
                {
                    ItemDataSO itemData = levelItems[i];
                    GameObject itemPrefab = itemData != null ? (itemData.prefab != null ? itemData.prefab : FindPrefabForItemId(itemData.GetEffectiveItemId())) : null;

                    if (itemPrefab != null)
                    {
                        obj = Instantiate(itemPrefab);
                        obj.name = $"LevelObj_{itemData.GetEffectiveItemId()}_{i}";
                        shapeType = ObjectShapeType.Cube;
                        itemId = itemData.GetEffectiveItemId();
                        itemColor = itemData != null ? itemData.targetColor : Color.white;
                        isCustomModel = true;

                        float configuredSize = (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null) ? LevelManager.Instance.ActiveLevelData.foodTargetSize : 1.20f;
                        float targetSize = Mathf.Max(1.10f, configuredSize);
                        float rand = Random.Range(0.95f, 1.05f);
                        float localMax = LocalMaxDimension(obj);
                        float norm = localMax > 1e-4f ? targetSize / localMax : targetSize;
                        scale = norm * rand;
                        massSize = targetSize * rand;
                    }
                    else
                    {
                        // Fallback primitive if prefab missing
                        obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        obj.name = $"FallbackCube_{i}";
                        shapeType = ObjectShapeType.Cube;
                        itemId = itemData != null ? itemData.GetEffectiveItemId() : "cube";
                        itemColor = itemData != null ? itemData.targetColor : Color.cyan;
                        scale = minScale;
                        massSize = scale;
                    }
                    obj.transform.SetParent(container);
                }
                else if (spawnFood)
                {
                    GameObject model = foodModels[Random.Range(0, foodModels.Length)];
                    obj = Instantiate(model);
                    obj.name = $"Food_{model.name}_{i}";
                    obj.transform.SetParent(container);

                    shapeType = ObjectShapeType.Cube;   // constant: identity is the food TYPE below
                    itemId = model.name;                // e.g. "apple", "banana"
                    itemColor = Color.white;
                    isCustomModel = true;

                    float effectiveFoodTargetSize = Mathf.Min(foodTargetSize, autoMaxItemSize);
                    float rand = Random.Range(0.9f, 1.1f);
                    float localMax = LocalMaxDimension(obj);
                    float norm = localMax > 1e-4f ? effectiveFoodTargetSize / localMax : effectiveFoodTargetSize;
                    scale = norm * rand;
                    massSize = effectiveFoodTargetSize * rand;
                }
                else
                {
                    bool isCube = (i % 2 == 0);
                    obj = GameObject.CreatePrimitive(isCube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                    obj.name = isCube ? $"PhysicsCube_{i}" : $"PhysicsSphere_{i}";
                    obj.transform.SetParent(container);

                    int colorIdx = Random.Range(0, namedColors.Count);
                    NamedColor chosenColor = namedColors[colorIdx];
                    shapeType = isCube ? ObjectShapeType.Cube : ObjectShapeType.Sphere;
                    itemId = chosenColor.name;
                    itemColor = chosenColor.color;
                    scale = Random.Range(minScale, maxScale);
                    massSize = scale;

                    Renderer rend = obj.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        if (useTexturedAppearance)
                            rend.sharedMaterial = AppearanceLibrary.RandomForColor(chosenColor.color);
                        else if (colorIdx < colorMaterials.Count)
                            rend.sharedMaterial = colorMaterials[colorIdx];
                    }
                }

                float posX = Random.Range(-spawnAreaSize.x * 0.45f, spawnAreaSize.x * 0.45f);
                float posZ = Random.Range(-spawnAreaSize.y * 0.45f, spawnAreaSize.y * 0.45f);
                float posY = Random.Range(spawnHeightMin, spawnHeightMax);
                obj.transform.position = new Vector3(posX, posY, posZ);
                obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                obj.transform.localScale = Vector3.one * scale;

                if (isCustomModel)
                {
                    foreach (Collider existingCol in obj.GetComponentsInChildren<Collider>())
                    {
                        Destroy(existingCol);
                    }
                    AddOptimalCollider(obj);
                }

                foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                }

                foreach (Collider c in obj.GetComponentsInChildren<Collider>())
                    c.sharedMaterial = physicsMaterial;

                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp == null) targetComp = obj.AddComponent<FindTargetObject>();
                targetComp.Initialize(shapeType, itemColor, itemId);

                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb == null) rb = obj.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.None;
                rb.mass = objectMass;
                rb.linearDamping = linearDrag;
                rb.angularDamping = angularDrag;
                rb.sleepThreshold = 0.05f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.solverIterations = 8;
                rb.solverVelocityIterations = 2;
                rb.WakeUp();

                // Attach active contact pusher so colliding objects immediately push each other away
                ColliderContactPusher pusher = obj.GetComponent<ColliderContactPusher>();
                if (pusher == null) obj.AddComponent<ColliderContactPusher>();

                spawnedObjects.Add(obj);
            }
        }

        /// <summary>
        /// Reads LevelDataSO settings and applies BlackLockItem component to designated random items in the pile.
        /// Excludes any Mecha characters or Mecha host items.
        /// </summary>
        public void ApplyBlackLocksForLevel()
        {
            if (LevelManager.Instance == null || LevelManager.Instance.ActiveLevelData == null) return;
            LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;

            if (!levelData.enableBlackLockedObjects || levelData.blackObjectCount <= 0) return;

            List<GameObject> candidates = new List<GameObject>();
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj == null) continue;
                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp == null || targetComp.isDocked) continue;

                // Skip any object that has a Mecha child or is a Mecha itself
                if (obj.GetComponentInChildren<MechaRunnerBehavior>(true) != null) continue;
                if (obj.name.Contains("Mecha") || obj.name.Contains("meccha") || obj.name.Contains("ragdoll")) continue;
                if (CanvasUIDesignManager.HasChildMecha(targetComp)) continue;

                candidates.Add(obj);
            }

            if (candidates.Count == 0) return;

            // Shuffle candidates to select random items to lock
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                GameObject temp = candidates[i];
                candidates[i] = candidates[rnd];
                candidates[rnd] = temp;
            }

            int lockCountToApply = Mathf.Min(levelData.blackObjectCount, candidates.Count);
            int unlockCount = Mathf.Max(1, levelData.blackObjectUnlockCount);

            for (int i = 0; i < lockCountToApply; i++)
            {
                GameObject targetObj = candidates[i];
                BlackLockItem lockComp = targetObj.GetComponent<BlackLockItem>();
                if (lockComp == null) lockComp = targetObj.AddComponent<BlackLockItem>();

                lockComp.InitializeLock(unlockCount);
            }
        }

        /// Food FBX ship without colliders. Add a single BoxCollider on the ROOT (so docking, which
        /// disables one collider, and tap-raycast both work) sized from the combined renderer bounds.
        /// Called while the object is at its imported transform, so bounds map cleanly to local space.
        private static void AddOptimalCollider(GameObject obj)
        {
            if (obj == null) return;

            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Quaternion origRot = obj.transform.rotation;
            obj.transform.rotation = Quaternion.identity;

            Bounds localBounds = rends[0].bounds;
            for (int k = 1; k < rends.Length; k++)
            {
                if (rends[k] != null && rends[k].enabled)
                {
                    localBounds.Encapsulate(rends[k].bounds);
                }
            }

            obj.transform.rotation = origRot;

            Vector3 ls = obj.transform.lossyScale;
            Vector3 unscaledSize = new Vector3(
                Mathf.Abs(localBounds.size.x / Mathf.Max(1e-4f, ls.x)),
                Mathf.Abs(localBounds.size.y / Mathf.Max(1e-4f, ls.y)),
                Mathf.Abs(localBounds.size.z / Mathf.Max(1e-4f, ls.z)));

            Vector3 localCenter = obj.transform.InverseTransformPoint(localBounds.center);

            float maxDim = Mathf.Max(unscaledSize.x, unscaledSize.y, unscaledSize.z);
            float minDim = Mathf.Min(unscaledSize.x, unscaledSize.y, unscaledSize.z);

            if (maxDim / Mathf.Max(1e-4f, minDim) < 1.6f)
            {
                SphereCollider sphere = obj.GetComponent<SphereCollider>();
                if (sphere == null) sphere = obj.AddComponent<SphereCollider>();
                sphere.center = localCenter;
                sphere.radius = maxDim * 0.48f;
            }
            else
            {
                BoxCollider box = obj.GetComponent<BoxCollider>();
                if (box == null) box = obj.AddComponent<BoxCollider>();
                box.center = localCenter;
                box.size = unscaledSize;
            }
        }

        /// Caps how big a pile item can be so `countToSpawn` of them can actually fit the spawn area
        /// without burying the tray in overlap — treats each item as roughly circular footprint and
        /// leaves some packing slack (0.85) for gaps between neighbours.
        private float ComputeAutoMaxItemSize(int countToSpawn)
        {
            float areaW = spawnAreaSize.x * 1.5f;
            float areaH = spawnAreaSize.y * 1.5f;
            float areaPerItem = (areaW * areaH) / Mathf.Max(1, countToSpawn);
            return Mathf.Max(1.10f, Mathf.Sqrt(areaPerItem) * 1.2f);
        }

        /// Largest renderer dimension in the object's LOCAL space (independent of its current scale),
        /// used to normalize differently-sized food models to one consistent pile size.
        private static float LocalMaxDimension(GameObject obj)
        {
            return GetTrueVisualMaxExtent(obj);
        }

        private static float GetTrueVisualMaxExtent(GameObject obj)
        {
            if (obj == null) return 1f;

            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return 1f;

            Vector3 origScale = obj.transform.localScale;
            Quaternion origRot = obj.transform.rotation;

            obj.transform.localScale = Vector3.one;
            obj.transform.rotation = Quaternion.identity;

            Bounds combined = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].enabled)
                {
                    combined.Encapsulate(rends[i].bounds);
                }
            }

            obj.transform.localScale = origScale;
            obj.transform.rotation = origRot;

            float maxExtent = Mathf.Max(combined.size.x, combined.size.y, combined.size.z);
            return maxExtent > 1e-4f ? maxExtent : 1f;
        }

        /// Gathers the remaining (not-yet-docked) pile objects back toward the tray's center with
        /// a smooth tween instead of destroying/respawning, so leftover pieces stuck near the
        /// walls or corners get pulled back into easy reach and rejumbled.
        public void GatherAndReshuffleRemaining()
        {
            HashSet<GameObject> targetObjs = new HashSet<GameObject>();

            foreach (GameObject obj in spawnedObjects)
            {
                if (obj != null) targetObjs.Add(obj);
            }

            FindTargetObject[] sceneItems = Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            foreach (FindTargetObject item in sceneItems)
            {
                if (item != null && !item.isDocked && item.gameObject != null)
                {
                    targetObjs.Add(item.gameObject);
                }
            }

            foreach (GameObject obj in targetObjs)
            {
                if (obj == null) continue;

                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp != null && targetComp.isDocked) continue;

                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb == null) continue;

                float posX = Random.Range(-spawnAreaSize.x * 0.35f, spawnAreaSize.x * 0.35f);
                float posZ = Random.Range(-spawnAreaSize.y * 0.35f, spawnAreaSize.y * 0.35f);
                float posY = Random.Range(spawnHeightMin + 0.1f, spawnHeightMax + 0.5f);
                Vector3 targetPos = transform.position + new Vector3(posX, posY, posZ);
                Quaternion targetRot = Random.rotation;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;

                obj.transform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(obj.transform.DOMove(targetPos, 0.45f).SetEase(Ease.OutQuad));
                seq.Join(obj.transform.DORotateQuaternion(targetRot, 0.45f).SetEase(Ease.OutQuad));
                seq.OnComplete(() =>
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.WakeUp();
                    }
                });
            }
        }

        public void ClearObjects()
        {
            // Items already collected into a dock slot must survive a reshuffle: rescue them out
            // of this pile's container before it gets destroyed below.
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj == null) continue;

                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp != null && targetComp.isDocked)
                {
                    obj.transform.SetParent(null, true);
                    continue;
                }

                Destroy(obj);
            }
            spawnedObjects.Clear();

            Transform oldContainer = transform.Find("Spawned_Physics_Objects");
            if (oldContainer != null)
            {
                Destroy(oldContainer.gameObject);
            }
        }

        private GameObject FindPrefabForItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (foodModels != null)
            {
                foreach (var model in foodModels)
                {
                    if (model != null && model.name.Equals(itemId, System.StringComparison.OrdinalIgnoreCase)) return model;
                }
                foreach (var model in foodModels)
                {
                    if (model != null && model.name.ToLowerInvariant().Contains(itemId.ToLowerInvariant())) return model;
                }
            }

            if (CanvasUIDesignManager.Instance != null)
            {
                GameObject displayPrefab = CanvasUIDesignManager.Instance.FindDisplayPrefabForItem(itemId);
                if (displayPrefab != null) return displayPrefab;
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position + Vector3.up * ((spawnHeightMin + spawnHeightMax) * 0.5f);
            Vector3 size = new Vector3(spawnAreaSize.x, spawnHeightMax - spawnHeightMin, spawnAreaSize.y);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
