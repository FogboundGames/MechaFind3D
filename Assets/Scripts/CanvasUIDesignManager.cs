using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Professional 2-Box Cardboard Sorting & Packaging Manager.
    /// Manages 2 open cardboard box slots, item filling, item icon badges, and slow DOTween box closing & shipping animations.
    /// </summary>
    public class CanvasUIDesignManager : MonoBehaviour
    {
        public static CanvasUIDesignManager Instance { get; private set; }

        [Header("Canvas Configuration")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);

        [Header("Mini 3D Object Slot Holder Alignment")]
        [SerializeField] private float miniObjectDockScale = 0.08f;
        [SerializeField] private float dockItemFillRatio = 0.45f;
        [SerializeField] private float dockCameraDepth = 1.6f;
        [SerializeField] private float uiPlaneDistance = 3.2f;

        [Header("3D Cardboard Box Packaging (DOTween Animation)")]
        [SerializeField] private GameObject cardboardBoxOpenedPrefab;
        [Tooltip("Optional material for the packaging box. Box.fbx ships a single untextured material, so leave empty only if you are happy with the model's own look.")]
        [SerializeField] private Material boxMaterialOverride;

        [Header("3D Conveyor Belt (replaces the flat UI conveyor)")]
        [SerializeField] private GameObject conveyorPrefab;
        [Tooltip("Belt height as a fraction of a dock box's height. Drives the tile scale, and with it how many tiles fit side by side.")]
        [Range(0.15f, 1.5f)]
        [SerializeField] private float conveyorHeightFraction = 0.6f;
        [Tooltip("How far the boxes sink into the belt's top surface, in world units. Slightly positive hides any gap under them.")]
        [SerializeField] private float conveyorSinkIntoBoxes = 0.01f;
        [Tooltip("Stripe scroll speed in LOOPS per second. Negative runs the belt to the RIGHT, which is the direction the packed boxes travel.")]
        [SerializeField] private float conveyorSpeed = -0.2f;
        [Tooltip("Mirrors the stripe layout so the arrow heads point right, matching the belt's travel direction.")]
        [SerializeField] private bool conveyorFlipStripes = true;
        [Tooltip("How many belt pallets to lay across the shelf. They stretch to fill the run, so fewer means bigger. 0 auto-fits them from Conveyor Height Fraction instead.")]
        [Min(0)]
        [SerializeField] private int conveyorTileCount = 6;
        [Tooltip("Linear move speed of completed boxes along the conveyor belt (world units per second). Matches belt travel direction.")]
        [SerializeField] private float conveyorBoxMoveSpeed = 0.40f;
#pragma warning disable CS0414
        [Tooltip("If true, spawns initial completed boxes on the conveyor belt at start so it moves continuously right away.")]
        [SerializeField] private bool spawnInitialConveyorBoxes = false;
        [Tooltip("Show only every Nth arrow on a pallet. 1 shows all eight the model ships.")]
        [Min(1)]
        [SerializeField] private int conveyorArrowStride = 1;
        [Tooltip("Size multiplier on the arrows. 1 leaves them exactly as modelled.")]
        [Min(0.1f)]
        [SerializeField] private float conveyorArrowScale = 1f;
        [Tooltip("Number of completed boxes to fit side-by-side in a single horizontal row before starting a new row.")]
        [SerializeField] private int completedBoxesPerRow = 4;
#pragma warning restore CS0414

        private const int MAX_SLOTS = 3;
        private const int ITEMS_PER_BOX = 3;

        // Box.fbx ships its own lid-folding animation, baked into a single four-flap clip by
        // MechaFind3D → Kutu → Box.fbx Kapanma Klibini Üret and played by PackagingBoxFlaps. The
        // hand-tuned per-bone quaternions the old "Cardboard Box (Rigged)" prefab needed are gone.
        private const string PackagingBoxResourcePath = "CardboardBox/PackagingBox";

        // Box.fbx was authored Z-up, so its imported prefab root carries a baked Euler(-90, 0, 0) that
        // stands the box upright. Assigning an ABSOLUTE rotation (the old code's Quaternion.Euler(18,0,0))
        // throws that away and lays the box on its side. Every display rotation is therefore composed on
        // top of the prefab's own rest rotation, which is captured once when the prefab is loaded.
        private static readonly Vector3 BoxSlotTiltEuler = new Vector3(18f, 0f, 0f);
        private static readonly Vector3 BoxShelfTiltEuler = new Vector3(16f, 10f, 0f);

        private Quaternion boxRestRotation = Quaternion.identity;

        private const string ConveyorResourcePath = "Conveyor/ConveyorBelt";
        private GameObject conveyorInstance;
        private float conveyorTopOffsetY;

        private Quaternion BoxDisplayRotation(Vector3 tiltEuler)
        {
            return Quaternion.Euler(tiltEuler) * boxRestRotation;
        }

        /// <summary>
        /// DOJump arcs along world +Y, but the dock plane sits only <see cref="dockCameraDepth"/> units in
        /// front of a camera that is pitched steeply down - so world +Y points largely back INTO the lens,
        /// and every unit of jump power eats |dot(up, camForward)| units of view depth. The old fixed
        /// powers (1.0 for items, 0.9 for the box) put the arc's apex ~0.75-0.85 units from the camera,
        /// i.e. inside its face. This caps the apex at a fraction of the dock depth instead, so the arc
        /// stays a readable hop no matter how the camera angle or dock depth is retuned.
        /// </summary>
        /// <summary>
        /// DOPunchScale's punch is in ABSOLUTE local-scale units, not a multiplier. The old box prefab sat
        /// near scale 1 so a 0.18 punch was a light squash; Box.fbx is ~24 units wide natively, so it fits
        /// its slot at scale ~0.0066 and the same 0.18 punched it to 26x its size for a moment - the box
        /// appearing to explode or leap at the camera whenever an item landed in it. Scaling the punch by
        /// the box's current scale keeps the squash proportional whatever model is used.
        ///
        /// The box is also parked in <see cref="tweeningDockObjects"/> for the duration, because
        /// AlignDocked3DObjectsWithSlots otherwise rewrites localScale every frame and fights the punch.
        /// </summary>
        private void PunchBox(GameObject box, Vector3 relativePunch, float duration, int vibrato, float elasticity)
        {
            if (box == null) return;

            float scale = box.transform.localScale.x;
            box.transform.DOKill(true);

            tweeningDockObjects.Add(box);
            box.transform.DOPunchScale(relativePunch * scale, duration, vibrato, elasticity)
                .OnComplete(() =>
                {
                    tweeningDockObjects.Remove(box);
                    if (box != null) box.transform.localScale = Vector3.one * scale;
                });
        }

        private float GetDockJumpPower(float maxPower)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null) return maxPower;

            const float maxDepthFractionSpent = 0.25f;

            float depthLostPerUnit = Mathf.Abs(Vector3.Dot(Vector3.up, mainCamera.transform.forward));
            if (depthLostPerUnit < 0.05f) return maxPower;

            float safePower = dockCameraDepth * maxDepthFractionSpent / depthLostPerUnit;
            return Mathf.Min(maxPower, safePower);
        }

        private readonly GameObject[] slotBox = new GameObject[MAX_SLOTS];
        private readonly string[] slotAssignedItemName = new string[MAX_SLOTS];
        private readonly List<DockItemData>[] slotBoxContents = new List<DockItemData>[MAX_SLOTS];
        private readonly int[] slotRequiredCount = new int[MAX_SLOTS];

        private Canvas mainCanvas;
        private RectTransform topGoalContainer;
        private RectTransform bottomDockContainer;

        private readonly List<RectTransform> slotRects = new List<RectTransform>();
        private readonly List<Text> slotBadgeTexts = new List<Text>();
        private readonly List<Image> slotItemIconImages = new List<Image>();
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private Camera mainCamera;
        private bool isProcessingMatch = false;
        private bool warnedAboutFlaps = false;

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private static readonly string[] GoalCardPalette = { "Pink", "Yellow", "Green", "Blue" };

        private static Sprite LoadUISprite(string resourcesPath)
        {
            if (!spriteCache.TryGetValue(resourcesPath, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(resourcesPath);
                spriteCache[resourcesPath] = sprite;
            }
            return sprite;
        }

        private static Sprite ButtonSprite(string colorName) => LoadUISprite($"Buttons/Button {colorName}");
        private static Sprite IconSprite(string iconName) => LoadUISprite($"Icons/{iconName}");

        private static void ApplySlicedSprite(Image img, Sprite sprite)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                slotBoxContents[i] = new List<DockItemData>();
            }

            EnsureCanvasStructure();
        }

        private void Start()
        {
            Ensure3DCardboardBoxes();

            // Deferred by a frame on purpose. The belt's width and tile count are baked once from the
            // shipped-box shelf, and that shelf is derived from the canvas layout - which has not settled
            // during Start(). Building here left the belt sized from a stale layout while the per-frame
            // follow moved it to the settled position, so it sat off-centre from the shelf.
            StartCoroutine(BuildConveyorAfterLayout());

            if (LevelManager.Instance == null)
            {
                if (MatchGoalManager.Instance != null)
                {
                    MatchGoalManager.Instance.SetupLevelGoals();
                }
                RefreshTargetGoalsUI();
            }
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            if (mainCanvas != null && mainCanvas.worldCamera == null && mainCamera != null)
            {
                mainCanvas.worldCamera = mainCamera;
            }

            AlignDocked3DObjectsWithSlots();
            UpdateConveyorBoxes();
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Build Canvas UI Design")]
        public static void BuildCanvasUIDesignTool()
        {
            GameObject sceneController = GameObject.Find("Physics_Scene_Controller");
            if (sceneController == null)
            {
                sceneController = new GameObject("Physics_Scene_Controller");
            }

            CanvasUIDesignManager manager = sceneController.GetComponent<CanvasUIDesignManager>();
            if (manager == null)
            {
                manager = sceneController.AddComponent<CanvasUIDesignManager>();
            }

            manager.EnsureCanvasStructure();
            Selection.activeGameObject = sceneController;
            Debug.Log("🎨 Professional 2-Box Cardboard Packaging System Built Successfully!");
        }
#endif

        public void EnsureCanvasStructure()
        {
            EnsureEventSystem();

            Transform existingCanvas = transform.Find("MatchFactory_Canvas");
            if (existingCanvas != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingCanvas.gameObject);
#else
                Destroy(existingCanvas.gameObject);
#endif
            }

            GameObject canvasObj = new GameObject("MatchFactory_Canvas");
            canvasObj.transform.SetParent(transform);

            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            mainCanvas.worldCamera = mainCamera;
            mainCanvas.planeDistance = uiPlaneDistance;
            mainCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            BuildHeaderGoalPanel(canvasObj.transform);
            BuildBottomDockPanel(canvasObj.transform);
            BuildShuffleButton(canvasObj.transform);
            Ensure3DCardboardBoxes();
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esObj.AddComponent<InputSystemUIInputModule>();
#else
            esObj.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildHeaderGoalPanel(Transform parent)
        {
            GameObject headerObj = new GameObject("Header_Goal_Panel");
            headerObj.transform.SetParent(parent, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -40);
            headerRect.sizeDelta = new Vector2(980, 160);

            Image bg = headerObj.AddComponent<Image>();
            ApplySlicedSprite(bg, ButtonSprite("Purple"));

            GameObject titleObj = new GameObject("Level_Badge");
            titleObj.transform.SetParent(headerObj.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 20);
            titleRect.sizeDelta = new Vector2(170, 136);

            Image titleBg = titleObj.AddComponent<Image>();
            titleBg.sprite = LoadUISprite("Panels/Ribbon Yellow");
            titleBg.type = Image.Type.Simple;
            titleBg.preserveAspect = true;
            titleBg.color = Color.white;

            GameObject titleTextObj = new GameObject("TextNode");
            titleTextObj.transform.SetParent(titleObj.transform, false);

            RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0.17f, 0.10f);
            titleTextRect.anchorMax = new Vector2(0.83f, 0.68f);
            titleTextRect.sizeDelta = Vector2.zero;

            Text titleTxt = titleTextObj.AddComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = new Color(0.35f, 0.18f, 0.02f);

            string titleStr = "SEVİYE 1";
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                titleStr = LevelManager.Instance.ActiveLevelData.levelTitle.ToUpperInvariant();
            }
            titleTxt.text = titleStr;
            titleTxt.alignment = TextAnchor.MiddleCenter;

            GameObject goalsContainer = new GameObject("Goals_Container");
            goalsContainer.transform.SetParent(headerObj.transform, false);

            topGoalContainer = goalsContainer.AddComponent<RectTransform>();
            topGoalContainer.anchorMin = new Vector2(0f, 0f);
            topGoalContainer.anchorMax = new Vector2(1f, 0.75f);
            topGoalContainer.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = goalsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }

        private void BuildBottomDockPanel(Transform parent)
        {
            GameObject dockObj = new GameObject("Bottom_Dock_Panel");
            dockObj.transform.SetParent(parent, false);

            RectTransform dockRect = dockObj.AddComponent<RectTransform>();
            dockRect.anchorMin = new Vector2(0.5f, 0f);
            dockRect.anchorMax = new Vector2(0.5f, 0f);
            dockRect.pivot = new Vector2(0.5f, 0f);
            dockRect.anchoredPosition = new Vector2(0, 65);
            dockRect.sizeDelta = new Vector2(980, 210);

            Image bg = dockObj.AddComponent<Image>();
            ApplySlicedSprite(bg, ButtonSprite("Violet"));

            GameObject slotsContainerObj = new GameObject("Slots_Container");
            slotsContainerObj.transform.SetParent(dockObj.transform, false);

            bottomDockContainer = slotsContainerObj.AddComponent<RectTransform>();
            bottomDockContainer.anchorMin = Vector2.zero;
            bottomDockContainer.anchorMax = Vector2.one;
            bottomDockContainer.sizeDelta = new Vector2(-20, 0);

            HorizontalLayoutGroup layout = slotsContainerObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 16, 16);
            layout.spacing = 35;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            slotRects.Clear();
            slotBadgeTexts.Clear();
            slotItemIconImages.Clear();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                GameObject slotObj = new GameObject($"DockSlot_{i}");
                slotObj.transform.SetParent(slotsContainerObj.transform, false);

                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                Image slotBg = slotObj.AddComponent<Image>();
                // Removed blue background square image for clean 3D box tray appearance!
                slotBg.color = Color.clear;

                // Item Icon Image displayed on/above each box
                GameObject iconObj = new GameObject("ItemIconBadge");
                iconObj.transform.SetParent(slotObj.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.88f);
                iconRect.anchorMax = new Vector2(0.5f, 0.88f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(72, 72);

                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
                iconObj.SetActive(false);

                // Slot badge text below box
                GameObject labelObj = new GameObject("LabelText");
                labelObj.transform.SetParent(slotObj.transform, false);

                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.22f);
                labelRect.sizeDelta = Vector2.zero;

                Text labelTxt = labelObj.AddComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.fontSize = 20;
                labelTxt.fontStyle = FontStyle.Bold;
                labelTxt.alignment = TextAnchor.MiddleCenter;
                if (i == 2)
                {
                    labelTxt.color = new Color(0.2f, 0.95f, 1.0f);
                    labelTxt.text = "🤖 MECHA";
                }
                else
                {
                    labelTxt.color = Color.yellow;
                    labelTxt.text = $"KUTU {i + 1}";
                }

                slotRects.Add(slotRect);
                slotBadgeTexts.Add(labelTxt);
                slotItemIconImages.Add(iconImg);
            }
        }

        private void BuildShuffleButton(Transform parent)
        {
            GameObject btnObj = new GameObject("Shuffle_Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(-330, 285);
            btnRect.sizeDelta = new Vector2(230, 65);

            Image btnBg = btnObj.AddComponent<Image>();
            ApplySlicedSprite(btnBg, ButtonSprite("Cyan"));

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() =>
            {
                PhysicsObjectSpawner spawner = Object.FindFirstObjectByType<PhysicsObjectSpawner>();
                if (spawner != null) spawner.GatherAndReshuffleRemaining();
            });

            GameObject btnIconObj = new GameObject("Icon");
            btnIconObj.transform.SetParent(btnObj.transform, false);

            RectTransform btnIconRect = btnIconObj.AddComponent<RectTransform>();
            btnIconRect.anchorMin = new Vector2(0f, 0.5f);
            btnIconRect.anchorMax = new Vector2(0f, 0.5f);
            btnIconRect.pivot = new Vector2(0f, 0.5f);
            btnIconRect.anchoredPosition = new Vector2(22, 0);
            btnIconRect.sizeDelta = new Vector2(30, 30);

            Image btnIconImg = btnIconObj.AddComponent<Image>();
            btnIconImg.sprite = IconSprite("Cycle");
            btnIconImg.type = Image.Type.Simple;
            btnIconImg.preserveAspect = true;

            GameObject btnTextObj = new GameObject("TextNode");
            btnTextObj.transform.SetParent(btnObj.transform, false);

            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = new Vector2(0f, 0f);
            btnTextRect.anchorMax = new Vector2(1f, 1f);
            btnTextRect.offsetMin = new Vector2(46, 0);
            btnTextRect.offsetMax = new Vector2(-10, 0);

            Text btnTxt = btnTextObj.AddComponent<Text>();
            btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnTxt.fontSize = 22;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.color = Color.white;
            btnTxt.text = "KARIŞTIR";
            btnTxt.alignment = TextAnchor.MiddleCenter;
        }

        public void RefreshTargetGoalsUI()
        {
            if (MatchGoalManager.Instance == null || topGoalContainer == null) return;

            foreach (Transform child in topGoalContainer)
            {
                Destroy(child.gameObject);
            }

            List<MatchGoal> goals = MatchGoalManager.Instance.levelGoals;
            if (goals == null || goals.Count == 0) return;

            for (int i = 0; i < goals.Count; i++)
            {
                MatchGoal goal = goals[i];
                GameObject cardObj = new GameObject($"GoalCard_{goal.colorName}_{goal.shapeType}");
                cardObj.transform.SetParent(topGoalContainer, false);

                RectTransform cardRect = cardObj.AddComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(400, 90);

                Image cardBg = cardObj.AddComponent<Image>();
                ApplySlicedSprite(cardBg, ButtonSprite(GoalCardPalette[i % GoalCardPalette.Length]));

                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(cardObj.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(15, 0);
                iconRect.sizeDelta = new Vector2(60, 60);

                Image iconImg = iconObj.AddComponent<Image>();
                Sprite foodIcon = string.IsNullOrEmpty(goal.colorName) ? null : IconSprite(goal.colorName);
                if (foodIcon != null)
                {
                    iconImg.sprite = foodIcon;
                    iconImg.type = Image.Type.Simple;
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                }
                else
                {
                    iconImg.color = goal.targetColor;
                }

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(cardObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0.22f, 0f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.sizeDelta = Vector2.zero;

                Text txt = textObj.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 26;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = goal.IsCompleted ? Color.green : Color.white;

                string label = string.IsNullOrEmpty(goal.colorName)
                    ? goal.colorName
                    : char.ToUpper(goal.colorName[0]) + goal.colorName.Substring(1);
                txt.text = goal.IsCompleted ? "✅ TAMAMLANDI" : $"{label}\nKalan: {goal.Remaining}";
            }
        }

        public static bool IsMechaItem(FindTargetObject item)
        {
            if (item == null) return false;
            if (item.name.Contains("Mecha") || item.name.Contains("meccha")) return true;
            if (item.colorName != null && (item.colorName.Equals("mecha", System.StringComparison.OrdinalIgnoreCase) || item.colorName.Contains("Mecha"))) return true;
            if (item.GetComponentInChildren<MechaRagdollSpawner>() != null) return true;
            if (item.transform.Find("MechaRagdoll") != null || item.transform.Find("meccha chameleon") != null) return true;

            foreach (Transform t in item.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Mecha") || t.name.Contains("meccha")) return true;
            }
            return false;
        }

        private int CountTotalMatchingObjectsInLevel(FindTargetObject targetItem)
        {
            if (targetItem == null) return 1;
            bool isMecha = IsMechaItem(targetItem);

            int count = 0;
            FindTargetObject[] allItems = Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (isMecha)
                {
                    if (IsMechaItem(item)) count++;
                }
                else
                {
                    if (!IsMechaItem(item) && item.colorName == targetItem.colorName) count++;
                }
            }
            return Mathf.Max(1, count);
        }

        public bool TryCollectItemToDock(FindTargetObject item)
        {
            if (isProcessingMatch || item == null) return false;

            bool isMecha = IsMechaItem(item);
            string itemType = isMecha ? "Mecha" : item.colorName;

            int targetSlot = -1;

            if (isMecha)
            {
                // Slot 2 is dedicated exclusively for Mecha
                int mechaSlot = 2;
                if (string.IsNullOrEmpty(slotAssignedItemName[mechaSlot]))
                {
                    slotAssignedItemName[mechaSlot] = "Mecha";
                    slotRequiredCount[mechaSlot] = CountTotalMatchingObjectsInLevel(item);
                }
                int req = slotRequiredCount[mechaSlot] > 0 ? slotRequiredCount[mechaSlot] : 1;
                if (slotAssignedItemName[mechaSlot] == "Mecha" && slotBoxContents[mechaSlot].Count < req)
                {
                    targetSlot = mechaSlot;
                }
            }
            else
            {
                // Slots 0 & 1 are for general items
                for (int i = 0; i < 2; i++)
                {
                    int req = slotRequiredCount[i] > 0 ? slotRequiredCount[i] : 3;
                    if (slotAssignedItemName[i] == itemType && slotBoxContents[i].Count < req)
                    {
                        targetSlot = i;
                        break;
                    }
                }

                if (targetSlot == -1)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (string.IsNullOrEmpty(slotAssignedItemName[i]))
                        {
                            slotAssignedItemName[i] = itemType;
                            slotRequiredCount[i] = CountTotalMatchingObjectsInLevel(item);
                            targetSlot = i;
                            break;
                        }
                    }
                }
            }

            if (targetSlot == -1)
            {
                return false;
            }

            if (isMecha)
            {
                Transform mechaTransform = item.transform;
                Transform hostTransform = null;

                if (!item.name.Contains("Mecha") && !item.name.Contains("meccha"))
                {
                    foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != item.transform && (child.name.Contains("Mecha") || child.name.Contains("meccha") || child.name.Contains("Ragdoll")))
                        {
                            mechaTransform = child;
                            hostTransform = item.transform;
                            break;
                        }
                    }
                }

                GameObject mechaObjToCollect;

                if (hostTransform != null)
                {
                    // UNPARENT MECHA SO HOST OBJECT STAYS IN THE PILE INTACT!
                    mechaTransform.SetParent(null, true);
                    mechaObjToCollect = mechaTransform.gameObject;

                    // Ensure mechaObjToCollect has a FindTargetObject component for dock tracking
                    FindTargetObject mechaTargetComp = mechaObjToCollect.GetComponent<FindTargetObject>();
                    if (mechaTargetComp == null)
                    {
                        mechaTargetComp = mechaObjToCollect.AddComponent<FindTargetObject>();
                        mechaTargetComp.Initialize(ObjectShapeType.Cube, Color.cyan, "Mecha");
                    }
                    mechaTargetComp.isDocked = true;

                    // Re-enable host object physics & colliders so it stays naturally in the pile
                    item.isDocked = false;
                    foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
                    {
                        if (c != null) c.enabled = true;
                    }
                    Rigidbody hostRb = item.GetComponent<Rigidbody>();
                    if (hostRb != null)
                    {
                        hostRb.isKinematic = false;
                        hostRb.WakeUp();
                    }
                }
                else
                {
                    item.isDocked = true;
                    mechaObjToCollect = item.gameObject;
                }

                Rigidbody rb = mechaObjToCollect.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                foreach (Renderer r in mechaObjToCollect.GetComponentsInChildren<Renderer>())
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                }

                Collider col = mechaObjToCollect.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                FindTargetObject mechaTarget = mechaObjToCollect.GetComponent<FindTargetObject>() ?? item;
                DockItemData data = new DockItemData
                {
                    targetObject = mechaTarget,
                    shapeType = mechaTarget.shapeType,
                    colorName = "Mecha",
                    objectColor = mechaTarget.objectColor
                };

                slotBoxContents[targetSlot].Add(data);

                int reqCount = slotRequiredCount[targetSlot] > 0 ? slotRequiredCount[targetSlot] : 1;
                bool willShip = slotBoxContents[targetSlot].Count >= reqCount;
                if (willShip)
                {
                    isProcessingMatch = true;
                }

                // Unscrew spin animation (720 deg Y rotation + hop up)
                Sequence unscrewSeq = DOTween.Sequence();
                tweeningDockObjects.Add(mechaObjToCollect);
                unscrewSeq.Append(mechaObjToCollect.transform.DORotate(new Vector3(0, 720, 0), 0.35f, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
                unscrewSeq.Join(mechaObjToCollect.transform.DOJump(mechaObjToCollect.transform.position, 0.40f, 1, 0.35f));
                unscrewSeq.OnComplete(() =>
                {
                    tweeningDockObjects.Remove(mechaObjToCollect);
                    AnimateItemIntoBox(mechaObjToCollect, targetSlot, () =>
                    {
                        if (willShip)
                        {
                            AnimateSlowBoxClosingAndShipping(targetSlot);
                        }
                    });
                });
            }
            else
            {
                item.isDocked = true;

                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                }

                Collider col = item.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                DockItemData data = new DockItemData
                {
                    targetObject = item,
                    shapeType = item.shapeType,
                    colorName = item.colorName,
                    objectColor = item.objectColor
                };

                slotBoxContents[targetSlot].Add(data);

                int reqCount = slotRequiredCount[targetSlot] > 0 ? slotRequiredCount[targetSlot] : 3;
                bool willShip = slotBoxContents[targetSlot].Count >= reqCount;
                if (willShip)
                {
                    isProcessingMatch = true;
                }

                AnimateItemIntoBox(item.gameObject, targetSlot, () =>
                {
                    if (willShip)
                    {
                        AnimateSlowBoxClosingAndShipping(targetSlot);
                    }
                });
            }

            UpdateSlotBadgesUI();
            return true;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (slotIndex < 0 || slotIndex >= slotRects.Count || mainCamera == null) return Vector3.zero;

            RectTransform rect = slotRects[slotIndex];
            Vector3 slotWorldCenter = rect.TransformPoint(rect.rect.center);

            Vector2 screenPos2D = RectTransformUtility.WorldToScreenPoint(CanvasEventCamera(), slotWorldCenter);
            Vector3 screenPoint = new Vector3(screenPos2D.x, screenPos2D.y, dockCameraDepth);
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }

        private float ComputeFitScaleForSlot(int slotIndex, GameObject obj3D = null)
        {
            if (slotIndex < 0 || slotIndex >= slotRects.Count || mainCamera == null) return miniObjectDockScale;

            RectTransform rect = slotRects[slotIndex];
            Camera uiCam = CanvasEventCamera();

            Vector3 worldMin = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin, 0f));
            Vector3 worldMax = rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.yMax, 0f));

            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(uiCam, worldMin);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(uiCam, worldMax);

            float slotScreenSize = Mathf.Min(Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));
            if (slotScreenSize <= 0f) return miniObjectDockScale;

            Vector2 screenCenter = (screenMin + screenMax) * 0.5f;
            Vector3 worldEdgeA = mainCamera.ScreenToWorldPoint(new Vector3(screenCenter.x - slotScreenSize * 0.5f, screenCenter.y, dockCameraDepth));
            Vector3 worldEdgeB = mainCamera.ScreenToWorldPoint(new Vector3(screenCenter.x + slotScreenSize * 0.5f, screenCenter.y, dockCameraDepth));

            float slotWorldSize = Vector3.Distance(worldEdgeA, worldEdgeB);
            float targetWorldSize = slotWorldSize * dockItemFillRatio;

            if (obj3D != null)
            {
                float localMax = GetObjectStaticUnscaledMaxExtent(obj3D);
                if (localMax > 1e-4f)
                {
                    return targetWorldSize / localMax;
                }
            }

            return targetWorldSize;
        }

        private static readonly Dictionary<int, float> unscaledExtentCache = new Dictionary<int, float>();

        private static float GetObjectStaticUnscaledMaxExtent(GameObject obj)
        {
            if (obj == null) return 1f;

            int instanceId = obj.GetInstanceID();
            if (unscaledExtentCache.TryGetValue(instanceId, out float cachedExtent) && cachedExtent > 1e-4f)
            {
                return cachedExtent;
            }

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
            float finalExtent = maxExtent > 1e-4f ? maxExtent : 1f;

            unscaledExtentCache[instanceId] = finalExtent;
            return finalExtent;
        }

        private Camera CanvasEventCamera()
        {
            return (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? mainCanvas.worldCamera : null;
        }

        private void LoadCardboardBoxPrefabsIfNull()
        {
            // The scene still has the old "Cardboard Box (Rigged)" prefab serialized into this field. That
            // box has an Armature/*.Top bone layout, which the Box.fbx clip's BoxFlap_* paths do not match,
            // so it would silently never fold. Anything without PackagingBoxFlaps is therefore treated as a
            // stale reference and replaced; assigning a prepared box that HAS the component still overrides.
            if (cardboardBoxOpenedPrefab != null && cardboardBoxOpenedPrefab.GetComponent<PackagingBoxFlaps>() == null)
            {
                cardboardBoxOpenedPrefab = null;
            }

            if (cardboardBoxOpenedPrefab == null)
            {
                cardboardBoxOpenedPrefab = Resources.Load<GameObject>(PackagingBoxResourcePath);
            }

            boxRestRotation = cardboardBoxOpenedPrefab != null
                ? cardboardBoxOpenedPrefab.transform.rotation
                : Quaternion.identity;

            if (cardboardBoxOpenedPrefab == null)
            {
                Debug.LogError(
                    $"📦 Paketleme kutusu prefab'ı yok (Resources/{PackagingBoxResourcePath}). " +
                    "Unity menüsünden 'MechaFind3D → Kutu → Box.fbx Kapanma Klibini Üret' komutunu bir kez çalıştır.");
            }
        }

        /// <summary>Instantiates a packaging box, strips its physics and leaves it in the open pose.</summary>
        private GameObject CreatePackagingBox(int slotIndex)
        {
            if (cardboardBoxOpenedPrefab == null) return null;

            GameObject box = Instantiate(cardboardBoxOpenedPrefab, transform);
            box.name = $"Slot3DBox_{slotIndex}";
            StripPhysicsFromVisual(box);

            if (box.GetComponent<PackagingBoxFlaps>() == null) box.AddComponent<PackagingBoxFlaps>();

            if (!warnedAboutFlaps && !HasFlapParts(box))
            {
                warnedAboutFlaps = true;
                Debug.LogError($"📦 '{cardboardBoxOpenedPrefab.name}' üzerinde BoxFlap_* parçaları yok; " +
                               "kapanma klibinin yolları tutmaz ve kapaklar hiç katlanmaz.");
            }

            SetBoxFlapsInstant(box, false);

            if (boxMaterialOverride != null)
            {
                foreach (Renderer r in box.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = boxMaterialOverride;
                }
            }

            return box;
        }

        private static void StripPhysicsFromVisual(GameObject go)
        {
            if (go == null) return;
            foreach (var pusher in go.GetComponentsInChildren<MechaFind3D.PhysicsInteraction.ColliderContactPusher>(true)) Destroy(pusher);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        }

        private static bool HasFlapParts(GameObject box)
        {
            foreach (Transform t in box.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("BoxFlap")) return true;
            }
            return false;
        }

        private static void SetBoxFlapsInstant(GameObject box, bool closed)
        {
            if (box == null) return;
            if (box.TryGetComponent(out PackagingBoxFlaps flaps)) flaps.SetClosedInstant(closed);
        }

        /// <summary>Joins the box's own baked lid-folding animation into an existing sequence so all four flaps fold together, in place (no position/scale change on the box itself).</summary>
        private static void AnimateBoxFlaps(Sequence seq, GameObject box, bool closing, float duration)
        {
            if (box == null) return;
            if (!box.TryGetComponent(out PackagingBoxFlaps flaps)) return;

            Tween fold = flaps.Fold(closing, duration);
            if (fold != null) seq.Join(fold);
        }

        private Vector3 GetHeaderGoalWorldPosition()
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (topGoalContainer == null || mainCamera == null) return new Vector3(0f, 4f, dockCameraDepth);

            Vector3 goalCenter = topGoalContainer.TransformPoint(topGoalContainer.rect.center);
            Vector2 screenPos2D = RectTransformUtility.WorldToScreenPoint(CanvasEventCamera(), goalCenter);
            Vector3 screenPoint = new Vector3(screenPos2D.x, screenPos2D.y, dockCameraDepth);
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }

        private void CleanupAllOldCardboardBoxes()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (slotBox[i] != null) Destroy(slotBox[i]);
                slotBox[i] = null;
            }

            for (int i = completedBoxObjects.Count - 1; i >= 0; i--)
            {
                if (completedBoxObjects[i] != null) Destroy(completedBoxObjects[i]);
            }
            completedBoxObjects.Clear();
            completedBoxesCount = 0;

            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in allObjects)
            {
                if (go != null && (go.name.Contains("Slot3DBox") || go.name.Contains("Delivery_3DBox")
                                   || go.name.StartsWith("Cardboard Box") || go.name.StartsWith("PackagingBox")
                                   || go.name.StartsWith("ConveyorInitialBox")))
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
        }

        private void Ensure3DCardboardBoxes()
        {
            LoadCardboardBoxPrefabsIfNull();
            CleanupAllOldCardboardBoxes();

            if (cardboardBoxOpenedPrefab != null)
            {
                for (int i = 0; i < MAX_SLOTS; i++)
                {
                    GameObject box = CreatePackagingBox(i);
                    if (box == null) continue;

                    float fitScale = ComputeFitScaleForSlot(i, box) * 1.25f;
                    box.transform.localScale = Vector3.one * fitScale;
                    box.transform.rotation = BoxDisplayRotation(BoxSlotTiltEuler);
                    box.transform.position = GetSlotWorldPosition(i);

                    slotBox[i] = box;
                }
            }
        }

        /// <summary>
        /// Swaps the hand-built flat UI conveyor for the 3D Conveyor.fbx belt.
        ///
        /// The old conveyor exists ONLY in the scene (a Conveyor_Belt_Panel Image plus a Chevron_Arrows
        /// Text faking the arrows with characters) - no code creates it - so it is found by name and
        /// switched off rather than deleted, leaving the scene authoring intact and reversible.
        /// The 3D belt is then fitted to exactly the screen footprint the panel used to occupy, so the
        /// dock boxes keep sitting on it without any of their own layout maths changing.
        /// </summary>
        private System.Collections.IEnumerator BuildConveyorAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            EnsureConveyorBelt();
        }

        private void EnsureConveyorBelt()
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null) return;

            if (conveyorPrefab == null) conveyorPrefab = Resources.Load<GameObject>(ConveyorResourcePath);
            if (conveyorPrefab == null)
            {
                Debug.LogError($"🎞️ Konveyör prefab'ı yok (Resources/{ConveyorResourcePath}). " +
                               "Unity menüsünden 'MechaFind3D → Konveyör → Conveyor.fbx Prefab'ını Üret' komutunu bir kez çalıştır.");
                return;
            }

            if (!TryGetShippedBoxRow(out Vector3 spanCentre, out float spanWidth, out float boxBottomY, out float boxHeight))
            {
                Debug.LogWarning("🎞️ Gönderilen koli rafı hesaplanamadı; 3D kayış yerleştirilemedi.");
                return;
            }

            if (conveyorInstance != null) Destroy(conveyorInstance);

            Quaternion beltRotation = Quaternion.Euler(BoxShelfTiltEuler.x, 0f, 0f)
                                      * conveyorPrefab.transform.rotation;
            Vector3 topSurface = new Vector3(spanCentre.x, boxBottomY + conveyorSinkIntoBoxes, spanCentre.z);

            int tilesToBuild = conveyorTileCount < 6 ? 6 : conveyorTileCount;

            conveyorInstance = ConveyorBelt.BuildRow(conveyorPrefab, transform, mainCamera,
                                                     topSurface, beltRotation,
                                                     spanWidth, boxHeight * conveyorHeightFraction,
                                                     conveyorSpeed, conveyorFlipStripes,
                                                     tilesToBuild, conveyorArrowStride, conveyorArrowScale);

            conveyorTopOffsetY = 0f;
            if (conveyorInstance != null)
            {
                Bounds beltBounds = default;
                bool beltHas = false;
                foreach (Renderer r in conveyorInstance.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!beltHas) { beltBounds = r.bounds; beltHas = true; }
                    else beltBounds.Encapsulate(r.bounds);
                }
                if (beltHas) conveyorTopOffsetY = beltBounds.max.y - conveyorInstance.transform.position.y;
            }

            RectTransform legacyPanel = FindConveyorPanel();
            if (legacyPanel != null && legacyPanel != bottomDockContainer)
            {
                foreach (Graphic g in legacyPanel.GetComponentsInChildren<Graphic>(true))
                {
                    g.enabled = false;
                }
            }
        }

        private float GetIdealConveyorBoxSpacing()
        {
            float footprint = EstimateShippedBoxFootprint();
            return footprint * 1.35f;
        }

        /// <summary>
        /// Moves completed boxes continuously along the belt towards the right, maintaining perfect, uniform spacing
        /// between consecutive boxes and wrapping off-screen right back to off-screen left.
        /// </summary>
        private void UpdateConveyorBoxes()
        {
            if (mainCamera == null || completedBoxObjects == null || completedBoxObjects.Count == 0) return;

            float shelfDepth = dockCameraDepth + 0.40f;
            Vector3 leftBoundWorld = mainCamera.ViewportToWorldPoint(new Vector3(-0.15f, 0.235f, shelfDepth));
            Vector3 rightBoundWorld = mainCamera.ViewportToWorldPoint(new Vector3(1.15f, 0.235f, shelfDepth));

            float minX = leftBoundWorld.x;
            float maxX = rightBoundWorld.x;
            float spanX = maxX - minX;

            Vector3 camRight = mainCamera.transform.right;
            float step = conveyorBoxMoveSpeed * Time.deltaTime;
            float idealSpacing = GetIdealConveyorBoxSpacing();

            int leadIndex = -1;
            for (int i = 0; i < completedBoxObjects.Count; i++)
            {
                if (completedBoxObjects[i] != null && !tweeningDockObjects.Contains(completedBoxObjects[i]))
                {
                    leadIndex = i;
                    break;
                }
            }

            if (leadIndex < 0) return;

            GameObject leadBox = completedBoxObjects[leadIndex];
            Vector3 leadPos = leadBox.transform.position + camRight * step;
            if (leadPos.x > maxX) leadPos.x = minX + (leadPos.x - maxX);
            else if (leadPos.x < minX) leadPos.x = maxX - (minX - leadPos.x);

            leadBox.transform.position = leadPos;
            leadBox.transform.rotation = BoxDisplayRotation(BoxShelfTiltEuler);

            for (int i = 0; i < completedBoxObjects.Count; i++)
            {
                if (i == leadIndex) continue;
                GameObject box = completedBoxObjects[i];
                if (box == null || tweeningDockObjects.Contains(box)) continue;

                int relativeOffset = i - leadIndex;
                Vector3 targetPos = leadPos - camRight * (relativeOffset * idealSpacing);

                while (targetPos.x < minX) targetPos.x += spanX;
                while (targetPos.x > maxX) targetPos.x -= spanX;

                box.transform.position = targetPos;
                box.transform.rotation = BoxDisplayRotation(BoxShelfTiltEuler);
            }
        }

        private static RectTransform FindConveyorPanel()
        {
            GameObject panelObj = GameObject.Find("Conveyor_Belt_Panel");
            return panelObj != null ? panelObj.GetComponent<RectTransform>() : null;
        }

        private void FollowDockBoxesWithConveyor()
        {
            if (conveyorInstance == null) return;
            if (!TryGetShippedBoxRow(out Vector3 centre, out _, out float bottomY, out _)) return;

            conveyorInstance.transform.position = new Vector3(
                centre.x,
                bottomY + conveyorSinkIntoBoxes - conveyorTopOffsetY,
                centre.z);
        }

        private bool TryGetShippedBoxRow(out Vector3 centre, out float width, out float bottomY, out float boxHeight)
        {
            centre = Vector3.zero;
            width = 0f;
            bottomY = 0f;
            boxHeight = 0f;

            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null || slotRects.Count == 0) return false;

            float footprint = EstimateShippedBoxFootprint();
            float shelfDepth = dockCameraDepth + 0.40f;

            Vector3 leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(-0.25f, 0.235f, shelfDepth));
            Vector3 rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1.25f, 0.235f, shelfDepth));

            centre = (leftEdge + rightEdge) * 0.5f;
            width = Vector3.Distance(leftEdge, rightEdge);
            bottomY = leftEdge.y;
            boxHeight = footprint;

            Bounds shipped = default;
            bool has = false;
            foreach (GameObject box in completedBoxObjects)
            {
                if (box == null) continue;
                foreach (Renderer r in box.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!has) { shipped = r.bounds; has = true; }
                    else shipped.Encapsulate(r.bounds);
                }
            }

            if (has)
            {
                bottomY = shipped.min.y;
                boxHeight = shipped.size.y;
            }

            return true;
        }

        private Quaternion GetDockItemSidewaysRotation()
        {
            return Quaternion.Euler(22f, 35f, 0f);
        }

        private Vector3 GetItemPositionInsideBox(int itemIndex, int totalRequired, Vector3 slotWorldPos)
        {
            int req = Mathf.Max(1, totalRequired);
            if (req <= 3)
            {
                // Single row layout for 1-3 items
                float spacing = req > 1 ? 0.08f / (req - 1) : 0f;
                float xOffset = (itemIndex - (req - 1) * 0.5f) * spacing;
                return slotWorldPos + new Vector3(xOffset, 0.01f, 0.02f);
            }
            else
            {
                // 2-Row Grid layout for 4+ items so items NEVER stick out of the box walls!
                int itemsPerRow = Mathf.CeilToInt(req / 2.0f);
                int row = itemIndex / itemsPerRow;
                int col = itemIndex % itemsPerRow;

                float colSpacing = itemsPerRow > 1 ? 0.07f / (itemsPerRow - 1) : 0f;
                float xOffset = (col - (itemsPerRow - 1) * 0.5f) * colSpacing;
                float zOffset = (row == 0) ? 0.035f : -0.025f;

                return slotWorldPos + new Vector3(xOffset, 0.01f, zOffset);
            }
        }

        private float GetItemFitScaleRatioInsideBox(int totalRequired)
        {
            if (totalRequired <= 3) return 0.50f;
            if (totalRequired <= 6) return 0.35f;
            return 0.28f;
        }

        private void AnimateItemIntoBox(GameObject obj3D, int slotIndex, System.Action onComplete)
        {
            if (obj3D == null)
            {
                onComplete?.Invoke();
                return;
            }

            Vector3 slotWorldPos = GetSlotWorldPosition(slotIndex);
            int reqCount = (slotIndex >= 0 && slotIndex < MAX_SLOTS) ? slotRequiredCount[slotIndex] : 3;
            if (reqCount <= 0) reqCount = 3;

            int currentItemIndex = (slotIndex >= 0 && slotIndex < MAX_SLOTS && slotBoxContents[slotIndex] != null) ? slotBoxContents[slotIndex].Count - 1 : 0;
            if (currentItemIndex < 0) currentItemIndex = 0;

            Vector3 boxItemPos = GetItemPositionInsideBox(currentItemIndex, reqCount, slotWorldPos);
            float scaleRatio = GetItemFitScaleRatioInsideBox(reqCount);

            float targetScaleVal = ComputeFitScaleForSlot(slotIndex, obj3D) * scaleRatio;
            Vector3 targetScale = Vector3.one * targetScaleVal;
            Quaternion targetRot = GetDockItemSidewaysRotation();

            tweeningDockObjects.Add(obj3D);
            obj3D.transform.DOKill();

            Vector3 initScale = obj3D.transform.localScale;

            Sequence seq = DOTween.Sequence();

            // Phase 1: Lift-off Pop / Anticipation (snappy 0.10s pulse scale up +25%)
            seq.Append(obj3D.transform.DOScale(initScale * 1.25f, 0.10f).SetEase(Ease.OutBack));

            // Phase 2: Parabolic Arc Flight into box (0.38s smooth jump + spin)
            seq.Append(obj3D.transform.DOJump(boxItemPos, GetDockJumpPower(1.15f), 1, 0.38f).SetEase(Ease.OutCubic));
            seq.Join(obj3D.transform.DOScale(targetScale, 0.38f).SetEase(Ease.OutQuad));
            seq.Join(obj3D.transform.DORotateQuaternion(targetRot, 0.38f).SetEase(Ease.OutQuad));

            // Phase 3: Impact Bounce & Box Squash & Stretch Reaction!
            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);

                if (obj3D != null)
                {
                    // Item elastic landing punch
                    obj3D.transform.DOPunchScale(targetScale * 0.25f, 0.22f, 8, 1f);
                }

                // Heavy impact squash-and-stretch punch on the box!
                if (slotIndex >= 0 && slotIndex < MAX_SLOTS && slotBox[slotIndex] != null)
                {
                    PunchBox(slotBox[slotIndex], new Vector3(0.24f, -0.16f, 0.24f), 0.35f, 8, 0.8f);
                }

                // Badge text punch pulse
                if (slotIndex >= 0 && slotIndex < slotBadgeTexts.Count && slotBadgeTexts[slotIndex] != null)
                {
                    slotBadgeTexts[slotIndex].transform.DOKill();
                    slotBadgeTexts[slotIndex].transform.localScale = Vector3.one;
                    slotBadgeTexts[slotIndex].transform.DOPunchScale(Vector3.one * 0.30f, 0.25f, 6, 1f);
                }

                onComplete?.Invoke();
            });
        }

        private void AlignDocked3DObjectsWithSlots()
        {
            FollowDockBoxesWithConveyor();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                Vector3 slotWorldPos = GetSlotWorldPosition(i);

                if (slotBox[i] != null && !tweeningDockObjects.Contains(slotBox[i]))
                {
                    slotBox[i].transform.position = Vector3.Lerp(slotBox[i].transform.position, slotWorldPos, Time.deltaTime * 22f);
                    float fitScale = ComputeFitScaleForSlot(i, slotBox[i]) * 1.25f;
                    slotBox[i].transform.localScale = Vector3.one * fitScale;
                    slotBox[i].transform.rotation = Quaternion.Slerp(slotBox[i].transform.rotation, BoxDisplayRotation(BoxSlotTiltEuler), Time.deltaTime * 15f);
                }

                List<DockItemData> itemsInBox = slotBoxContents[i];
                if (itemsInBox != null)
                {
                    int reqCount = slotRequiredCount[i] > 0 ? slotRequiredCount[i] : 3;
                    float scaleRatio = GetItemFitScaleRatioInsideBox(reqCount);

                    for (int k = 0; k < itemsInBox.Count; k++)
                    {
                        DockItemData data = itemsInBox[k];
                        if (data != null && data.targetObject != null && !tweeningDockObjects.Contains(data.targetObject.gameObject))
                        {
                            Vector3 boxItemPos = GetItemPositionInsideBox(k, reqCount, slotWorldPos);
                            data.targetObject.transform.position = Vector3.Lerp(data.targetObject.transform.position, boxItemPos, Time.deltaTime * 22f);
                            float fitScale = ComputeFitScaleForSlot(i, data.targetObject.gameObject) * scaleRatio;
                            data.targetObject.transform.localScale = Vector3.one * fitScale;
                            data.targetObject.transform.rotation = Quaternion.Slerp(data.targetObject.transform.rotation, GetDockItemSidewaysRotation(), Time.deltaTime * 15f);
                        }
                    }
                }
            }
        }

        private Sprite ExtractIconFromItem(DockItemData itemData)
        {
            if (itemData == null) return null;

            string rawName = itemData.colorName;
            if (!string.IsNullOrEmpty(rawName))
            {
                // Clean name (e.g. "Watermelon_003" -> "watermelon", "Sausage_01" -> "sausage")
                string cleanName = rawName.Split('_')[0].Trim().ToLower();
                Sprite icon = IconSprite(cleanName);
                if (icon != null) return icon;

                icon = IconSprite(rawName.ToLower());
                if (icon != null) return icon;

                icon = IconSprite(rawName);
                if (icon != null) return icon;
            }

            string shapeName = itemData.shapeType.ToString().ToLower();
            Sprite shapeIcon = IconSprite(shapeName);
            if (shapeIcon != null) return shapeIcon;

            return null;
        }

        /// <summary>
        /// Places an actual clone of the packed item (its real mesh/model, not a generic shape) on a
        /// small rounded card backdrop fixed to the top of the sealed box - like a label - so the box
        /// visibly and clearly shows exactly what was packed. Both pieces are parented under one group so
        /// they move together with the box and never end up floating loose next to it.
        /// </summary>
        private void AttachItemVisualToBox(GameObject box, DockItemData itemData)
        {
            if (box == null || itemData == null || itemData.targetObject == null) return;

            Transform existingGroup = box.transform.Find("Box_Item_Display");
            if (existingGroup != null) Destroy(existingGroup.gameObject);

            // Create display group parented to box lid
            GameObject group = new GameObject("Box_Item_Display");
            group.transform.SetParent(box.transform, false);

            // Calculate top of box lid
            Bounds boxBounds = default;
            bool hasBoxBounds = false;
            foreach (Renderer r in box.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                if (!hasBoxBounds) { boxBounds = b; hasBoxBounds = true; }
                else boxBounds.Encapsulate(b);
            }

            Vector3 topWorldPos = hasBoxBounds
                ? new Vector3(boxBounds.center.x, boxBounds.max.y + 0.04f, boxBounds.center.z)
                : box.transform.position + Vector3.up * 0.25f;

            group.transform.position = topWorldPos;

            // Instantiate clone of packed item model
            GameObject display = Instantiate(itemData.targetObject.gameObject);
            display.name = "ItemModel";
            display.transform.SetParent(group.transform, false);

            display.SetActive(true);
            display.transform.localPosition = Vector3.zero;
            display.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);

            // Restore scales and active state on all children
            display.transform.localScale = Vector3.one;
            foreach (Transform childTrans in display.GetComponentsInChildren<Transform>(true))
            {
                if (childTrans != null)
                {
                    childTrans.gameObject.SetActive(true);
                    childTrans.localScale = Vector3.one;
                }
            }

            // Remove colliders, rigidbodies, and scripts
            foreach (Component comp in display.GetComponentsInChildren<Component>(true))
            {
                if (comp is Transform || comp is Renderer || comp is MeshFilter || comp is SkinnedMeshRenderer) continue;
                Destroy(comp);
            }

            // Enable renderers
            foreach (Renderer r in display.GetComponentsInChildren<Renderer>())
            {
                if (r != null)
                {
                    r.enabled = true;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }

            float boxLidSize = hasBoxBounds ? Mathf.Min(boxBounds.size.x, boxBounds.size.z) : 0.35f;
            float itemSize = GetObjectStaticUnscaledMaxExtent(display);
            float desiredWorldSize = boxLidSize * 0.45f;
            float scaleFactor = itemSize > 1e-4f ? desiredWorldSize / itemSize : 0.12f;
            display.transform.localScale = Vector3.one * scaleFactor;

            group.transform.DOKill();
            group.transform.localScale = Vector3.zero;
            group.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        private int completedBoxesCount = 0;
        private readonly List<GameObject> completedBoxObjects = new List<GameObject>();

        private const int COMPLETED_BOXES_PER_ROW = 4;

        /// <summary>
        /// Computes the world-space footprint a box has once it lands on the shelf, from the same
        /// slot-size math used to size dock boxes rather than reading a live box's current bounds.
        /// A live read could land mid-tween (e.g. the box shrinking/growing during its own closing or
        /// respawn animation) and return a transient size, which made consecutive shelf gaps uneven.
        /// The unscaled mesh extent cancels out of ComputeFitScaleForSlot's ratio, so this reproduces
        /// the exact final on-shelf size (dock fit * 1.30 ship base * 1.15 shelf scale) deterministically.
        /// </summary>
        private float EstimateShippedBoxFootprint()
        {
            if (slotRects.Count == 0) return 0.5f;

            float targetWorldSize = ComputeFitScaleForSlot(0);
            float footprint = targetWorldSize * 1.30f * 1.15f;
            return footprint > 1e-4f ? footprint : 0.5f;
        }

        private int GetTotalExpectedCompletedBoxes()
        {
            if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null && MatchGoalManager.Instance.levelGoals.Count > 0)
            {
                int totalBoxes = 0;
                foreach (var goal in MatchGoalManager.Instance.levelGoals)
                {
                    totalBoxes += Mathf.Max(1, Mathf.CeilToInt(goal.totalRequired / 3.0f));
                }
                if (totalBoxes > 0) return Mathf.Max(4, totalBoxes);
            }
            return 4;
        }

        public Vector3 GetRedMarkedCompletedBoxWorldPos(int index)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            float depth = dockCameraDepth + 0.40f;

            if (mainCamera == null) return new Vector3(-1.2f + index * 0.65f, 0.60f, -1.6f);

            float idealSpacing = GetIdealConveyorBoxSpacing();
            Vector3 camRight = mainCamera.transform.right;

            float minX = mainCamera.ViewportToWorldPoint(new Vector3(-0.15f, 0.235f, depth)).x;
            float maxX = mainCamera.ViewportToWorldPoint(new Vector3(1.15f, 0.235f, depth)).x;
            float spanX = maxX - minX;

            GameObject lastBox = null;
            for (int i = completedBoxObjects.Count - 1; i >= 0; i--)
            {
                if (completedBoxObjects[i] != null && !tweeningDockObjects.Contains(completedBoxObjects[i]))
                {
                    lastBox = completedBoxObjects[i];
                    break;
                }
            }

            Vector3 targetPos;
            if (lastBox != null)
            {
                targetPos = lastBox.transform.position - camRight * idealSpacing;
                if (targetPos.x < minX) targetPos.x += spanX;
            }
            else
            {
                targetPos = mainCamera.ViewportToWorldPoint(new Vector3(0.25f, 0.235f, depth));
            }

            return targetPos;
        }

        private void AnimateSlowBoxClosingAndShipping(int slotIndex)
        {
            isProcessingMatch = true;
            bool isMechaSlot = (slotIndex == 2);

            // Reserve the shipping index immediately so sequential boxes never get duplicate or skipped indices
            int shipIndex = isMechaSlot ? -1 : completedBoxesCount++;

            // Hide the dock slot's in-progress fill badge right away - otherwise it keeps floating over
            // the (now visually empty) dock slot for the whole shipping animation, reading as a second,
            // detached copy of "what's in the box" next to the real one on the lid.
            if (slotIndex < slotItemIconImages.Count && slotItemIconImages[slotIndex] != null)
            {
                slotItemIconImages[slotIndex].gameObject.SetActive(false);
            }

            List<DockItemData> filledItems = new List<DockItemData>(slotBoxContents[slotIndex]);
            GameObject box = slotBox[slotIndex];

            DockItemData firstItemData = filledItems.Count > 0 ? filledItems[0] : null;

            float baseScale = box != null ? ComputeFitScaleForSlot(slotIndex, box) * 1.30f : 0.18f;

            Sequence boxSeq = DOTween.Sequence();

            // Phase 1: Items inside shrink smoothly into the box bottom (0.45s)
            foreach (var itemData in filledItems)
            {
                if (itemData.targetObject != null)
                {
                    tweeningDockObjects.Add(itemData.targetObject.gameObject);
                    boxSeq.Join(itemData.targetObject.transform.DOScale(Vector3.zero, 0.45f).SetEase(Ease.InCubic));
                }
            }

            // Phase 2: The box's own four lid flaps fold shut in place - the box never moves or scales,
            // so it can't appear to sink into the ground; only the cardboard flaps rotate closed (0.65s).
            if (box != null)
            {
                tweeningDockObjects.Add(box);
                AnimateBoxFlaps(boxSeq, box, true, 0.65f);
            }

            // Phase 3: Rest a small replica of the packed item on the sealed lid
            boxSeq.AppendCallback(() =>
            {
                if (box != null) AttachItemVisualToBox(box, firstItemData);
            });

            // Phase 4: Brief 0.30s pause so the player clearly sees the sealed box resting on the slot
            boxSeq.AppendInterval(0.30f);

            if (!isMechaSlot)
            {
                // Regular food boxes: Jump to the walking 3D conveyor belt!
                Vector3 shipTargetWorld = GetRedMarkedCompletedBoxWorldPos(shipIndex);
                if (box != null)
                {
                    boxSeq.Append(box.transform.DOJump(shipTargetWorld, GetDockJumpPower(0.9f), 1, 1.0f).SetEase(Ease.OutCubic));
                    boxSeq.Join(box.transform.DOScale(baseScale * 1.15f, 1.0f));
                    boxSeq.Join(box.transform.DORotateQuaternion(BoxDisplayRotation(BoxShelfTiltEuler), 1.0f));
                }

                boxSeq.OnComplete(() =>
                {
                    if (box != null)
                    {
                        tweeningDockObjects.Remove(box);
                        PunchBox(box, new Vector3(0.14f, -0.10f, 0.14f), 0.32f, 10, 1f);
                        completedBoxObjects.Add(box);
                    }

                    // Spawn a fresh open box for this slot so a new item can start filling it
                    GameObject newBox = CreatePackagingBox(slotIndex);
                    if (newBox != null)
                    {
                        newBox.transform.rotation = BoxDisplayRotation(BoxSlotTiltEuler);
                        newBox.transform.position = GetSlotWorldPosition(slotIndex);
                        newBox.transform.localScale = Vector3.zero;
                        slotBox[slotIndex] = newBox;

                        float openBaseScale = ComputeFitScaleForSlot(slotIndex, newBox) * 1.25f;
                        tweeningDockObjects.Add(newBox);
                        newBox.transform.DOScale(openBaseScale, 0.55f).SetEase(Ease.OutBack).OnComplete(() =>
                        {
                            tweeningDockObjects.Remove(newBox);
                        });
                    }

                    foreach (var itemData in filledItems)
                    {
                        if (itemData.targetObject != null)
                        {
                            tweeningDockObjects.Remove(itemData.targetObject.gameObject);
                            Destroy(itemData.targetObject.gameObject);
                        }
                    }

                    if (MatchGoalManager.Instance != null && filledItems.Count > 0)
                    {
                        MatchGoalManager.Instance.RegisterMatchedItem(filledItems[0].shapeType, filledItems[0].colorName);
                        RefreshTargetGoalsUI();
                    }

                    slotBoxContents[slotIndex].Clear();
                    slotAssignedItemName[slotIndex] = null;
                    slotRequiredCount[slotIndex] = 0;

                    UpdateSlotBadgesUI();
                    isProcessingMatch = false;
                });
            }
            else
            {
                // Mecha Box: When ALL Mechas enter, close flaps shut and STAY CLOSED permanently on dock!
                boxSeq.OnComplete(() =>
                {
                    if (box != null)
                    {
                        tweeningDockObjects.Remove(box);
                        PunchBox(box, new Vector3(0.20f, 0.20f, 0.20f), 0.45f, 12, 1f);
                    }

                    // DO NOT spawn a new open box! The Mecha Box remains closed and sealed resting on the dock.

                    foreach (var itemData in filledItems)
                    {
                        if (itemData.targetObject != null)
                        {
                            tweeningDockObjects.Remove(itemData.targetObject.gameObject);
                            Destroy(itemData.targetObject.gameObject);
                        }
                    }

                    if (MatchGoalManager.Instance != null && filledItems.Count > 0)
                    {
                        MatchGoalManager.Instance.RegisterMatchedItem(filledItems[0].shapeType, filledItems[0].colorName);
                        RefreshTargetGoalsUI();
                    }

                    slotBoxContents[slotIndex].Clear();
                    slotAssignedItemName[slotIndex] = "Mecha_Completed";

                    UpdateSlotBadgesUI();
                    isProcessingMatch = false;
                });
            }
        }

        private void UpdateSlotBadgesUI()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (i < slotRects.Count)
                {
                    int count = slotBoxContents[i].Count;
                    string assignedName = slotAssignedItemName[i];
                    int reqCount = slotRequiredCount[i] > 0 ? slotRequiredCount[i] : (i == 2 ? 1 : 3);

                    if (i == 2 && assignedName == "Mecha_Completed")
                    {
                        slotBadgeTexts[i].text = "✅ MECHA";
                        if (i < slotItemIconImages.Count && slotItemIconImages[i] != null)
                        {
                            slotItemIconImages[i].gameObject.SetActive(false);
                        }
                    }
                    else if (!string.IsNullOrEmpty(assignedName))
                    {
                        slotBadgeTexts[i].text = $"{count}/{reqCount}";

                        if (i < slotItemIconImages.Count && slotItemIconImages[i] != null)
                        {
                            Image iconImg = slotItemIconImages[i];
                            DockItemData firstItem = count > 0 ? slotBoxContents[i][0] : null;
                            Sprite itemSprite = ExtractIconFromItem(firstItem);

                            if (itemSprite != null)
                            {
                                iconImg.sprite = itemSprite;
                                iconImg.color = Color.white;
                            }
                            else
                            {
                                iconImg.sprite = IconSprite("Box");
                                if (count > 0 && slotBoxContents[i][0].targetObject != null)
                                    iconImg.color = slotBoxContents[i][0].objectColor;
                                else
                                    iconImg.color = i == 2 ? new Color(0.2f, 0.95f, 1.0f) : Color.yellow;
                            }

                            if (!iconImg.gameObject.activeSelf)
                            {
                                iconImg.gameObject.SetActive(true);
                                iconImg.transform.DOKill();
                                iconImg.transform.localScale = Vector3.zero;
                                iconImg.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
                            }
                        }
                    }
                    else
                    {
                        if (i == 2)
                            slotBadgeTexts[i].text = "🤖 MECHA";
                        else
                            slotBadgeTexts[i].text = $"KUTU {i + 1}";

                        if (i < slotItemIconImages.Count && slotItemIconImages[i] != null)
                        {
                            slotItemIconImages[i].gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        public void HideAllOverlayPanels()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                for (int k = slotBoxContents[i].Count - 1; k >= 0; k--)
                {
                    if (slotBoxContents[i][k].targetObject != null)
                    {
                        Destroy(slotBoxContents[i][k].targetObject.gameObject);
                    }
                }
                slotBoxContents[i].Clear();
                slotAssignedItemName[i] = null;
                slotRequiredCount[i] = 0;
            }

            tweeningDockObjects.Clear();
            isProcessingMatch = false;

            UpdateSlotBadgesUI();
        }
    }
}
