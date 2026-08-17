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
    [ExecuteAlways]
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

        [Header("Header Goal Panel")]
        [SerializeField] private Vector2 headerSize = new Vector2(1000f, 110f);
        [SerializeField] private Vector2 headerAnchoredPosition = new Vector2(0f, -100f);
        [SerializeField] private int titleFontSize = 28;
        [Range(0f, 1f)]
        [SerializeField] private float titleAreaWidthRatio = 0.25f;
        [SerializeField] private int goalContainerSpacing = 5;

        [Header("Goal Cards")]
        [SerializeField] private Vector2 goalCardSize = new Vector2(110f, 80f);
        [SerializeField] private float goalCardIconSize = 65f;
        [SerializeField] private int goalCardFontSize = 28;
        [SerializeField] private float goalCard3DModelScale = 450f;
        [SerializeField] private float goalCard3DModelTargetSize = 85f;
        [SerializeField] private Vector3 goalCard3DModelLocalPosition = new Vector3(32.5f, 0f, -25f);
        [SerializeField] private float goalCard3DModelTiltX = 15f;

        [Header("Goal Card Animations")]
        [SerializeField] private float goalSpawnStaggerDelay = 0.07f;
        [SerializeField] private float goalSpawnScaleDuration = 0.35f;
        [SerializeField] private float goalSpawnFadeDuration = 0.22f;
        [SerializeField] private float goalTickCardPunchStrength = 0.28f;
        [SerializeField] private float goalTickCardPunchDuration = 0.38f;
        [SerializeField] private float goalTickTextPunchStrength = 0.45f;
        [SerializeField] private float goalTickTextPunchDuration = 0.35f;
        [SerializeField] private float goalRemoveBounceScale = 1.18f;
        [SerializeField] private float goalRemoveBounceDuration = 0.14f;
        [SerializeField] private float goalRemoveShrinkDuration = 0.28f;

        [Header("Bottom Dock Panel")]
        [SerializeField] private Vector2 dockPanelSize = new Vector2(980f, 210f);
        [SerializeField] private Vector2 dockPanelAnchoredPosition = new Vector2(0f, 65f);
        [SerializeField] private float dockSlotSpacing = 35f;
        [SerializeField] private int dockSlotLabelFontSize = 24;

        [Header("Shuffle Button")]
        [SerializeField] private Vector2 shuffleButtonPosition = new Vector2(60f, 240f);
        [SerializeField] private Vector2 shuffleButtonSize = new Vector2(80f, 80f);
        [SerializeField] private float shuffleIconSize = 45f;

        [Header("3D Cardboard Box Packaging (DOTween Animation)")]
        [SerializeField] private GameObject cardboardBoxOpenedPrefab;
        [Tooltip("Optional material for the packaging box. Overrides EVERY slot including the tape, so leave empty to keep the model's separate cardboard/tape materials.")]
        [SerializeField] private Material boxMaterialOverride;
        [Tooltip("Tape colours handed out to boxes in order, so consecutive boxes never share one. Only the BoxTape slot is tinted; the cardboard is untouched.")]
        [SerializeField] private Color[] boxTapeColors =
        {
            new Color(0.87f, 0.35f, 0.41f), // kırmızı  (modelin kendi rengi)
            new Color(0.95f, 0.76f, 0.30f), // hardal
            new Color(0.36f, 0.70f, 0.55f), // yeşil
            new Color(0.36f, 0.58f, 0.86f), // mavi
            new Color(0.72f, 0.50f, 0.85f)  // mor
        };

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
        [Tooltip("How many belt pallets to lay across the shelf. 2 creates two side-by-side rectangular sections.")]
        [Min(0)]
        [SerializeField] private int conveyorTileCount = 2;
        [Tooltip("Y-axis height multiplier for the conveyor belt.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float conveyorYScaleMultiplier = 1.0f;
        [Tooltip("Linear move speed of completed boxes along the conveyor belt (world units per second). Matches belt travel direction.")]
        [SerializeField] private float conveyorBoxMoveSpeed = 0.40f;
        [Tooltip("How quickly a shipped box eases into its place in the run. Higher is snappier; too low and the boxes visibly trail the belt.")]
        [SerializeField] private float conveyorBoxSettleSpeed = 12f;

        [Header("3D Conveyor Belt Scene Placement")]
        [Tooltip("If true, automatically spawns a new belt prefab if none exists in the scene. Turn OFF to strictly use your scene-placed belt.")]
        [SerializeField] private bool autoSpawnConveyorBelt = false;
        [Tooltip("If true, places the 3D conveyor belt directly in 3D world space (on the scene floor) instead of under UI canvas elements.")]
        [SerializeField] private bool use3DScenePosition = true;
        [Tooltip("3D World Position of the conveyor belt in the scene.")]
        [SerializeField] private Vector3 conveyorWorldPosition = new Vector3(0f, 0.05f, -3.2f);
        [Tooltip("Total width of the 3D conveyor belt run in 3D world units.")]
        [SerializeField] private float conveyorWorldWidth = 6.0f;
        [Tooltip("Height of the conveyor belt run in 3D world units.")]
        [SerializeField] private float conveyorWorldHeight = 0.5f;
        [Tooltip("Rotation of the conveyor belt in 3D scene space.")]
        [SerializeField] private Vector3 conveyorWorldRotationEuler = new Vector3(15f, 0f, 0f);

        [Header("Mecha On Sealed Lid")]
        [Tooltip("How much of the lid seam the mecha stretches across, laid out full length. 1 spans the whole box.")]
        [Range(0.4f, 1f)]
        [SerializeField] private float mechaLidFillRatio = 0.9f;
        [Tooltip("How far the mecha floats above the lid, as a fraction of the seam's length. Just enough to stop it z-fighting the cardboard.")]
        [SerializeField] private float mechaLidClearance = 0.02f;
#pragma warning disable CS0414
        [Tooltip("If true, spawns initial completed boxes on the conveyor belt at start so it moves continuously right away.")]
        [SerializeField] private bool spawnInitialConveyorBoxes = false;
        [Tooltip("Colour of the UI badges, goal cards and the shuffle button - the elements that used to be the kit's violet. Set to the colour you actually want to see; the sprite tint is worked out from it.")]
        [SerializeField] private Color uiAccentColor = new Color(0f, 26f / 255f, 112f / 255f, 1f); // #001A70

        [Tooltip("Show only every Nth arrow on a pallet. 1 is the authored density of ConveyorTile.fbx (10 groups per tile); 4 was for the old, far denser Conveyor.fbx.")]
        [Min(1)]
        [SerializeField] private int conveyorArrowStride = 1;
        [Tooltip("Size multiplier on the arrows. 1.0 keeps the arrow proportional.")]
        [Min(0.1f)]
        [SerializeField] private float conveyorArrowScale = 1.0f;
        [Tooltip("Number of completed boxes to fit side-by-side in a single horizontal row before starting a new row.")]
        [SerializeField] private int completedBoxesPerRow = 4;
#pragma warning restore CS0414

        // Two slots. The third was the mecha's dedicated box, but the mecha is no longer collected
        // at all - it runs and vanishes (MechaRunnerBehavior), so that slot could never fill.
        private const int MAX_SLOTS = 2;
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

        // Matches "BoxTape" and the "BoxTape_Variant" copies made from it.
        private const string TapeMaterialPrefix = "BoxTape";
        private int nextTapeVariant;

        // Matches the dark navy the main camera used to clear with directly, before it switched to
        // ClearFlags.Depth and started relying on Background_Camera to clear color instead.
        private static readonly Color FallbackBackgroundColor = new Color(0.06f, 0.08f, 0.12f);

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
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private Camera mainCamera;
        // Per-slot rather than one global flag: a single lock froze ALL collection for the ~2.5s a full box
        // spent closing and shipping, so the player could not drop anything into the other, idle slots
        // either. Not readonly - Unity drops readonly fields when it serializes state across a domain
        // reload, which would leave this out of step with the rest of the slot arrays.
        private bool[] slotProcessing = new bool[MAX_SLOTS];

        private bool IsSlotBusy(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < MAX_SLOTS && slotProcessing[slotIndex];
        }

        private List<DockItemData> GetSlotBoxContent(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return null;
            if (slotBoxContents[slotIndex] == null)
            {
                slotBoxContents[slotIndex] = new List<DockItemData>();
            }
            return slotBoxContents[slotIndex];
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

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

        // The badges, goal cards and the shuffle button used to be the kit's violet ones, which clashed
        // with the navy conveyor frame. The kit ships no navy button, so the BLUE sprite is tinted instead.
        private const string UIAccentButton = "Buttons/Button Blue";
        // Note the Misc/ subfolder - the blue square button lives there, unlike the violet one it replaces,
        // and loading it from "Buttons/" silently returns null and leaves the shuffle button spriteless.
        private const string UIAccentSquareButton = "Buttons/Misc/Small Square Button Blue";

        /// <summary>Measured mid-tone of both blue button sprites, which is what the tint multiplies.</summary>
        private static readonly Color UIAccentSpriteMidtone = new Color(0.18f, 0.612f, 1f);

        /// <summary>
        /// Image tint MULTIPLIES the sprite, so asking for #001A70 and assigning it straight would land
        /// somewhere much darker. The wanted colour is divided by the sprite's own mid-tone instead, which
        /// makes the button read as exactly this colour while keeping the sprite's bevel and shading.
        /// </summary>
        private Color UIAccentTint => new Color(
            Mathf.Clamp01(uiAccentColor.r / UIAccentSpriteMidtone.r),
            Mathf.Clamp01(uiAccentColor.g / UIAccentSpriteMidtone.g),
            Mathf.Clamp01(uiAccentColor.b / UIAccentSpriteMidtone.b),
            uiAccentColor.a);
        private static Sprite IconSprite(string iconName) => LoadUISprite($"Icons/{iconName}");

        private static void ApplySlicedSprite(Image img, Sprite sprite)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        public static Sprite LoadGameBackgroundSprite()
        {
            Sprite s = Resources.Load<Sprite>("GameBackground");
            if (s != null) return s;

            // Fallback for when the asset is still imported as a plain Texture2D rather than a
            // Sprite (2D and UI) - keeps the background working even if the import type regresses.
            Texture2D tex = Resources.Load<Texture2D>("GameBackground");
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            return null;
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

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            if (!Application.isPlaying && use3DScenePosition)
            {
                EnsureConveyorBelt();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (goalCard3DModelTargetSize > 100f || goalCard3DModelTargetSize <= 0f)
            {
                goalCard3DModelTargetSize = 85f;
            }
            if (!Application.isPlaying && use3DScenePosition)
            {
                EnsureConveyorBelt();
            }
        }
#endif

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
            manager.EnsureConveyorBelt();
            Selection.activeGameObject = sceneController;
            Debug.Log("🎨 Professional 2-Box Cardboard Packaging System Built Successfully!");
        }
#endif

        public void EnsureCanvasStructure()
        {
            EnsureEventSystem();

            Transform canvasTr = transform.Find("MatchFactory_Canvas");
            GameObject canvasObj;
            if (canvasTr != null)
            {
                canvasObj = canvasTr.gameObject;
            }
            else
            {
                canvasObj = new GameObject("MatchFactory_Canvas");
                canvasObj.transform.SetParent(transform);
            }

            mainCanvas = canvasObj.GetComponent<Canvas>() ?? canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            mainCanvas.worldCamera = mainCamera;
            mainCanvas.planeDistance = uiPlaneDistance;
            mainCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>() ?? canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            EnsureBackgroundCanvas();
            BuildHeaderGoalPanel(canvasObj.transform);
            BuildBottomDockPanel(canvasObj.transform);
            BuildShuffleButton(canvasObj.transform);
            Ensure3DCardboardBoxes();
        }

        public void EnsureBackgroundCanvas()
        {
            Transform existing = transform.Find("MatchFactory_Background_Canvas");
            if (existing != null) return;

            Sprite bgSprite = LoadGameBackgroundSprite();

            // The main gameplay camera is set to ClearFlags.Depth (see ScenePhysicsSetup.SetupCamera),
            // meaning it never clears the color buffer itself - it relies entirely on this background
            // camera rendering first. So this camera must ALWAYS exist and ALWAYS clear with a solid
            // color, even when no background image is available yet, or the screen shows whatever
            // undefined content was left in the buffer instead of a clean fallback color.
            Camera bgCam = null;
            Transform bgCamTransform = transform.Find("Background_Camera");
            if (bgCamTransform != null)
            {
                bgCam = bgCamTransform.GetComponent<Camera>();
            }
            if (bgCam == null)
            {
                GameObject bgCamObj = new GameObject("Background_Camera");
                bgCamObj.transform.SetParent(transform, false);
                bgCam = bgCamObj.AddComponent<Camera>();
            }

            bgCam.depth = -10;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = FallbackBackgroundColor;
            bgCam.orthographic = true;
            bgCam.orthographicSize = 5f;
            bgCam.nearClipPlane = 0.1f;
            bgCam.farClipPlane = 100f;

            if (bgSprite == null) return;

            GameObject bgCanvasObj = new GameObject("MatchFactory_Background_Canvas");
            bgCanvasObj.transform.SetParent(transform);

            Canvas bgCanvas = bgCanvasObj.AddComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            bgCanvas.worldCamera = bgCam;
            bgCanvas.planeDistance = 10f;
            bgCanvas.sortingOrder = -100;

            CanvasScaler scaler = bgCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject bgImgObj = new GameObject("Full_Screen_Background_Image");
            bgImgObj.transform.SetParent(bgCanvasObj.transform, false);

            RectTransform rect = bgImgObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image img = bgImgObj.AddComponent<Image>();
            img.sprite = bgSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
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
            Transform existingHeader = parent.Find("Header_Goal_Panel");
            GameObject headerObj;
            if (existingHeader != null)
            {
                headerObj = existingHeader.gameObject;
            }
            else
            {
                headerObj = new GameObject("Header_Goal_Panel");
                headerObj.transform.SetParent(parent, false);

                RectTransform headerRect = headerObj.AddComponent<RectTransform>();
                headerRect.anchorMin = new Vector2(0.5f, 1f);
                headerRect.anchorMax = new Vector2(0.5f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.anchoredPosition = headerAnchoredPosition;
                headerRect.sizeDelta = headerSize;
            }

            // 1. Level Badge & Text
            if (headerObj.transform.Find("Level_Badge") == null)
            {
                GameObject titleBadgeObj = new GameObject("Level_Badge");
                titleBadgeObj.transform.SetParent(headerObj.transform, false);
                RectTransform titleBadgeRect = titleBadgeObj.AddComponent<RectTransform>();
                titleBadgeRect.anchorMin = new Vector2(0.04f, 0.1f);
                titleBadgeRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                titleBadgeRect.sizeDelta = Vector2.zero;
                Image titleBadge = titleBadgeObj.AddComponent<Image>();
                ApplySlicedSprite(titleBadge, LoadUISprite(UIAccentButton));
                titleBadge.color = UIAccentTint;
            }

            if (headerObj.transform.Find("Level_Text") == null)
            {
                GameObject titleTextObj = new GameObject("Level_Text");
                titleTextObj.transform.SetParent(headerObj.transform, false);

                RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
                titleTextRect.anchorMin = new Vector2(0.04f, 0.1f);
                titleTextRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                titleTextRect.sizeDelta = Vector2.zero;

                Text titleTxt = titleTextObj.AddComponent<Text>();
                titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleTxt.fontSize = titleFontSize;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.color = Color.white;
                
                Shadow shadow = titleTextObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(2, -2);
                titleTxt.alignment = TextAnchor.MiddleCenter;
            }

            Transform lt = headerObj.transform.Find("Level_Text");
            if (lt != null)
            {
                Text titleTxt = lt.GetComponent<Text>();
                if (titleTxt != null)
                {
                    string titleStr = "SEVİYE 1";
                    if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
                    {
                        titleStr = LevelManager.Instance.ActiveLevelData.levelTitle.ToUpperInvariant();
                    }
                    titleTxt.text = titleStr;
                }
            }

            // 2. Timer Badge & Text
            if (headerObj.transform.Find("timer_badge") == null)
            {
                GameObject timerBadgeObj = new GameObject("timer_badge");
                timerBadgeObj.transform.SetParent(headerObj.transform, false);
                RectTransform timerBadgeRect = timerBadgeObj.AddComponent<RectTransform>();
                timerBadgeRect.anchorMin = new Vector2(0.04f, 0.1f);
                timerBadgeRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                timerBadgeRect.sizeDelta = Vector2.zero;
                timerBadgeRect.anchoredPosition = new Vector2(0f, -100f);
                Image timerBadge = timerBadgeObj.AddComponent<Image>();
                ApplySlicedSprite(timerBadge, LoadUISprite(UIAccentButton));
                timerBadge.color = UIAccentTint;
            }

            if (headerObj.transform.Find("timer_text") == null)
            {
                GameObject timerTextObj = new GameObject("timer_text");
                timerTextObj.transform.SetParent(headerObj.transform, false);
                RectTransform timerTextRect = timerTextObj.AddComponent<RectTransform>();
                timerTextRect.anchorMin = new Vector2(0.04f, 0.1f);
                timerTextRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                timerTextRect.sizeDelta = Vector2.zero;
                timerTextRect.anchoredPosition = new Vector2(0f, -100f);
                Text timerTxt = timerTextObj.AddComponent<Text>();
                timerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                timerTxt.fontSize = titleFontSize;
                timerTxt.fontStyle = FontStyle.Bold;
                timerTxt.color = Color.white;
                
                Shadow timerShadow = timerTextObj.AddComponent<Shadow>();
                timerShadow.effectColor = new Color(0, 0, 0, 0.8f);
                timerShadow.effectDistance = new Vector2(2, -2);
                
                timerTxt.text = "00:00";
                timerTxt.alignment = TextAnchor.MiddleCenter;
            }

            // 3. Mecha Goal Badge & Text
            if (headerObj.transform.Find("Mecha_Goal_Badge") == null)
            {
                GameObject mechaBadgeObj = new GameObject("Mecha_Goal_Badge");
                mechaBadgeObj.transform.SetParent(headerObj.transform, false);
                RectTransform mechaBadgeRect = mechaBadgeObj.AddComponent<RectTransform>();
                mechaBadgeRect.anchorMin = new Vector2(0.04f, 0.1f);
                mechaBadgeRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                mechaBadgeRect.sizeDelta = Vector2.zero;
                mechaBadgeRect.anchoredPosition = new Vector2(0f, -200f);
                Image mechaBadge = mechaBadgeObj.AddComponent<Image>();
                ApplySlicedSprite(mechaBadge, LoadUISprite(UIAccentButton));
                mechaBadge.color = new Color(0.12f, 0.28f, 0.55f, 1f);
            }

            if (headerObj.transform.Find("Mecha_Goal_Text") == null)
            {
                GameObject mechaTextObj = new GameObject("Mecha_Goal_Text");
                mechaTextObj.transform.SetParent(headerObj.transform, false);
                RectTransform mechaTextRect = mechaTextObj.AddComponent<RectTransform>();
                mechaTextRect.anchorMin = new Vector2(0.04f, 0.1f);
                mechaTextRect.anchorMax = new Vector2(titleAreaWidthRatio, 0.9f);
                mechaTextRect.sizeDelta = Vector2.zero;
                mechaTextRect.anchoredPosition = new Vector2(0f, -200f);
                Text mechaTxt = mechaTextObj.AddComponent<Text>();
                mechaTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                mechaTxt.fontSize = titleFontSize - 4;
                mechaTxt.fontStyle = FontStyle.Bold;
                mechaTxt.color = new Color(0.4f, 0.95f, 1f);
                mechaTxt.alignment = TextAnchor.MiddleCenter;

                Shadow mechaShadow = mechaTextObj.AddComponent<Shadow>();
                mechaShadow.effectColor = new Color(0, 0, 0, 0.8f);
                mechaShadow.effectDistance = new Vector2(2, -2);

                mechaTxt.text = "MECHA x1";
            }

            // NOTE: Custom user objects like kutu_badge are untouched!

            // 4. Goals Container
            Transform existingGoalsContainer = headerObj.transform.Find("Goals_Container");
            if (existingGoalsContainer == null)
            {
                GameObject goalsContainer = new GameObject("Goals_Container");
                goalsContainer.transform.SetParent(headerObj.transform, false);

                topGoalContainer = goalsContainer.AddComponent<RectTransform>();
                topGoalContainer.anchorMin = new Vector2(titleAreaWidthRatio, 0f);
                topGoalContainer.anchorMax = new Vector2(0.98f, 1f);
                topGoalContainer.sizeDelta = Vector2.zero;

                HorizontalLayoutGroup layout = goalsContainer.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 10, 10);
                layout.spacing = goalContainerSpacing;
                layout.childAlignment = TextAnchor.MiddleRight;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
            }
            else
            {
                topGoalContainer = existingGoalsContainer.GetComponent<RectTransform>();
            }
        }

        private void BuildBottomDockPanel(Transform parent)
        {
            Transform existingDock = parent.Find("Bottom_Dock_Panel");
            if (existingDock != null)
            {
                slotRects.Clear();
                slotBadgeTexts.Clear();

                Transform existingSlots = existingDock.Find("Slots_Container");
                if (existingSlots != null)
                {
                    bottomDockContainer = existingSlots.GetComponent<RectTransform>();
                    for (int i = 0; i < MAX_SLOTS; i++)
                    {
                        Transform slotChild = existingSlots.Find($"DockSlot_{i}");
                        if (slotChild != null)
                        {
                            slotRects.Add(slotChild.GetComponent<RectTransform>());
                            Transform labelChild = slotChild.Find("LabelText");
                            if (labelChild != null)
                            {
                                slotBadgeTexts.Add(labelChild.GetComponent<Text>());
                            }
                        }
                    }
                }
                return;
            }

            GameObject dockObj = new GameObject("Bottom_Dock_Panel");
            dockObj.transform.SetParent(parent, false);

            RectTransform dockRect = dockObj.AddComponent<RectTransform>();
            dockRect.anchorMin = new Vector2(0.5f, 0f);
            dockRect.anchorMax = new Vector2(0.5f, 0f);
            dockRect.pivot = new Vector2(0.5f, 0f);
            dockRect.anchoredPosition = dockPanelAnchoredPosition;
            dockRect.sizeDelta = dockPanelSize;

            Image bg = dockObj.AddComponent<Image>();
            bg.color = Color.clear; // Removed the giant purple background!

            GameObject slotsContainerObj = new GameObject("Slots_Container");
            slotsContainerObj.transform.SetParent(dockObj.transform, false);

            bottomDockContainer = slotsContainerObj.AddComponent<RectTransform>();
            bottomDockContainer.anchorMin = Vector2.zero;
            bottomDockContainer.anchorMax = Vector2.one;
            bottomDockContainer.sizeDelta = new Vector2(-20, 0);

            HorizontalLayoutGroup layout = slotsContainerObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 16, 16);
            layout.spacing = dockSlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            slotRects.Clear();
            slotBadgeTexts.Clear();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                GameObject slotObj = new GameObject($"DockSlot_{i}");
                slotObj.transform.SetParent(slotsContainerObj.transform, false);

                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                Image slotBg = slotObj.AddComponent<Image>();
                slotBg.color = Color.clear;

                GameObject labelObj = new GameObject("LabelText");
                labelObj.transform.SetParent(slotObj.transform, false);

                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.22f);
                labelRect.sizeDelta = Vector2.zero;
                // Move it slightly lower so it doesn't clip the 3D box
                labelRect.anchoredPosition = new Vector2(0, -30);

                Text labelTxt = labelObj.AddComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.fontSize = dockSlotLabelFontSize;
                labelTxt.fontStyle = FontStyle.Bold;
                labelTxt.alignment = TextAnchor.MiddleCenter;
                labelTxt.color = Color.white;
                
                Shadow shadow = labelObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(2, -2);
                
                Outline outline = labelObj.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.8f);
                outline.effectDistance = new Vector2(1, -1);

                labelTxt.text = "";

                slotRects.Add(slotRect);
                slotBadgeTexts.Add(labelTxt);
            }
        }

        private void BuildShuffleButton(Transform parent)
        {
            Transform existingBtn = parent.Find("Shuffle_Button");
            GameObject btnObj;
            if (existingBtn != null)
            {
                btnObj = existingBtn.gameObject;
            }
            else
            {
                btnObj = new GameObject("Shuffle_Button");
                btnObj.transform.SetParent(parent, false);

                RectTransform btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0f, 0f);
                btnRect.anchorMax = new Vector2(0f, 0f);
                btnRect.pivot = new Vector2(0f, 0f);
                btnRect.anchoredPosition = shuffleButtonPosition;
                btnRect.sizeDelta = shuffleButtonSize;

                Image btnBg = btnObj.AddComponent<Image>();
                ApplySlicedSprite(btnBg, LoadUISprite(UIAccentSquareButton));
                btnBg.color = UIAccentTint;

                GameObject btnIconObj = new GameObject("Icon");
                btnIconObj.transform.SetParent(btnObj.transform, false);

                RectTransform btnIconRect = btnIconObj.AddComponent<RectTransform>();
                btnIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnIconRect.pivot = new Vector2(0.5f, 0.5f);
                btnIconRect.anchoredPosition = Vector2.zero;
                btnIconRect.sizeDelta = new Vector2(shuffleIconSize, shuffleIconSize);

                Image btnIconImg = btnIconObj.AddComponent<Image>();
                btnIconImg.sprite = IconSprite("Cycle");
                btnIconImg.type = Image.Type.Simple;
                btnIconImg.preserveAspect = true;
                btnIconImg.color = Color.white;
            }

            Image bg = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
            bg.raycastTarget = true;

            Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnShuffleButtonClicked);
        }

        public void OnShuffleButtonClicked()
        {
            PhysicsObjectSpawner spawner = Object.FindFirstObjectByType<PhysicsObjectSpawner>();
            if (spawner != null)
            {
                spawner.GatherAndReshuffleRemaining();
            }
            else
            {
                FindTargetObject[] allItems = Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
                foreach (FindTargetObject item in allItems)
                {
                    if (item == null || item.isDocked) continue;
                    Rigidbody rb = item.GetComponent<Rigidbody>();
                    if (rb == null) continue;

                    Vector3 randPos = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(0.2f, 0.6f), Random.Range(-1.8f, 1.8f));
                    Quaternion randRot = Random.rotation;

                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;

                    item.transform.DOKill();
                    Sequence seq = DOTween.Sequence();
                    seq.Join(item.transform.DOMove(randPos, 0.45f).SetEase(Ease.OutQuad));
                    seq.Join(item.transform.DORotateQuaternion(randRot, 0.45f).SetEase(Ease.OutQuad));
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
        }

        public void RefreshTargetGoalsUI()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                GameObject levelTextObj = GameObject.Find("Level_Text");
                if (levelTextObj != null)
                {
                    Text titleTxt = levelTextObj.GetComponent<Text>();
                    if (titleTxt != null)
                    {
                        titleTxt.text = LevelManager.Instance.ActiveLevelData.levelTitle.ToUpperInvariant();
                    }
                }
            }

            if (MatchGoalManager.Instance == null || topGoalContainer == null) return;

            foreach (Transform child in topGoalContainer)
            {
                SafeDestroy(child.gameObject);
            }

            List<MatchGoal> goals = MatchGoalManager.Instance.levelGoals;

            // Separate Mecha goals from normal item goals
            MatchGoal mechaGoal = null;
            if (goals != null)
            {
                foreach (MatchGoal g in goals)
                {
                    if (g.colorName.Equals("Mecha", System.StringComparison.OrdinalIgnoreCase) ||
                        g.colorName.Contains("Mecha") || g.colorName.Contains("meccha"))
                    {
                        mechaGoal = g;
                        break;
                    }
                }
            }

            // Update dedicated Mecha Badge under the timer on the left UI panel
            GameObject mBadgeObj = GameObject.Find("Mecha_Goal_Badge");
            GameObject mTextObj = GameObject.Find("Mecha_Goal_Text");
            if (mBadgeObj == null && topGoalContainer != null && topGoalContainer.parent != null)
            {
                Transform mb = topGoalContainer.parent.Find("Mecha_Goal_Badge");
                if (mb != null) mBadgeObj = mb.gameObject;
                Transform mt = topGoalContainer.parent.Find("Mecha_Goal_Text");
                if (mt != null) mTextObj = mt.gameObject;
            }

            if (mechaGoal != null)
            {
                if (mBadgeObj != null) mBadgeObj.SetActive(true);
                if (mTextObj != null)
                {
                    mTextObj.SetActive(true);
                    Text txt = mTextObj.GetComponent<Text>();
                    if (txt != null)
                    {
                        if (mechaGoal.IsCompleted)
                        {
                            txt.text = "MECHA ✓";
                            txt.color = new Color(0.45f, 1f, 0.55f);
                        }
                        else
                        {
                            txt.text = $"MECHA x{mechaGoal.Remaining}";
                            txt.color = new Color(0.4f, 0.95f, 1f);
                        }
                    }
                }
            }
            else
            {
                // In Editor Mode (Tool View / Scene Preview) keep visible so developer can see & inspect it!
                bool isEditorPreview = !Application.isPlaying;
                if (mBadgeObj != null) mBadgeObj.SetActive(isEditorPreview);
                if (mTextObj != null) mTextObj.SetActive(isEditorPreview);
            }

            if (goals == null || goals.Count == 0) return;

            for (int i = 0; i < goals.Count; i++)
            {
                MatchGoal goal = goals[i];
                if (goal.IsCompleted) continue; // SKIP COMPLETED GOALS!

                // SKIP MECHA GOAL from topGoalContainer — it has its own dedicated spot under the timer!
                if (goal.colorName.Equals("Mecha", System.StringComparison.OrdinalIgnoreCase) ||
                    goal.colorName.Contains("Mecha") || goal.colorName.Contains("meccha"))
                {
                    continue;
                }

                GameObject cardObj = new GameObject($"GoalCard_{goal.colorName}_{goal.shapeType}");
                cardObj.transform.SetParent(topGoalContainer, false);

                RectTransform cardRect = cardObj.AddComponent<RectTransform>();
                cardRect.sizeDelta = goalCardSize;

                // Badge sized to the card — renders behind children.
                Image cardBg = cardObj.AddComponent<Image>();
                ApplySlicedSprite(cardBg, LoadUISprite(UIAccentButton));
                cardBg.color = UIAccentTint;

                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(cardObj.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(10, 0);
                iconRect.sizeDelta = new Vector2(goalCardIconSize, goalCardIconSize);

                if (goal.displayPrefab != null)
                {
                    GameObject modelWrapper = new GameObject("3D_Icon_Wrapper");
                    modelWrapper.transform.SetParent(iconObj.transform, false);
                    modelWrapper.transform.localPosition = goalCard3DModelLocalPosition;
                    modelWrapper.transform.localRotation = Quaternion.Euler(goalCard3DModelTiltX, 0f, 0f);
                    modelWrapper.transform.localScale = Vector3.one;

                    GameObject modelObj = Instantiate(goal.displayPrefab, modelWrapper.transform);
                    modelObj.name = "3D_Icon_Model";
                    modelObj.transform.localPosition = Vector3.zero;
                    modelObj.transform.localRotation = Quaternion.identity;
                    modelObj.transform.localScale = Vector3.one;

                    // Strip physics components and scripts from model
                    foreach (var c in modelObj.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
                    foreach (var c in modelObj.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(c);
                    foreach (var c in modelObj.GetComponentsInChildren<MonoBehaviour>(true)) SafeDestroy(c);

                    // Fix layer for UI camera
                    int uiLayer = LayerMask.NameToLayer("UI");
                    Transform[] allChildren = modelWrapper.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allChildren)
                    {
                        t.gameObject.layer = uiLayer;
                    }

                    // Calculate combined bounding box in modelWrapper space to normalize scale & visual center
                    Renderer[] renderers = modelObj.GetComponentsInChildren<Renderer>(true);
                    if (renderers != null && renderers.Length > 0)
                    {
                        Bounds combinedBounds = new Bounds();
                        bool hasBounds = false;
                        foreach (Renderer r in renderers)
                        {
                            if (r == null || !r.enabled) continue;
                            if (!hasBounds)
                            {
                                combinedBounds = r.bounds;
                                hasBounds = true;
                            }
                            else
                            {
                                combinedBounds.Encapsulate(r.bounds);
                            }
                        }

                        if (hasBounds)
                        {
                            Vector3 localCenterOffset = modelWrapper.transform.InverseTransformPoint(combinedBounds.center);
                            Vector3 worldSize = combinedBounds.size;
                            float maxWorldDim = Mathf.Max(worldSize.x, worldSize.y, worldSize.z);

                            float worldUnitInUIPixels = modelWrapper.transform.lossyScale.x;
                            float rawMeshSizeInUIPixels = (worldUnitInUIPixels > 0.00001f) ? (maxWorldDim / worldUnitInUIPixels) : maxWorldDim;

                            float effectiveTargetSize = (goalCard3DModelTargetSize > 100f || goalCard3DModelTargetSize <= 0f) ? 85f : goalCard3DModelTargetSize;
                            float scaleFactor = (rawMeshSizeInUIPixels > 0.0001f) ? (effectiveTargetSize / rawMeshSizeInUIPixels) : 1f;

                            modelObj.transform.localScale = Vector3.one * scaleFactor;
                            modelObj.transform.localPosition = -localCenterOffset * scaleFactor;
                        }
                        else
                        {
                            modelObj.transform.localScale = Vector3.one * goalCard3DModelScale;
                        }
                    }
                    else
                    {
                        modelObj.transform.localScale = Vector3.one * goalCard3DModelScale;
                    }

                    // Add smooth rotator to wrapper
                    modelWrapper.AddComponent<MechaFind3D.UI.UIRotator>();
                }
                else
                {
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
                }

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(cardObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0.5f, 0f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.sizeDelta = Vector2.zero;

                Text txt = textObj.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = goalCardFontSize;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;

                Shadow shadow = textObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(2, -2);

                txt.text = $"x{goal.Remaining}";

                // Staggered pop-in: scale from 0 with OutBack, then fade in
                int capturedIndex = i;
                cardObj.transform.localScale = Vector3.zero;
                CanvasGroup cg = cardObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                Sequence spawnSeq = DOTween.Sequence();
                spawnSeq.AppendInterval(capturedIndex * goalSpawnStaggerDelay);
                spawnSeq.Append(cardObj.transform.DOScale(Vector3.one, goalSpawnScaleDuration).SetEase(Ease.OutBack, 1.6f));
                spawnSeq.Join(cg.DOFade(1f, goalSpawnFadeDuration));
                spawnSeq.Play();
            }
        }
        
        private void SetGoalUIVisualTick(string colorName)
        {
            if (colorName.Equals("Mecha", System.StringComparison.OrdinalIgnoreCase) ||
                colorName.Contains("Mecha") || colorName.Contains("meccha"))
            {
                GameObject mBadgeObj = GameObject.Find("Mecha_Goal_Badge");
                GameObject mTextObj = GameObject.Find("Mecha_Goal_Text");
                if (mBadgeObj == null && topGoalContainer != null && topGoalContainer.parent != null)
                {
                    Transform mb = topGoalContainer.parent.Find("Mecha_Goal_Badge");
                    if (mb != null) mBadgeObj = mb.gameObject;
                    Transform mt = topGoalContainer.parent.Find("Mecha_Goal_Text");
                    if (mt != null) mTextObj = mt.gameObject;
                }

                MatchGoal mechaGoal = null;
                if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null)
                {
                    foreach (var g in MatchGoalManager.Instance.levelGoals)
                    {
                        if (g.colorName.Equals("Mecha", System.StringComparison.OrdinalIgnoreCase) ||
                            g.colorName.Contains("Mecha") || g.colorName.Contains("meccha"))
                        {
                            mechaGoal = g;
                            break;
                        }
                    }
                }

                if (mTextObj != null)
                {
                    Text txt = mTextObj.GetComponent<Text>();
                    if (txt != null)
                    {
                        if (mechaGoal != null)
                        {
                            if (mechaGoal.IsCompleted)
                            {
                                txt.text = "MECHA ✓";
                                txt.color = new Color(0.45f, 1f, 0.55f);
                            }
                            else
                            {
                                txt.text = $"MECHA x{mechaGoal.Remaining}";
                            }
                        }
                        txt.DOColor(new Color(1f, 0.92f, 0.25f), 0.12f)
                            .OnComplete(() => txt.DOColor(mechaGoal != null && mechaGoal.IsCompleted ? new Color(0.45f, 1f, 0.55f) : new Color(0.4f, 0.95f, 1f), 0.3f));
                        mTextObj.transform.DOKill();
                        mTextObj.transform.DOPunchScale(Vector3.one * goalTickTextPunchStrength, goalTickTextPunchDuration, 7, 0.9f);
                    }
                }

                if (mBadgeObj != null)
                {
                    Image cardBg = mBadgeObj.GetComponent<Image>();
                    if (cardBg != null)
                    {
                        Color orig = cardBg.color;
                        cardBg.DOColor(new Color(0.45f, 1f, 0.55f, orig.a), 0.12f)
                            .SetEase(Ease.OutQuad)
                            .OnComplete(() => cardBg.DOColor(orig, 0.25f).SetEase(Ease.InQuad));
                    }
                    mBadgeObj.transform.DOKill();
                    mBadgeObj.transform.DOPunchScale(Vector3.one * goalTickCardPunchStrength, goalTickCardPunchDuration, 8, 0.8f);
                }
                return;
            }

            if (topGoalContainer == null) return;

            MatchGoal goal = null;
            if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null)
            {
                foreach (var g in MatchGoalManager.Instance.levelGoals)
                {
                    if (g.colorName.Equals(colorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        goal = g;
                        break;
                    }
                }
            }

            foreach (Transform child in topGoalContainer)
            {
                if (!child.name.Contains($"GoalCard_{colorName}_")) continue;

                // Bounce + flash + update the count text immediately
                Transform textObj = child.Find("Text");
                if (textObj != null)
                {
                    Text txt = textObj.GetComponent<Text>();
                    if (txt != null)
                    {
                        if (goal != null)
                        {
                            txt.text = $"x{goal.Remaining}";
                        }
                        txt.DOColor(new Color(1f, 0.92f, 0.25f), 0.12f)
                            .OnComplete(() => txt.DOColor(Color.white, 0.3f));
                        textObj.DOKill();
                        textObj.DOPunchScale(Vector3.one * goalTickTextPunchStrength, goalTickTextPunchDuration, 7, 0.9f);
                    }
                }

                // Flash the card green, then restore
                Image cardBg = child.GetComponent<Image>();
                if (cardBg != null)
                {
                    Color orig = cardBg.color;
                    cardBg.DOColor(new Color(0.45f, 1f, 0.55f, orig.a), 0.12f)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() => cardBg.DOColor(orig, 0.25f).SetEase(Ease.InQuad));
                }

                // Punch-scale the whole card
                child.DOKill();
                child.DOPunchScale(Vector3.one * goalTickCardPunchStrength, goalTickCardPunchDuration, 8, 0.8f);

                if (goal != null && goal.IsCompleted)
                {
                    RemoveGoalUI(colorName);
                }
                break;
            }
        }

        private void RemoveGoalUI(string colorName)
        {
            if (topGoalContainer == null) return;
            foreach (Transform child in topGoalContainer)
            {
                if (!child.name.Contains($"GoalCard_{colorName}_")) continue;

                child.DOKill();
                CanvasGroup cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();

                // Show check icon with a pop before the card exits
                Transform existingCheck = child.Find("CheckIcon");
                if (existingCheck == null)
                {
                    GameObject checkObj = new GameObject("CheckIcon");
                    checkObj.transform.SetParent(child, false);
                    RectTransform cr = checkObj.AddComponent<RectTransform>();
                    cr.anchorMin = Vector2.zero;
                    cr.anchorMax = Vector2.one;
                    cr.sizeDelta = Vector2.zero;
                    Image ci = checkObj.AddComponent<Image>();
                    Sprite checkSprite = LoadUISprite("Icons/Check");
                    if (checkSprite != null) { ci.sprite = checkSprite; ci.preserveAspect = true; }
                    else ci.color = new Color(0.45f, 1f, 0.55f);
                    checkObj.transform.localScale = Vector3.zero;
                    checkObj.transform.DOScale(Vector3.one * 1.3f, 0.2f).SetEase(Ease.OutBack);
                }

                // Count text hides immediately
                Transform textT = child.Find("Text");
                if (textT != null)
                {
                    Text t = textT.GetComponent<Text>();
                    if (t != null) t.text = "";
                }

                // Bounce up slightly, then shrink-fade out
                Sequence removeSeq = DOTween.Sequence();
                removeSeq.Append(child.DOScale(Vector3.one * goalRemoveBounceScale, goalRemoveBounceDuration).SetEase(Ease.OutQuad));
                removeSeq.Append(child.DOScale(Vector3.zero, goalRemoveShrinkDuration).SetEase(Ease.InBack, 1.8f));
                removeSeq.Join(cg.DOFade(0f, goalRemoveShrinkDuration * 0.8f).SetEase(Ease.InQuad));
                removeSeq.OnComplete(() => { if (child != null) Destroy(child.gameObject); });
                removeSeq.Play();
                break;
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
                    // The slice carrying the mecha counts as its own food type as well. It used to be
                    // excluded, because IsMechaItem is true for the HOST while the mecha is still riding
                    // it - so with three watermelons, one of them hosting, the box only ever asked for two
                    // and sealed itself early while a perfectly ordinary third slice was still in the pile.
                    // Tapping the mecha lifts it off and leaves the host behind as a plain slice, so it
                    // belongs in the food count from the start.
                    if (item.colorName == targetItem.colorName) count++;
                }
            }
            return Mathf.Max(1, count);
        }

        public static bool HasChildMecha(FindTargetObject item)
        {
            if (item == null) return false;
            foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
            {
                if (child != item.transform && (child.name.Contains("Mecha") || child.name.Contains("meccha") || child.name.Contains("Ragdoll")))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsHitOnMechaCollider(FindTargetObject item, Collider hitCollider)
        {
            if (hitCollider == null) return false;

            Transform t = hitCollider.transform;
            while (t != null && (item == null || t != item.transform))
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("mecha") || name.Contains("meccha") || name.Contains("ragdoll") || 
                    name.Contains("bodycollider") || name.Contains("mixamorig") || name.Contains("hullmesh") || name.Contains("bone") || name.Contains("arm") || name.Contains("leg") || name.Contains("head"))
                {
                    return true;
                }
                if (t.GetComponent<MechaRagdollSpawner>() != null || t.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        /// <summary>
        /// Called when the mecha is found or vanishes. Registers Mecha goal completion and updates the Mecha badge checkmark (tik).
        /// </summary>
        public void OnMechaVanished()
        {
            OnMechaFoundOrVanished();
        }

        public void OnMechaFoundOrVanished()
        {
            if (MatchGoalManager.Instance != null)
            {
                MatchGoalManager.Instance.RegisterMatchedItem(ObjectShapeType.Cube, "Mecha", 1);
            }
            SetGoalUIVisualTick("Mecha");
        }

        public bool TryCollectItemToDock(FindTargetObject item)
        {
            return TryCollectItemToDock(item, null);
        }

        public bool TryCollectItemToDock(FindTargetObject item, Collider hitCollider)
        {
            if (item == null) return false;

            // Strict Hit Sensitivity: Collect Mecha ONLY if the player clicked directly on the mecha character figure's collider!
            bool isMecha = false;
            if (hitCollider != null)
            {
                isMecha = IsHitOnMechaCollider(item, hitCollider);
            }
            else
            {
                // Standalone mecha object (not embedded in a host item)
                isMecha = item.name.Contains("Mecha") || item.name.Contains("meccha") ||
                         (item.colorName != null && item.colorName.Equals("mecha", System.StringComparison.OrdinalIgnoreCase));
            }

            // GAMEPLAY RULE: A host object carrying a hidden mecha CANNOT be collected/boxed until the player clicks directly on the mecha first to pluck it off!
            if (!isMecha && HasChildMecha(item))
            {
                return false;
            }

            string itemType = isMecha ? "Mecha" : item.colorName;

            int targetSlot = -1;

            if (isMecha)
            {
                // Find or attach MechaRunnerBehavior component on the hit mecha transform
                MechaRunnerBehavior runner = item.GetComponentInChildren<MechaRunnerBehavior>();
                if (runner == null && hitCollider != null)
                {
                    runner = hitCollider.GetComponentInParent<MechaRunnerBehavior>();
                }
                if (runner == null)
                {
                    Transform mechaRoot = item.transform;
                    if (hitCollider != null)
                    {
                        Transform t = hitCollider.transform;
                        while (t != null && t != item.transform)
                        {
                            string n = t.name.ToLowerInvariant();
                            if (n.Contains("mecha") || n.Contains("meccha") || n.Contains("ragdoll"))
                            {
                                mechaRoot = t;
                                break;
                            }
                            t = t.parent;
                        }
                    }
                    runner = mechaRoot.gameObject.AddComponent<MechaRunnerBehavior>();
                }

                if (runner.currentState == MechaRunnerBehavior.MechaState.CamouflagedOnHost)
                {
                    // 1st Tap: Start Running animation and wander around the tray area (No boxing!)
                    runner.StartRunningMode(item.gameObject);
                    return false;
                }
                else if (runner.currentState == MechaRunnerBehavior.MechaState.RunningInArea)
                {
                    // 2nd Tap: Play DOTween spin-shrink vanish exit animation (No boxing!)
                    runner.VanishAndDisappear();
                    return false;
                }

                return false;
            }
            else
            {
                // Slots 0 & 1 are for general items
                for (int i = 0; i < 2; i++)
                {
                    if (IsSlotBusy(i)) continue;

                    int req = slotRequiredCount[i] > 0 ? slotRequiredCount[i] : 3;
                    if (slotAssignedItemName[i] == itemType && GetSlotBoxContent(i).Count < req)
                    {
                        targetSlot = i;
                        break;
                    }
                }

                if (targetSlot == -1)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (IsSlotBusy(i)) continue;

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

                // Camouflage comes off the moment it is torn away from its host: the glass look only exists
                // to hide it in the pile, so from here on - the flight, the box, the sealed lid - it shows
                // as solid white.
                ChameleonCamouflage.ApplyRevealedMaterial(mechaObjToCollect);

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

                GetSlotBoxContent(targetSlot).Add(data);

                if (MatchGoalManager.Instance != null)
                {
                    MatchGoalManager.Instance.RegisterMatchedItem(mechaTarget.shapeType, "Mecha", 1);
                    SetGoalUIVisualTick("Mecha");
                }

                int reqCount = slotRequiredCount[targetSlot] > 0 ? slotRequiredCount[targetSlot] : 1;
                bool willShip = GetSlotBoxContent(targetSlot).Count >= reqCount;
                if (willShip)
                {
                    slotProcessing[targetSlot] = true;
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
                // Unparent any child mecha riding on this host item before collecting the host item so the mecha drops into the pile
                foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
                {
                    if (child != item.transform && (child.name.Contains("Mecha") || child.name.Contains("meccha") || child.name.Contains("Ragdoll")))
                    {
                        child.SetParent(null, true);
                        Rigidbody mechaRb = child.GetComponent<Rigidbody>();
                        if (mechaRb != null)
                        {
                            mechaRb.isKinematic = false;
                            mechaRb.WakeUp();
                        }
                        foreach (Collider c in child.GetComponentsInChildren<Collider>(true))
                        {
                            if (c != null) c.enabled = true;
                        }
                        break;
                    }
                }

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

                GetSlotBoxContent(targetSlot).Add(data);

                if (MatchGoalManager.Instance != null)
                {
                    MatchGoalManager.Instance.RegisterMatchedItem(item.shapeType, item.colorName, 1);
                    SetGoalUIVisualTick(item.colorName);
                }

                int reqCount = slotRequiredCount[targetSlot] > 0 ? slotRequiredCount[targetSlot] : 3;
                bool willShip = GetSlotBoxContent(targetSlot).Count >= reqCount;
                if (willShip)
                {
                    slotProcessing[targetSlot] = true;
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
            
            // Unity Bug Fix: SkinnedMeshRenderers can return 1000+ bounds before the Animator updates on frame 1.
            // If it's absurdly large, don't cache it, and return a safe fallback so the box doesn't become microscopic.
            if (maxExtent > 500f) return 48f;

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

            ApplyTapeVariant(box);

            return box;
        }

        /// <summary>
        /// Gives this box its own tape colour so a row of shipped boxes never reads as identical copies.
        ///
        /// Only the BoxTape material slot is touched - Box.fbx keeps tape on its own faces (the two
        /// last-closing flaps and the body), separate from the Cardboard slot, so the cardboard is left
        /// exactly as authored. Colours are cycled rather than picked at random, which is what guarantees
        /// consecutive boxes actually differ instead of occasionally repeating.
        /// </summary>
        private void ApplyTapeVariant(GameObject box)
        {
            if (box == null || boxTapeColors == null || boxTapeColors.Length == 0) return;

            Color tint = boxTapeColors[nextTapeVariant % boxTapeColors.Length];
            nextTapeVariant++;

            // One instance shared by every renderer on THIS box. Copying per renderer would multiply
            // materials for no visual gain, and tinting the slot in place would recolour the shared
            // BoxTape.mat asset - repainting every box in the game, including ones already shipped.
            Material variant = null;

            foreach (Renderer r in box.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool touched = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(TapeMaterialPrefix)) continue;

                    if (variant == null)
                    {
                        variant = new Material(mats[i]) { name = mats[i].name + "_Variant" };
                        if (variant.HasProperty("_BaseColor")) variant.SetColor("_BaseColor", tint);
                        if (variant.HasProperty("_Color")) variant.SetColor("_Color", tint);
                    }

                    mats[i] = variant;
                    touched = true;
                }

                if (touched) r.sharedMaterials = mats;
            }
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

        /// <summary>
        /// Clears the packed boxes riding the conveyor, leaving the dock's own slot boxes alone.
        ///
        /// A shipped box belongs to the level that packed it, but they were surviving into the next one:
        /// the only thing that ever cleared them was CleanupAllOldCardboardBoxes, and a level load does not
        /// go through it - so the belt still carried the previous level's boxes.
        /// </summary>
        private void ClearShippedBoxes()
        {
            for (int i = completedBoxObjects.Count - 1; i >= 0; i--)
            {
                GameObject box = completedBoxObjects[i];
                if (box == null) continue;

                // A box can still be mid-flight when the level ends; killing its tweens first stops them
                // writing to a transform that is about to be destroyed.
                tweeningDockObjects.Remove(box);
                box.transform.DOKill();
                SafeDestroy(box);
            }

            completedBoxObjects.Clear();
            completedBoxesCount = 0;
        }

        private void CleanupAllOldCardboardBoxes()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (slotBox[i] != null) SafeDestroy(slotBox[i]);
                slotBox[i] = null;
            }

            ClearShippedBoxes();

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

            if (cardboardBoxOpenedPrefab != null)
            {
                for (int i = 0; i < MAX_SLOTS; i++)
                {
                    if (slotBox[i] == null)
                    {
                        GameObject existingBox = GameObject.Find($"Slot3DBox_Closed_{i}");
                        if (existingBox != null)
                        {
                            slotBox[i] = existingBox;
                        }
                        else
                        {
                            GameObject box = CreatePackagingBox(i);
                            if (box != null)
                            {
                                slotBox[i] = box;
                            }
                        }
                    }

                    if (slotBox[i] != null)
                    {
                        float fitScale = ComputeFitScaleForSlot(i, slotBox[i]) * 1.25f;
                        slotBox[i].transform.localScale = Vector3.one * fitScale;
                        slotBox[i].transform.rotation = BoxDisplayRotation(BoxSlotTiltEuler);
                        Vector3 slotPos = GetSlotWorldPosition(i);
                        if (slotPos != Vector3.zero)
                        {
                            slotBox[i].transform.position = slotPos;
                        }
                    }
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

        private void CleanupDuplicateConveyorBelts()
        {
            List<GameObject> sceneBelts = new List<GameObject>();
            GameObject[] allObjs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (GameObject go in allObjs)
            {
                if (go == null) continue;
                if (go.name == "Conveyor_Belt_3D" || go.name.StartsWith("Conveyor_Belt_3D"))
                {
                    sceneBelts.Add(go);
                }
            }

            if (sceneBelts.Count == 0)
            {
                conveyorInstance = null;
                return;
            }

            // Keep the belt closest to the camera (lowest Z coordinate = user's bottom belt)
            sceneBelts.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            conveyorInstance = sceneBelts[0];

            // Immediately destroy all extra top duplicate belts
            for (int i = 1; i < sceneBelts.Count; i++)
            {
                GameObject extra = sceneBelts[i];
                if (extra != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(extra);
                    else Destroy(extra);
#else
                    Destroy(extra);
#endif
                }
            }
        }

        private void EnsureConveyorBelt()
        {
            // Sweep and remove any extra top duplicate belts from the scene
            CleanupDuplicateConveyorBelts();

            // If a conveyor belt already exists in the scene (user's bottom belt), NEVER build another!
            if (conveyorInstance != null)
            {
                return;
            }

            if (!autoSpawnConveyorBelt) return;

            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null) return;

            if (conveyorPrefab == null) conveyorPrefab = Resources.Load<GameObject>(ConveyorResourcePath);
            if (conveyorPrefab == null) return;

            Quaternion sceneBeltRotation = Quaternion.Euler(conveyorWorldRotationEuler)
                                          * conveyorPrefab.transform.rotation;
            int sceneTilesToBuild = conveyorTileCount <= 0 ? 2 : conveyorTileCount;

            conveyorInstance = ConveyorBelt.BuildRow(conveyorPrefab, transform, mainCamera,
                                                     conveyorWorldPosition, sceneBeltRotation,
                                                     conveyorWorldWidth, conveyorWorldHeight,
                                                     conveyorSpeed, conveyorFlipStripes,
                                                     sceneTilesToBuild, conveyorArrowStride, conveyorArrowScale,
                                                     conveyorYScaleMultiplier);
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

                // Eased into place rather than assigned outright. Every box's target is recomputed from the
                // lead box's spacing, so the moment a newly shipped box joined the run, all the others were
                // teleported to their new even spacing in a single frame - the visible snap. Only the lead
                // box keeps a hard assignment, because it IS the motion reference; the followers all lag by
                // the same tiny amount, so the spacing between them stays exactly even.
                float gap = Vector3.Distance(box.transform.position, targetPos);
                if (gap > spanX * 0.5f)
                {
                    // Wrapped around the far edge - easing across would drag it back over the whole screen.
                    box.transform.position = targetPos;
                }
                else
                {
                    box.transform.position = Vector3.Lerp(box.transform.position, targetPos,
                                                          Time.deltaTime * conveyorBoxSettleSpeed);
                }

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
            if (use3DScenePosition)
            {
                // Preserve the exact transform user set in Scene View - do not overwrite!
                return;
            }

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

            Sequence seq = DOTween.Sequence();

            // Parabolic Arc Flight into box
            seq.Append(obj3D.transform.DOJump(boxItemPos, GetDockJumpPower(1.15f), 1, 0.38f).SetEase(Ease.OutCubic));
            seq.Join(obj3D.transform.DOScale(targetScale, 0.38f).SetEase(Ease.OutQuad));
            seq.Join(obj3D.transform.DORotateQuaternion(targetRot, 0.38f).SetEase(Ease.OutQuad));

            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);

                if (obj3D != null)
                {
                    obj3D.transform.DOPunchScale(Vector3.one * 0.15f, 0.20f, 5, 0.5f);
                }

                if (slotIndex >= 0 && slotIndex < MAX_SLOTS && slotBox[slotIndex] != null)
                {
                    PunchBox(slotBox[slotIndex], new Vector3(0.20f, -0.12f, 0.20f), 0.30f, 6, 0.7f);
                }

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
                    if ((slotBox[i].transform.position - slotWorldPos).sqrMagnitude > 0.00001f)
                    {
                        slotBox[i].transform.position = Vector3.Lerp(slotBox[i].transform.position, slotWorldPos, Time.deltaTime * 22f);
                    }
                    float fitScale = ComputeFitScaleForSlot(i, slotBox[i]) * 1.25f;
                    slotBox[i].transform.localScale = Vector3.one * fitScale;
                    Quaternion targetRot = BoxDisplayRotation(BoxSlotTiltEuler);
                    if (Quaternion.Angle(slotBox[i].transform.rotation, targetRot) > 0.05f)
                    {
                        slotBox[i].transform.rotation = Quaternion.Slerp(slotBox[i].transform.rotation, targetRot, Time.deltaTime * 15f);
                    }
                }

                List<DockItemData> itemsInBox = slotBoxContents[i];
                if (itemsInBox != null)
                {
                    int reqCount = slotRequiredCount[i] > 0 ? slotRequiredCount[i] : 3;
                    float scaleRatio = GetItemFitScaleRatioInsideBox(reqCount);
                    Quaternion itemTargetRot = GetDockItemSidewaysRotation();

                    for (int k = 0; k < itemsInBox.Count; k++)
                    {
                        DockItemData data = itemsInBox[k];
                        if (data != null && data.targetObject != null && !tweeningDockObjects.Contains(data.targetObject.gameObject))
                        {
                            Vector3 boxItemPos = GetItemPositionInsideBox(k, reqCount, slotWorldPos);
                            if ((data.targetObject.transform.position - boxItemPos).sqrMagnitude > 0.00001f)
                            {
                                data.targetObject.transform.position = Vector3.Lerp(data.targetObject.transform.position, boxItemPos, Time.deltaTime * 22f);
                            }
                            float fitScale = ComputeFitScaleForSlot(i, data.targetObject.gameObject) * scaleRatio;
                            data.targetObject.transform.localScale = Vector3.one * fitScale;
                            if (Quaternion.Angle(data.targetObject.transform.rotation, itemTargetRot) > 0.05f)
                            {
                                data.targetObject.transform.rotation = Quaternion.Slerp(data.targetObject.transform.rotation, itemTargetRot, Time.deltaTime * 15f);
                            }
                        }
                    }
                }
            }
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

            bool layFlatOnSeam = itemData.colorName == "Mecha" || IsMechaItem(itemData.targetObject);

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

            if (!(layFlatOnSeam && TryLayAlongLidSeam(box, group.transform, display)))
            {
                float boxLidSize = hasBoxBounds ? Mathf.Min(boxBounds.size.x, boxBounds.size.z) : 0.35f;
                float itemSize = GetObjectStaticUnscaledMaxExtent(display);
                float desiredWorldSize = boxLidSize * 0.45f;
                float scaleFactor = itemSize > 1e-4f ? desiredWorldSize / itemSize : 0.12f;
                display.transform.localScale = Vector3.one * scaleFactor;
            }

            group.transform.DOKill();
            group.transform.localScale = Vector3.zero;
            group.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        /// <summary>
        /// Lays a humanoid display model flat along the lid seam instead of standing it up.
        ///
        /// The generic Euler(20, 45, 0) showcase tilt suits a compact food item, but leaves the mecha
        /// standing bolt upright off the lid. Instead it is laid full length along the join between the two
        /// flaps that shut LAST - measured from Box.fbx's own animation, that is BoxFlap_Yn and BoxFlap_Yp
        /// (they reach closed at frames 58/61, while Xn/Xp are already down by 26/30), and they meet along
        /// the box's long axis.
        ///
        /// Everything is derived from the flap transforms at runtime rather than from hard-coded axes, so
        /// it survives the box being tilted for the slot, the shelf, or anything else.
        /// </summary>
        private bool TryLayAlongLidSeam(GameObject box, Transform group, GameObject display)
        {
            Transform xn = FindDeepChild(box.transform, "BoxFlap_Xn");
            Transform xp = FindDeepChild(box.transform, "BoxFlap_Xp");
            Transform yn = FindDeepChild(box.transform, "BoxFlap_Yn");
            Transform yp = FindDeepChild(box.transform, "BoxFlap_Yp");
            if (xn == null || xp == null || yn == null || yp == null) return false;

            // The last-closing pair meet along the line running between the FIRST-closing pair's hinges.
            Vector3 seamDir = xp.position - xn.position;
            Vector3 acrossSeam = yp.position - yn.position;
            float seamLength = seamDir.magnitude;
            if (seamLength < 1e-5f || acrossSeam.sqrMagnitude < 1e-10f) return false;

            seamDir /= seamLength;
            Vector3 lidNormal = Vector3.Cross(acrossSeam.normalized, seamDir).normalized;
            if (Vector3.Dot(lidNormal, Vector3.up) < 0f) lidNormal = -lidNormal;

            // Orientation lives on the GROUP, so the pop-in scale that follows grows the model out from the
            // seam's centre. Putting it on the model instead would make it swell out of one end.
            group.SetPositionAndRotation((yn.position + yp.position) * 0.5f,
                                         Quaternion.LookRotation(lidNormal, seamDir));

            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = Vector3.one;

            // Fitted against BOTH lid axes, not just the seam. A T-pose is nearly as wide across the arms
            // as it is long, so sizing it to fill the seam alone left the arms hanging over the sides of
            // the box - the lid is markedly narrower across the seam than along it. Whichever axis runs out
            // first decides the scale.
            float lengthAt1 = MeasureExtentAlong(display, seamDir);
            float armSpanAt1 = MeasureExtentAlong(display, acrossSeam.normalized);
            if (lengthAt1 < 1e-5f || armSpanAt1 < 1e-5f) return false;

            float acrossLength = acrossSeam.magnitude;
            float scaleFactor = Mathf.Min(seamLength * mechaLidFillRatio / lengthAt1,
                                          acrossLength * mechaLidFillRatio / armSpanAt1);

            float targetLength = lengthAt1 * scaleFactor;
            display.transform.localScale = Vector3.one * scaleFactor;

            // Offsets go through InverseTransformVector rather than being written straight into
            // localPosition. seamLength and targetLength are WORLD measurements, but localPosition is in
            // the group's own space - and the group hangs off a box scaled to about 0.0066, so writing
            // world numbers there shrank the offset to essentially nothing and the model sat off-centre.
            //
            // The mecha's origin is at its FEET, so it has to come back half its own length along the seam
            // to end up centred on it.
            Vector3 seamCentre = group.position;
            display.transform.localPosition =
                group.InverseTransformVector(-seamDir * (targetLength * 0.5f));

            // Then it is settled onto the lid by measurement: the model lies on its back, so how far its
            // back reaches below its own pivot depends on the rig, and guessing an offset sinks it into the
            // cardboard (measured -0.05 before this).
            float lowest = float.MaxValue;
            foreach (Renderer r in display.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                Bounds rb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? rb.min.x : rb.max.x,
                        (i & 2) == 0 ? rb.min.y : rb.max.y,
                        (i & 4) == 0 ? rb.min.z : rb.max.z);
                    lowest = Mathf.Min(lowest, Vector3.Dot(corner - seamCentre, lidNormal));
                }
            }

            if (lowest < float.MaxValue)
            {
                float lift = seamLength * mechaLidClearance - lowest;
                display.transform.localPosition += group.InverseTransformVector(lidNormal * lift);
            }

            return true;
        }

        /// <summary>How far the object's visible geometry reaches along a world axis, at its current pose.</summary>
        private static float MeasureExtentAlong(GameObject go, Vector3 axis)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);

                    float p = Vector3.Dot(corner, axis);
                    if (p < min) min = p;
                    if (p > max) max = p;
                }
            }

            return max > min ? max - min : 0f;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) return t;
            }
            return null;
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
            slotProcessing[slotIndex] = true;
            bool isMechaSlot = (slotIndex == 2);

            // Reserve the shipping index immediately so sequential boxes never get duplicate or skipped indices
            int shipIndex = isMechaSlot ? -1 : completedBoxesCount++;


            List<DockItemData> filledItems = new List<DockItemData>(slotBoxContents[slotIndex]);
            GameObject box = slotBox[slotIndex];

            DockItemData firstItemData = filledItems.Count > 0 ? filledItems[0] : null;

            float baseScale = box != null ? ComputeFitScaleForSlot(slotIndex, box) * 1.30f : 0.18f;

            Sequence boxSeq = DOTween.Sequence();

            // Phase 1: Items inside shrink smoothly into the box bottom (0.45s)
            if (firstItemData != null) SetGoalUIVisualTick(firstItemData.colorName);

            foreach (var itemData in filledItems)
            {
                if (itemData.targetObject != null)
                {
                    tweeningDockObjects.Add(itemData.targetObject.gameObject);
                    boxSeq.Join(itemData.targetObject.transform.DOScale(Vector3.zero, 0.45f).SetEase(Ease.InCubic));
                }
            }

            // Phase 2: The box's own four lid flaps fold shut in place and the tape gun then runs over the
            // seam - the box never moves or scales, so it can't appear to sink into the ground.
            // The duration was 0.65s while the clip was flaps-only (70 frames); the taping re-export
            // doubled it to 140 frames, which played the whole thing at twice the intended speed.
            if (box != null)
            {
                tweeningDockObjects.Add(box);
                AnimateBoxFlaps(boxSeq, box, true, 1.1f);
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
                // Regular food boxes: Jump to the walking 3D conveyor belt.
                //
                // The landing spot is worked out INSIDE the callback, not when this sequence is built.
                // Building it up front baked in a target that was already ~2.4s stale by the time the box
                // touched down (0.45s of items shrinking + 0.65s of flaps + a 0.30s pause + the 1.0s jump),
                // and the belt keeps running throughout - about a full unit of travel. The box therefore
                // landed a whole slot behind the formation and then slid forwards into place, which is the
                // "distances re-animating" that shows up whenever a box joins the run.
                //
                // The extra lead of speed x flight time is what makes it land where its slot WILL be,
                // rather than where the slot was when it set off.
                const float shipFlight = 1.0f;
                if (box != null)
                {
                    boxSeq.AppendCallback(() =>
                    {
                        if (box == null) return;

                        Vector3 target = GetRedMarkedCompletedBoxWorldPos(shipIndex);
                        if (mainCamera != null)
                        {
                            target += mainCamera.transform.right * (conveyorBoxMoveSpeed * shipFlight);
                        }

                        if (firstItemData != null) RemoveGoalUI(firstItemData.colorName);
                        box.transform.DOJump(target, GetDockJumpPower(0.9f), 1, shipFlight).SetEase(Ease.OutCubic);
                        box.transform.DOScale(baseScale * 1.15f, shipFlight);
                        box.transform.DORotateQuaternion(BoxDisplayRotation(BoxShelfTiltEuler), shipFlight);
                    });
                    boxSeq.AppendInterval(shipFlight);
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
                        RefreshTargetGoalsUI();
                    }

                    slotBoxContents[slotIndex].Clear();
                    slotAssignedItemName[slotIndex] = null;
                    slotRequiredCount[slotIndex] = 0;

                    UpdateSlotBadgesUI();
                    slotProcessing[slotIndex] = false;
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
                        RefreshTargetGoalsUI();
                    }

                    slotBoxContents[slotIndex].Clear();
                    slotAssignedItemName[slotIndex] = "Mecha_Completed";

                    UpdateSlotBadgesUI();
                    slotProcessing[slotIndex] = false;
                });
            }
        }

        private void UpdateSlotBadgesUI()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (i < slotBadgeTexts.Count && slotBadgeTexts[i] != null)
                {
                    slotBadgeTexts[i].text = "";
                }
            }
        }

        public void HideAllOverlayPanels()
        {
            // The name has always promised this, but until now it only reset the dock slots. LevelManager
            // calls it on every load, so a Win/Lose popup opened by the previous level would otherwise stay
            // on screen over the new one on any path that does not go through the panel's own button.
            if (WinLosePanelController.Instance != null)
            {
                WinLosePanelController.Instance.HideAll();
            }

            ClearShippedBoxes();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                List<DockItemData> content = GetSlotBoxContent(i);
                for (int k = content.Count - 1; k >= 0; k--)
                {
                    if (content[k] != null && content[k].targetObject != null)
                    {
                        SafeDestroy(content[k].targetObject.gameObject);
                    }
                }
                content.Clear();
                slotAssignedItemName[i] = null;
                slotRequiredCount[i] = 0;
                slotProcessing[i] = false;
            }

            tweeningDockObjects.Clear();

            UpdateSlotBadgesUI();
        }
    }
}
