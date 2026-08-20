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
    /// Match-N collection dock.
    ///
    /// Tapped items land in a five-slot tray at the bottom of the screen and group themselves next to their
    /// own kind. The moment a group reaches the count an active customer order is asking for, the whole
    /// group flies up and slams into that order's card, completing it. Anything nobody ordered just sits in
    /// the tray - fill all five slots without completing an order and the level is lost.
    /// </summary>
    [ExecuteAlways]
    public class CanvasUIDesignManager : MonoBehaviour
    {
        public static CanvasUIDesignManager Instance { get; private set; }

        [Header("Canvas Configuration")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);
        [SerializeField] private float uiPlaneDistance = 3.2f;

        [Header("Dock 3D Item Placement")]
        [Tooltip("How far in front of the camera the docked 3D items sit.")]
        [SerializeField] private float dockCameraDepth = 1.6f;
        [Tooltip("How much of a slot an item fills, 1 being edge to edge.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float dockItemFillRatio = 0.62f;
        [SerializeField] private float miniObjectDockScale = 0.08f;

        [Header("Header Order Panel")]
        [SerializeField] private Vector2 headerSize = new Vector2(950f, 220f);
        [SerializeField] private Vector2 headerAnchoredPosition = new Vector2(0f, -60f);
        [SerializeField] private int titleFontSize = 28;
        [SerializeField] private int goalContainerSpacing = 24;

        [Header("Order Cards")]
        [SerializeField] private Vector2 goalCardSize = new Vector2(180f, 155f);
        [Tooltip("Where the rule down an order card sits, as a fraction of its width. Left of it: the item and how many are still wanted. Right of it: the customer the order belongs to.")]
        [Range(0.3f, 0.9f)]
        [SerializeField] private float goalCardDividerX = 0.60f;
        [SerializeField] private float goalCardDividerWidth = 4f;
        [SerializeField] private Color goalCardDividerColor = new Color(1f, 1f, 1f, 0.65f);
        [Tooltip("Portrait for the customer half of an order card. Left empty, a plain placeholder panel is drawn so the zone is still visible.")]
        [SerializeField] private Sprite customerPortraitSprite;
        [SerializeField] private float goalCardIconSize = 65f;
        [SerializeField] private int goalCardFontSize = 32;
        [SerializeField] private float goalCard3DModelScale = 450f;
        [SerializeField] private float goalCard3DModelTargetSize = 150f;
        [SerializeField] private Vector3 goalCard3DModelLocalPosition = new Vector3(0f, 0f, -25f);
        [SerializeField] private float goalCard3DModelTiltX = 15f;

        [Header("Order Card Animations")]
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

        [Header("Bottom Dock (square slots)")]
        [Tooltip("Kaç kare slot olsun. Slotlar dolar ve hiçbir sipariş tamamlanmazsa oyun biter.")]
        [Min(1)]
        [SerializeField] private int dockCapacity = 5;
        [SerializeField] private Vector2 dockPanelAnchoredPosition = new Vector2(0f, 90f);
        [Tooltip("Edge length of one square slot, in reference-resolution pixels. Slots shrink below this when the row would otherwise be wider than Dock Panel Max Width.")]
        [SerializeField] private float dockSlotSize = 165f;
        [Tooltip("Widest the whole dock panel may get, in reference-resolution pixels. The slot size is capped so the row always fits inside it.")]
        [SerializeField] private float dockPanelMaxWidth = 1020f;
        [SerializeField] private float dockSlotSpacing = 18f;
        [SerializeField] private float dockPanelPadding = 22f;
        [SerializeField] private Color dockPanelColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color dockSlotEmptyColor = new Color(0.38f, 0.78f, 0.12f, 0.95f);
        [SerializeField] private Color dockSlotFilledColor = new Color(0.52f, 0.94f, 0.20f, 1.0f);

        [Header("Item Collection Flight")]
        [SerializeField] private float collectFlightDuration = 0.38f;

        [Header("Match Delivery Flight")]
        [Tooltip("How far the matched group lifts out of the tray before it launches at the card.")]
        [SerializeField] private float matchLiftDistance = 0.22f;
        [SerializeField] private float matchLiftDuration = 0.16f;
        [SerializeField] private float matchFlightDuration = 0.34f;
        [Tooltip("Delay between consecutive items of the same group leaving the tray.")]
        [SerializeField] private float matchStaggerDelay = 0.05f;

        [Header("Shuffle Button")]
        [SerializeField] private Vector2 shuffleButtonPosition = new Vector2(60f, 240f);
        [SerializeField] private Vector2 shuffleButtonSize = new Vector2(80f, 80f);
        [SerializeField] private float shuffleIconSize = 45f;

        [Header("Colours")]
        [Tooltip("Solid fill colour behind the whole scene.")]
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.09f, 0.24f);
        [Tooltip("Colour of the UI badges, order cards and the shuffle button.")]
        [SerializeField] private Color uiAccentColor = new Color(0f, 26f / 255f, 112f / 255f, 1f); // #001A70
        [Tooltip("Flash/text colour used everywhere something is marked complete.")]
        [SerializeField] private Color successAccentColor = new Color(0.45f, 1f, 0.55f);
        [Tooltip("Text colour for the Mecha goal badge while it's still outstanding.")]
        [SerializeField] private Color mechaAccentColor = new Color(0.4f, 0.95f, 1f);

        /// <summary>Tray capacity. Filling every slot without completing an order loses the level.</summary>
        public int DockCapacity => Mathf.Max(1, dockCapacity);

        /// <summary>
        /// Slot edge length after fitting the whole row inside <see cref="dockPanelMaxWidth"/>. Without this
        /// cap, raising the slot count simply pushed the outer slots off both sides of the screen.
        /// </summary>
        private float EffectiveSlotSize()
        {
            int n = DockCapacity;
            float available = dockPanelMaxWidth - dockPanelPadding * 2f - (n - 1) * dockSlotSpacing;
            return Mathf.Max(20f, Mathf.Min(dockSlotSize, available / n));
        }

        private const string RetiredCardPrefix = "Retiring_";
        private const string OrderCardPrefix = "GoalCard_";

        private Camera mainCamera;
        private Canvas mainCanvas;
        private RectTransform topGoalContainer;
        private RectTransform dockPanelRect;

        private readonly List<RectTransform> slotRects = new List<RectTransform>();
        private readonly List<Image> slotImages = new List<Image>();

        // Left to right, one entry per occupied slot. Same-type entries are always contiguous because
        // inserts land right after the last item of their own kind.
        private readonly List<DockItemData> dockItems = new List<DockItemData>();

        // Objects whose transform is owned by a tween right now, so the per-frame slot alignment leaves
        // them alone instead of fighting the animation.
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private bool gameOverTriggered;

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        private const string UIAccentButton = "Buttons/Button Blue";
        // Note the Misc/ subfolder - the blue square button lives there, unlike the violet one it replaces,
        // and loading it from "Buttons/" silently returns null and leaves the button spriteless.
        private const string UIAccentSquareButton = "Buttons/Misc/Small Square Button Blue";

        /// <summary>Measured mid-tone of both blue button sprites, which is what the tint multiplies.</summary>
        private static readonly Color UIAccentSpriteMidtone = new Color(0.18f, 0.612f, 1f);

        /// <summary>
        /// Image tint MULTIPLIES the sprite, so asking for #001A70 and assigning it straight would land
        /// somewhere much darker. The wanted colour is divided by the sprite's own mid-tone instead.
        /// </summary>
        private Color UIAccentTint => new Color(
            Mathf.Clamp01(uiAccentColor.r / UIAccentSpriteMidtone.r),
            Mathf.Clamp01(uiAccentColor.g / UIAccentSpriteMidtone.g),
            Mathf.Clamp01(uiAccentColor.b / UIAccentSpriteMidtone.b),
            uiAccentColor.a);

        private static Sprite LoadUISprite(string resourcesPath)
        {
            if (!spriteCache.TryGetValue(resourcesPath, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(resourcesPath);
                spriteCache[resourcesPath] = sprite;
            }
            return sprite;
        }

        private static Sprite IconSprite(string iconName) => LoadUISprite($"Icons/{iconName}");

        private static void ApplySlicedSprite(Image img, Sprite sprite)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            EnsureCanvasStructure();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            if (!Application.isPlaying)
            {
                EnsureCanvasStructure();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= EditorRefresh;
                UnityEditor.EditorApplication.delayCall += EditorRefresh;
            }
        }

        private void EditorRefresh()
        {
            if (this == null) return;
            EnsureCanvasStructure();
        }

        [ContextMenu("Rebuild UI")]
        public void RebuildUIFromContextMenu()
        {
            EnsureCanvasStructure();
        }
#endif

        private void Start()
        {
            StartConveyorDecor();
            StartCoroutine(RefreshAfterLayout());
        }

        /// <summary>
        /// Deferred a frame: the order cards size their 3D icons from the canvas layout, which has not
        /// settled during Start().
        /// </summary>
        private System.Collections.IEnumerator RefreshAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (LevelManager.Instance == null)
            {
                if (MatchGoalManager.Instance != null) MatchGoalManager.Instance.SetupLevelGoals();
                if (CustomerOrderManager.Instance != null) CustomerOrderManager.Instance.SetupCustomerOrders();
            }

            RefreshTargetGoalsUI();
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            if (mainCanvas != null && mainCanvas.worldCamera == null && mainCamera != null)
            {
                mainCanvas.worldCamera = mainCamera;
            }

            AlignDockItemsWithSlots();
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Build Canvas UI Design")]
        public static void BuildCanvasUIDesignTool()
        {
            GameObject sceneController = GameObject.Find("Physics_Scene_Controller");
            if (sceneController == null) sceneController = new GameObject("Physics_Scene_Controller");

            CanvasUIDesignManager manager = sceneController.GetComponent<CanvasUIDesignManager>();
            if (manager == null) manager = sceneController.AddComponent<CanvasUIDesignManager>();

            manager.EnsureCanvasStructure();
            Selection.activeGameObject = sceneController;
            Debug.Log("🧺 Match-N biriktirme dock'u kuruldu.");
        }
#endif

        // ---------------------------------------------------------------------------------------------
        // Canvas construction
        // ---------------------------------------------------------------------------------------------

        public void EnsureCanvasStructure()
        {
            EnsureEventSystem();
            CleanupLegacyPackagingObjects();

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

            if (canvasObj.GetComponent<GraphicRaycaster>() == null) canvasObj.AddComponent<GraphicRaycaster>();

            EnsureBackgroundCanvas();
            BuildHeaderOrderPanel(canvasObj.transform);
            BuildBottomDockPanel(canvasObj.transform);
            BuildShuffleButton(canvasObj.transform);
            RemoveTrashButton(canvasObj.transform);
            Canvas.ForceUpdateCanvases();

            CustomerOrderManager orderManager = EnsureSingleOrderManager();

            // Built here rather than left to the manager's own start-up: a component added by the line above
            // has not had its Awake run at all in edit mode.
            if (orderManager != null) orderManager.SetupCustomerOrders();
            else Debug.LogWarning("🧾 CustomerOrderManager kurulamadı - sipariş kartları boş kalır.");

            RefreshTargetGoalsUI();
        }

        /// <summary>
        /// Returns the one order manager in the scene, creating it if needed and stripping any duplicates.
        ///
        /// This used to guard an UNCONDITIONAL AddComponent with <c>CustomerOrderManager.Instance == null</c>.
        /// That singleton is only assigned by an Awake which never runs in edit mode, so every
        /// [ExecuteAlways] rebuild and every domain reload stacked another copy onto the same GameObject -
        /// the scene had 67 of them piled up. Searching by TYPE is what actually finds an existing one.
        ///
        /// Duplicates are removed COMPONENT-wise, never by destroying their GameObject: they all sit on the
        /// one holder, so deleting the object would take the surviving manager with it.
        /// </summary>
        private static CustomerOrderManager EnsureSingleOrderManager()
        {
            CustomerOrderManager[] found = Object.FindObjectsByType<CustomerOrderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            CustomerOrderManager kept = null;
            foreach (CustomerOrderManager mgr in found)
            {
                if (mgr == null) continue;
                if (kept == null) kept = mgr;
                else SafeDestroy(mgr);
            }

            if (kept != null) return kept;

            GameObject mgrObj = new GameObject("Customer_Order_Manager");
            return mgrObj.AddComponent<CustomerOrderManager>();
        }

        /// <summary>
        /// Removes what is left of the cardboard-box packaging flow that this dock replaces: the open slot
        /// boxes and the sealed boxes that used to ride the belt. They are plain scene objects created by
        /// the old code, so nothing else clears them.
        ///
        /// NOTE: Trash_Button is NOT in this list. It looks like the same kind of leftover - it was built by
        /// the box system's BuildTrashButton - but the dock keeps its own trash button under that same name
        /// (see BuildTrashButton below), so destroying it here would delete the dock's own button on every
        /// rebuild.
        /// </summary>
        private void CleanupLegacyPackagingObjects()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                if (go.name.StartsWith("Slot3DBox") || go.name.StartsWith("Delivery_3DBox")
                    || go.name.StartsWith("PackagingBox") || go.name.StartsWith("Cardboard Box")
                    || go.name.StartsWith("ConveyorInitialBox") || go.name.StartsWith("Trash_Button"))
                {
                    SafeDestroy(go);
                }
            }
        }

        /// <summary>The belt is scene decor now that nothing rides it, but it should still be moving.</summary>
        private void StartConveyorDecor()
        {
            foreach (ConveyorBelt tile in Object.FindObjectsByType<ConveyorBelt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tile != null) tile.AutoScroll = true;
            }
        }

        public void EnsureBackgroundCanvas()
        {
            Transform existingImageCanvas = transform.Find("MatchFactory_Background_Canvas");
            if (existingImageCanvas != null) SafeDestroy(existingImageCanvas.gameObject);

            // The main gameplay camera is set to ClearFlags.Depth (see ScenePhysicsSetup.SetupCamera),
            // meaning it never clears the color buffer itself - it relies entirely on this background
            // camera clearing to a solid color first.
            Camera bgCam = null;
            Transform bgCamTransform = transform.Find("Background_Camera");
            if (bgCamTransform != null) bgCam = bgCamTransform.GetComponent<Camera>();
            if (bgCam == null)
            {
                GameObject bgCamObj = new GameObject("Background_Camera");
                bgCamObj.transform.SetParent(transform, false);
                bgCam = bgCamObj.AddComponent<Camera>();
            }

            bgCam.depth = -10;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = backgroundColor;
            bgCam.orthographic = true;
            bgCam.orthographicSize = 5f;
            bgCam.nearClipPlane = 0.1f;
            bgCam.farClipPlane = 100f;
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

        /// <summary>
        /// A UI child, guaranteed to carry a RectTransform.
        ///
        /// THE TRAP THIS EXISTS FOR: `new GameObject(name)` gives the object a plain Transform, and
        /// `AddComponent&lt;RectTransform&gt;()` does NOT convert one into the other - the add silently fails
        /// and returns null, so the very next `rect.sizeDelta = ...` throws and takes the rest of the build
        /// loop with it. That is exactly what left the dock with only the two slots an older build had
        /// already saved into the scene: `DockSlot_2` was created bare, the loop threw, and slots 3+ were
        /// never reached. Any such stray already sitting in the scene is replaced here rather than patched.
        /// </summary>
        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing is RectTransform) return existing.gameObject;

            if (existing != null)
            {
                // Renamed and detached first: in Play, Destroy only lands at the end of the frame, so the
                // stray would otherwise still answer Find() and still be laid out by the layout group.
                existing.name = childName + "_Stale";
                existing.SetParent(null, false);
                SafeDestroy(existing.gameObject);
            }

            return NewUIObject(childName, parent);
        }

        /// <summary>
        /// Creates a UI object with its RectTransform in the constructor. Never `new GameObject(name)`
        /// followed by `AddComponent&lt;RectTransform&gt;()` - see <see cref="GetOrCreateChild"/> for why that
        /// pattern throws.
        /// </summary>
        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void DestroyChildIfExists(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null) SafeDestroy(child.gameObject);
        }

        private void BuildHeaderOrderPanel(Transform parent)
        {
            GameObject headerObj = GetOrCreateChild(parent, "Header_Goal_Panel");
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = headerAnchoredPosition;
            headerRect.sizeDelta = headerSize;

            DestroyChildIfExists(headerObj.transform, "Level_Badge");
            DestroyChildIfExists(headerObj.transform, "Level_Text");
            DestroyChildIfExists(headerObj.transform, "Mecha_Goal_Badge");
            DestroyChildIfExists(headerObj.transform, "Mecha_Goal_Text");

            GameObject timerBadgeObj = GetOrCreateChild(headerObj.transform, "timer_badge");
            RectTransform timerBadgeRect = timerBadgeObj.GetComponent<RectTransform>();
            timerBadgeRect.anchorMin = new Vector2(0.5f, 0.0f);
            timerBadgeRect.anchorMax = new Vector2(0.5f, 0.0f);
            timerBadgeRect.pivot = new Vector2(0.5f, 1.0f);
            timerBadgeRect.anchoredPosition = new Vector2(0f, -14f);
            timerBadgeRect.sizeDelta = new Vector2(260f, 52f);
            if (timerBadgeObj.GetComponent<Image>() == null)
            {
                Image timerBadge = timerBadgeObj.AddComponent<Image>();
                ApplySlicedSprite(timerBadge, LoadUISprite(UIAccentButton));
                timerBadge.color = UIAccentTint;
            }

            GameObject timerTextObj = GetOrCreateChild(headerObj.transform, "timer_text");
            RectTransform timerTextRect = timerTextObj.GetComponent<RectTransform>();
            timerTextRect.anchorMin = new Vector2(0.5f, 0.0f);
            timerTextRect.anchorMax = new Vector2(0.5f, 0.0f);
            timerTextRect.pivot = new Vector2(0.5f, 1.0f);
            timerTextRect.anchoredPosition = new Vector2(0f, -14f);
            timerTextRect.sizeDelta = new Vector2(260f, 52f);
            if (timerTextObj.GetComponent<Text>() == null)
            {
                Text timerTxt = timerTextObj.AddComponent<Text>();
                timerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                timerTxt.fontSize = titleFontSize;
                timerTxt.fontStyle = FontStyle.Bold;
                timerTxt.color = Color.white;
                timerTxt.text = "00:00";
                timerTxt.alignment = TextAnchor.MiddleCenter;

                Shadow timerShadow = timerTextObj.AddComponent<Shadow>();
                timerShadow.effectColor = new Color(0, 0, 0, 0.8f);
                timerShadow.effectDistance = new Vector2(2, -2);
            }

            GameObject goalsContainer = GetOrCreateChild(headerObj.transform, "Goals_Container");
            topGoalContainer = goalsContainer.GetComponent<RectTransform>();
            topGoalContainer.anchorMin = new Vector2(0.01f, 0.0f);
            topGoalContainer.anchorMax = new Vector2(0.99f, 1f);
            topGoalContainer.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = goalsContainer.GetComponent<HorizontalLayoutGroup>() ?? goalsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = goalContainerSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        /// <summary>
        /// Five square slots in a row. The panel is sized from the slots rather than the other way round,
        /// so the slots stay square whatever the reference resolution is.
        /// </summary>
        private void BuildBottomDockPanel(Transform parent)
        {
            GameObject dockObj = GetOrCreateChild(parent, "Bottom_Dock_Panel");

            int slotCount = DockCapacity;
            float slotSize = EffectiveSlotSize();
            float panelWidth = slotCount * slotSize + (slotCount - 1) * dockSlotSpacing + dockPanelPadding * 2f;
            float panelHeight = slotSize + dockPanelPadding * 2f;

            dockPanelRect = dockObj.GetComponent<RectTransform>();
            dockPanelRect.anchorMin = new Vector2(0.5f, 0f);
            dockPanelRect.anchorMax = new Vector2(0.5f, 0f);
            dockPanelRect.pivot = new Vector2(0.5f, 0f);
            dockPanelRect.anchoredPosition = dockPanelAnchoredPosition;
            dockPanelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            Image bg = dockObj.GetComponent<Image>() ?? dockObj.AddComponent<Image>();
            bg.sprite = null;
            bg.color = Color.clear;

            Outline dockOutline = dockObj.GetComponent<Outline>();
            if (dockOutline != null) SafeDestroy(dockOutline);

            GameObject slotsContainerObj = GetOrCreateChild(dockObj.transform, "Slots_Container");
            RectTransform slotsRect = slotsContainerObj.GetComponent<RectTransform>();
            slotsRect.anchorMin = Vector2.zero;
            slotsRect.anchorMax = Vector2.one;
            slotsRect.offsetMin = Vector2.zero;
            slotsRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = slotsContainerObj.GetComponent<HorizontalLayoutGroup>() ?? slotsContainerObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(dockPanelPadding), Mathf.RoundToInt(dockPanelPadding),
                                            Mathf.RoundToInt(dockPanelPadding), Mathf.RoundToInt(dockPanelPadding));
            layout.spacing = dockSlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Slot count is fixed, so any leftovers from an older layout are removed rather than reused.
            foreach (Transform child in slotsContainerObj.GetComponentsInChildren<Transform>(true))
            {
                if (child == slotsContainerObj.transform) continue;
                if (child.parent != slotsContainerObj.transform) continue;
                if (!child.name.StartsWith("DockSlot_")) continue;

                bool parsed = int.TryParse(child.name.Substring("DockSlot_".Length), out int idx);
                if (!parsed || idx < 0 || idx >= slotCount) SafeDestroy(child.gameObject);
            }

            slotRects.Clear();
            slotImages.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                Transform existing = slotsContainerObj.transform.Find($"DockSlot_{i}");
                GameObject slotObj;
                RectTransform slotRect;
                Image slotBg;

                if (existing != null)
                {
                    // Preserve exact manual transforms (rotation, position, scale) set by user in Unity Editor
                    slotObj = existing.gameObject;
                    slotRect = slotObj.GetComponent<RectTransform>();
                    slotBg = slotObj.GetComponent<Image>() ?? slotObj.AddComponent<Image>();
                }
                else
                {
                    slotObj = GetOrCreateChild(slotsContainerObj.transform, $"DockSlot_{i}");
                    slotRect = slotObj.GetComponent<RectTransform>();
                    slotRect.sizeDelta = new Vector2(slotSize, slotSize);
                    slotRect.localRotation = Quaternion.Euler(55f, 0f, -18f);

                    slotBg = slotObj.GetComponent<Image>() ?? slotObj.AddComponent<Image>();
                }

                if (slotBg != null)
                {
                    ApplySlicedSprite(slotBg, LoadUISprite("Buttons/Button Green"));
                    slotBg.color = dockSlotEmptyColor;
                    slotBg.raycastTarget = false;
                }

                Outline slotOutline = slotObj.GetComponent<Outline>() ?? slotObj.AddComponent<Outline>();
                slotOutline.effectColor = new Color(0.18f, 0.48f, 0.08f, 0.95f);
                slotOutline.effectDistance = new Vector2(1.5f, -1.5f);

                slotObj.transform.SetSiblingIndex(i);

                DestroyChildIfExists(slotObj.transform, "SelectionBadge");
                DestroyChildIfExists(slotObj.transform, "LabelText");

                Button staleBtn = slotObj.GetComponent<Button>();
                if (staleBtn != null) SafeDestroy(staleBtn);

                if (slotRect != null) slotRects.Add(slotRect);
                if (slotBg != null) slotImages.Add(slotBg);
            }

            UpdateSlotVisuals();
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
                btnObj = NewUIObject("Shuffle_Button", parent);

                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchorMin = Vector2.zero;
                btnRect.anchorMax = Vector2.zero;
                btnRect.pivot = Vector2.zero;
                btnRect.anchoredPosition = shuffleButtonPosition;
                btnRect.sizeDelta = shuffleButtonSize;

                Image btnBg = btnObj.AddComponent<Image>();
                ApplySlicedSprite(btnBg, LoadUISprite(UIAccentSquareButton));
                btnBg.color = UIAccentTint;

                GameObject btnIconObj = NewUIObject("Icon", btnObj.transform);

                RectTransform btnIconRect = btnIconObj.GetComponent<RectTransform>();
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
                return;
            }

            foreach (FindTargetObject item in Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None))
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

        private void RemoveTrashButton(Transform parent)
        {
            Transform existingBtn = parent.Find("Trash_Button");
            if (existingBtn != null)
            {
                SafeDestroy(existingBtn.gameObject);
            }
        }

        /// <summary>
        /// Sends the LEFTMOST group in the tray - the type sitting in slot 0 - back into the pile as loose
        /// physics objects. Only that one group leaves; everything behind it slides left into the freed
        /// slots the same way a delivered group's departure already slides the rest of the tray.
        ///
        /// The leftmost group is always a single contiguous run: <see cref="GetInsertIndexForType"/> only
        /// ever inserts a new item right after the last item of its own kind, so one type can never end up
        /// split around another.
        /// </summary>
        public void OnTrashButtonClicked()
        {
            if (dockItems.Count == 0 || gameOverTriggered) return;

            string leftoverType = dockItems[0]?.colorName;
            if (string.IsNullOrEmpty(leftoverType)) return;

            var group = new List<DockItemData>();
            while (dockItems.Count > 0 && dockItems[0] != null &&
                   leftoverType.Equals(dockItems[0].colorName, System.StringComparison.OrdinalIgnoreCase))
            {
                group.Add(dockItems[0]);
                dockItems.RemoveAt(0);
            }

            UpdateSlotVisuals();
            // The group leaving means that order wants MORE again - RemainingForOrder reads straight off
            // CountInDock, so this alone puts the card's count back up without any extra bookkeeping.
            RefreshOrderCardCounts(leftoverType);

            GameObject trashBtn = mainCanvas != null ? mainCanvas.transform.Find("Trash_Button")?.gameObject : GameObject.Find("Trash_Button");
            if (trashBtn != null)
            {
                trashBtn.transform.DOKill(true);
                trashBtn.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 5, 0.5f);
            }

            for (int i = 0; i < group.Count; i++)
            {
                DockItemData data = group[i];
                if (data?.targetObject == null) continue;

                GameObject itemObj = data.targetObject.gameObject;
                itemObj.transform.SetParent(null, true);

                Rigidbody rb = itemObj.GetComponent<Rigidbody>();
                if (rb == null) rb = itemObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = true;

                foreach (Collider c in itemObj.GetComponentsInChildren<Collider>())
                {
                    if (c == null) continue;
                    c.enabled = true;
                    c.isTrigger = false;
                }

                Vector3 pileScale = Vector3.one;
                if (data.targetObject.OriginalScale != Vector3.zero) pileScale = data.targetObject.OriginalScale;
                data.targetObject.isDocked = false;

                Vector3 returnPos = new Vector3(Random.Range(-1.3f, 1.3f), Random.Range(0.2f, 0.6f), Random.Range(-1.3f, 1.3f));
                Quaternion returnRot = Random.rotation;

                float delay = i * 0.06f;
                itemObj.transform.DOKill();
                tweeningDockObjects.Add(itemObj);

                Sequence returnSeq = DOTween.Sequence();
                returnSeq.AppendInterval(delay);
                returnSeq.Append(itemObj.transform.DOJump(returnPos, 0.60f, 1, 0.40f).SetEase(Ease.OutQuad));
                returnSeq.Join(itemObj.transform.DOScale(pileScale, 0.40f).SetEase(Ease.OutQuad));
                returnSeq.Join(itemObj.transform.DORotateQuaternion(returnRot, 0.40f).SetEase(Ease.OutQuad));
                returnSeq.OnComplete(() =>
                {
                    tweeningDockObjects.Remove(itemObj);
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.WakeUp();
                    }
                });
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Order cards
        // ---------------------------------------------------------------------------------------------

        /// <summary>Tears every order card down and rebuilds the row from scratch - used on level load.</summary>
        public void RefreshTargetGoalsUI()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                GameObject levelTextObj = GameObject.Find("Level_Text");
                if (levelTextObj != null)
                {
                    Text titleTxt = levelTextObj.GetComponent<Text>();
                    if (titleTxt != null) titleTxt.text = LevelManager.Instance.ActiveLevelData.levelTitle.ToUpperInvariant();
                }
            }

            if (topGoalContainer == null) return;

            // Detached from the container BEFORE being destroyed. In Play, Destroy() only takes effect at
            // the end of the frame, so a card rebuilt further down this same call would otherwise still
            // find the old one as "existing" and skip itself.
            var staleCards = new List<Transform>();
            foreach (Transform child in topGoalContainer) staleCards.Add(child);
            foreach (Transform child in staleCards)
            {
                KillCardTweens(child);
                child.SetParent(null, false);
                SafeDestroy(child.gameObject);
            }

            SyncOrderCards();
        }

        /// <summary>Adds cards for new customers and clears out cards whose order has already left.</summary>
        private void SyncOrderCards()
        {
            if (topGoalContainer == null || CustomerOrderManager.Instance == null) return;

            IReadOnlyList<CustomerOrder> orders = CustomerOrderManager.Instance.ActiveOrders;

            var live = new HashSet<int>();
            foreach (CustomerOrder order in orders)
            {
                if (order != null && !order.isCompleted) live.Add(order.orderId);
            }

            var toRemove = new List<Transform>();
            foreach (Transform child in topGoalContainer)
            {
                if (!child.name.StartsWith(OrderCardPrefix)) continue; // retiring cards own their own removal
                if (!live.Contains(GetCardOrderId(child))) toRemove.Add(child);
            }
            foreach (Transform child in toRemove)
            {
                KillCardTweens(child);
                child.SetParent(null, false);
                SafeDestroy(child.gameObject);
            }

            for (int i = 0; i < orders.Count; i++)
            {
                CustomerOrder order = orders[i];
                if (order == null || order.isCompleted) continue;
                if (FindOrderCard(order.orderId) != null) continue;

                GameObject card = BuildOrderCard(order, i);
                card.transform.SetSiblingIndex(Mathf.Min(i, topGoalContainer.childCount - 1));
            }
        }

        /// <summary>
        /// Kills every tween a card owns before it is destroyed.
        ///
        /// `card.DOKill()` only kills tweens whose TARGET is the transform. The spawn fade targets the
        /// CanvasGroup and the tick/hit flashes target the Image, so those survived the card and then
        /// spammed "the object has been destroyed but you are still trying to access it".
        /// </summary>
        private static void KillCardTweens(Transform card)
        {
            if (card == null) return;

            card.DOKill();
            KillCardGraphicTweens(card);
        }

        /// <summary>
        /// The graphic half of <see cref="KillCardTweens"/>, leaving transform-targeted tweens alone. Used
        /// from inside a card's own removal sequence, which must not kill itself while it is completing.
        /// </summary>
        private static void KillCardGraphicTweens(Transform card)
        {
            if (card == null) return;

            foreach (Component c in card.GetComponentsInChildren<Component>(true))
            {
                if (c is CanvasGroup || c is Graphic) DOTween.Kill(c);
            }
        }

        private static int GetCardOrderId(Transform card)
        {
            // Card names are "GoalCard_{orderId}_{itemId}".
            string[] parts = card.name.Split('_');
            if (parts.Length < 2) return -1;
            return int.TryParse(parts[1], out int id) ? id : -1;
        }

        private Transform FindOrderCard(int orderId)
        {
            if (topGoalContainer == null) return null;
            foreach (Transform child in topGoalContainer)
            {
                if (!child.name.StartsWith(OrderCardPrefix)) continue;
                if (GetCardOrderId(child) == orderId) return child;
            }
            return null;
        }

        /// <summary>Finds the level-goal entry or ItemDataSO asset for an item id, to reuse its 3D display prefab for the order-card icon.</summary>
        private GameObject FindDisplayPrefabForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null)
            {
                foreach (MatchGoal g in MatchGoalManager.Instance.levelGoals)
                {
                    if (g != null && g.colorName != null && g.colorName.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (g.displayPrefab != null) return g.displayPrefab;
                    }
                }
            }

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                if (levelData != null && levelData.targetGoals != null)
                {
                    foreach (var goal in levelData.targetGoals)
                    {
                        if (goal != null && goal.itemData != null)
                        {
                            if (goal.itemData.GetEffectiveItemId().Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                            {
                                if (goal.itemData.prefab != null) return goal.itemData.prefab;
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDataSO");
            if (guids != null)
            {
                foreach (string g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    ItemDataSO itemSO = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
                    if (itemSO != null && itemSO.GetEffectiveItemId().Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (itemSO.prefab != null) return itemSO.prefab;
                    }
                }
            }
#endif

            return null;
        }

        private GameObject BuildOrderCard(CustomerOrder order, int spawnIndex)
        {
            GameObject cardObj = NewUIObject($"{OrderCardPrefix}{order.orderId}_{order.itemId}", topGoalContainer);

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.sizeDelta = goalCardSize;

            LayoutElement le = cardObj.GetComponent<LayoutElement>() ?? cardObj.AddComponent<LayoutElement>();
            le.preferredWidth = goalCardSize.x;
            le.preferredHeight = goalCardSize.y;
            le.minWidth = goalCardSize.x;
            le.minHeight = goalCardSize.y;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            Image cardBg = cardObj.AddComponent<Image>();
            ApplySlicedSprite(cardBg, LoadUISprite("Buttons/Button Green"));
            cardBg.color = Color.white;

            Outline cardOutline = cardObj.GetComponent<Outline>() ?? cardObj.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.18f, 0.48f, 0.08f, 0.95f);
            cardOutline.effectDistance = new Vector2(2.5f, -2.5f);

            GameObject iconObj = NewUIObject("Icon", cardObj.transform);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.20f);
            iconRect.anchorMax = new Vector2(0.98f, 0.98f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = Vector2.zero;

            BuildOrderCardIcon(iconObj, order);

            GameObject textObj = NewUIObject("Text", cardObj.transform);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.02f);
            textRect.anchorMax = new Vector2(1f, 0.26f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = goalCardFontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = $"{RemainingForOrder(order)}";

            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(2, -2);

            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.9f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);

            if (Application.isPlaying)
            {
                cardObj.transform.localScale = Vector3.zero;
                CanvasGroup cg = cardObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                Sequence spawnSeq = DOTween.Sequence();
                spawnSeq.AppendInterval(spawnIndex * goalSpawnStaggerDelay);
                spawnSeq.Append(cardObj.transform.DOScale(Vector3.one, goalSpawnScaleDuration).SetEase(Ease.OutBack, 1.6f));
                spawnSeq.Join(cg.DOFade(1f, goalSpawnFadeDuration));
                spawnSeq.Play();
            }
            else
            {
                cardObj.transform.localScale = Vector3.one;
            }

            return cardObj;
        }

        private void BuildOrderCardIcon(GameObject iconObj, CustomerOrder order)
        {
            GameObject displayPrefab = FindDisplayPrefabForItem(order.itemId);

            if (displayPrefab == null)
            {
                Image iconImg = iconObj.AddComponent<Image>();
                Sprite foodIcon = order.itemIcon != null ? order.itemIcon
                                : (string.IsNullOrEmpty(order.itemId) ? null : IconSprite(order.itemId));
                if (foodIcon != null)
                {
                    iconImg.sprite = foodIcon;
                    iconImg.type = Image.Type.Simple;
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                }
                else
                {
                    ApplySlicedSprite(iconImg, LoadUISprite(UIAccentSquareButton));
                    iconImg.color = new Color(order.itemColor.r, order.itemColor.g, order.itemColor.b, 0.65f);
                }
                return;
            }

            Quaternion modelRotation = Quaternion.Euler(goalCard3DModelTiltX, -25f, 0f);
            string itemIdLower = order.itemId != null ? order.itemId.ToLowerInvariant() : "";
            if (itemIdLower.Equals("watermelon_001"))
            {
                // Half watermelon (round red cut face facing front)
                modelRotation = Quaternion.Euler(-75f, 0f, 0f);
            }
            else if (itemIdLower.Contains("watermelon"))
            {
                // Triangular watermelon slice (watermelon_002 / watermelon_003): tilted diagonally at ~40 deg (matching Image 2)
                modelRotation = Quaternion.Euler(15f, -30f, 40f);
            }
            else if (itemIdLower.Contains("fish"))
            {
                modelRotation = Quaternion.Euler(15f, -45f, 10f);
            }

            GameObject modelWrapper = new GameObject("3D_Icon_Wrapper");
            modelWrapper.transform.SetParent(iconObj.transform, false);
            modelWrapper.transform.localPosition = new Vector3(0f, 0f, goalCard3DModelLocalPosition.z);
            modelWrapper.transform.localRotation = modelRotation;
            modelWrapper.transform.localScale = Vector3.one;

            GameObject modelObj = Instantiate(displayPrefab, modelWrapper.transform);
            modelObj.name = "3D_Icon_Model";
            modelObj.transform.localPosition = Vector3.zero;
            modelObj.transform.localRotation = Quaternion.identity;
            modelObj.transform.localScale = Vector3.one;

            foreach (var c in modelObj.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
            foreach (var c in modelObj.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(c);
            foreach (var c in modelObj.GetComponentsInChildren<MonoBehaviour>(true)) SafeDestroy(c);

            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in modelWrapper.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = uiLayer;

            Renderer[] renderers = modelObj.GetComponentsInChildren<Renderer>(true);
            Bounds combinedBounds = new Bounds();
            bool hasBounds = false;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!hasBounds) { combinedBounds = r.bounds; hasBounds = true; }
                else combinedBounds.Encapsulate(r.bounds);
            }

            if (hasBounds)
            {
                Vector3 localCenterOffset = modelWrapper.transform.InverseTransformPoint(combinedBounds.center);
                Vector3 worldSize = combinedBounds.size;
                float maxWorldDim = Mathf.Max(worldSize.x, worldSize.y, worldSize.z);

                float worldUnitInUIPixels = modelWrapper.transform.lossyScale.x;
                float rawMeshSizeInUIPixels = (worldUnitInUIPixels > 0.00001f) ? (maxWorldDim / worldUnitInUIPixels) : maxWorldDim;

                float effectiveTargetSize = Mathf.Max(150f, goalCard3DModelTargetSize);
                float scaleFactor = (rawMeshSizeInUIPixels > 0.0001f) ? (effectiveTargetSize / rawMeshSizeInUIPixels) : 1f;

                modelObj.transform.localScale = Vector3.one * scaleFactor;
                modelObj.transform.localPosition = -localCenterOffset * scaleFactor;
            }
            else
            {
                modelObj.transform.localScale = Vector3.one * goalCard3DModelScale;
            }

            // Fixed orientation - UIRotator is NOT added to keep objects static from a single view direction
        }

        /// <summary>
        /// True for the one order that actually claims items sitting in the tray right now: whichever
        /// not-yet-completed order for this item type is FIRST in the active row. That is exactly
        /// <see cref="CustomerOrderManager.FindOrderForItem"/>'s own pick, so it stays in lockstep with which
        /// order <see cref="TryDeliverGroup"/> will actually deliver to.
        /// </summary>
        private bool IsActiveOrderForItem(CustomerOrder order)
        {
            if (order == null || CustomerOrderManager.Instance == null) return false;
            CustomerOrder active = CustomerOrderManager.Instance.FindOrderForItem(order.itemId);
            return active != null && active.orderId == order.orderId;
        }

        /// <summary>
        /// Two customers can both be asking for "cookie" at once, but only ONE of them is being fed by the
        /// tray at a time - the other's count must not move until its turn comes.
        ///
        /// This used to subtract `CountInDock(order.itemId)` for every order sharing that item id, so two
        /// cookie orders both ticked down together the moment cookies landed in the tray, even though only
        /// the older order would ever actually receive them. Only the ACTIVE order for the type now reserves
        /// against the tray; everything behind it in the queue shows its full, untouched count until it
        /// becomes the active one.
        /// </summary>
        private int RemainingForOrder(CustomerOrder order)
        {
            if (order == null) return 0;
            int reserved = IsActiveOrderForItem(order) ? CountInDock(order.itemId) : 0;
            return Mathf.Max(0, order.requiredCount - reserved);
        }

        /// <summary>Updates every card's "still wanted" count, punching the ones that actually changed.</summary>
        private void RefreshOrderCardCounts(string changedItemId)
        {
            if (topGoalContainer == null || CustomerOrderManager.Instance == null) return;

            foreach (CustomerOrder order in CustomerOrderManager.Instance.ActiveOrders)
            {
                if (order == null || order.isCompleted) continue;

                Transform card = FindOrderCard(order.orderId);
                if (card == null) continue;

                Transform textObj = card.Find("Text");
                Text txt = textObj != null ? textObj.GetComponent<Text>() : null;
                if (txt == null) continue;

                string next = $"{RemainingForOrder(order)}";
                bool changed = txt.text != next;
                txt.text = next;

                if (!changed) continue;
                if (!order.itemId.Equals(changedItemId, System.StringComparison.OrdinalIgnoreCase)) continue;

                txt.DOKill();
                txt.DOColor(new Color(1f, 0.92f, 0.25f), 0.12f).OnComplete(() => txt.DOColor(Color.white, 0.3f));
                textObj.DOKill();
                textObj.DOPunchScale(Vector3.one * goalTickTextPunchStrength, goalTickTextPunchDuration, 7, 0.9f);

                card.DOKill();
                card.DOPunchScale(Vector3.one * goalTickCardPunchStrength, goalTickCardPunchDuration, 8, 0.8f);
            }
        }

        /// <summary>The hit an order card takes when a matched group slams into it.</summary>
        private void PunchOrderCard(Transform card)
        {
            if (card == null) return;

            card.DOKill();
            card.DOPunchScale(Vector3.one * (goalTickCardPunchStrength * 1.6f), goalTickCardPunchDuration, 10, 0.9f);

            Image cardBg = card.GetComponent<Image>();
            if (cardBg == null) return;

            Color orig = cardBg.color;
            cardBg.DOKill();
            cardBg.DOColor(new Color(successAccentColor.r, successAccentColor.g, successAccentColor.b, orig.a), 0.10f)
                  .SetEase(Ease.OutQuad)
                  .OnComplete(() => cardBg.DOColor(orig, 0.25f).SetEase(Ease.InQuad));
        }

        /// <summary>
        /// Smoothly flashes the completed order card green, shrinks it away without checkmark icons,
        /// and invokes onComplete when the exit sequence finishes so the replacement card can enter.
        /// </summary>
        private void RetireOrderCard(Transform card, System.Action onComplete = null)
        {
            if (card == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Renamed so SyncOrderCards ignores it while it plays out
            card.name = RetiredCardPrefix + card.name;

            KillCardTweens(card);
            CanvasGroup cg = card.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();

            // 1. Smoothly transition background and border to vibrant success green
            Image cardBg = card.GetComponent<Image>();
            if (cardBg != null)
            {
                cardBg.DOKill();
                cardBg.DOColor(new Color(0.18f, 0.80f, 0.44f, 0.98f), 0.15f).SetEase(Ease.OutQuad);
            }

            Outline cardOutline = card.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.DOKill();
                cardOutline.DOColor(new Color(0.55f, 1.0f, 0.65f, 0.98f), 0.15f).SetEase(Ease.OutQuad);
            }

            // Remove any CheckIcon child if present (no checkmark icon)
            Transform oldCheck = card.Find("CheckIcon");
            if (oldCheck != null) SafeDestroy(oldCheck.gameObject);

            // Update count text to "0"
            Transform textT = card.Find("Text");
            if (textT != null)
            {
                Text t = textT.GetComponent<Text>();
                if (t != null)
                {
                    t.text = "0";
                    t.color = Color.white;
                }
            }

            // 2. Smooth sequence: Success pop -> pause -> smooth shrink & fade exit -> callback
            Sequence removeSeq = DOTween.Sequence();
            removeSeq.Append(card.DOPunchScale(Vector3.one * 0.16f, 0.28f, 6, 0.5f));
            removeSeq.AppendInterval(0.10f);
            removeSeq.Append(card.DOScale(Vector3.zero, 0.32f).SetEase(Ease.InBack, 1.5f));
            removeSeq.Join(cg.DOFade(0f, 0.25f).SetEase(Ease.InQuad));
            removeSeq.OnComplete(() =>
            {
                if (card != null)
                {
                    KillCardGraphicTweens(card);
                    SafeDestroy(card.gameObject);
                }
                onComplete?.Invoke();
            });
            removeSeq.Play();
        }

        /// <summary>Ticks the dedicated Mecha badge under the timer, when the scene still has one.</summary>
        private void SetMechaBadgeTick()
        {
            GameObject mBadgeObj = GameObject.Find("Mecha_Goal_Badge");
            GameObject mTextObj = GameObject.Find("Mecha_Goal_Text");

            MatchGoal mechaGoal = null;
            if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null)
            {
                foreach (var g in MatchGoalManager.Instance.levelGoals)
                {
                    if (g.colorName != null && g.colorName.IndexOf("mecha", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mechaGoal = g;
                        break;
                    }
                }
            }

            if (mTextObj != null)
            {
                Text txt = mTextObj.GetComponent<Text>();
                if (txt != null && mechaGoal != null)
                {
                    txt.text = mechaGoal.IsCompleted ? "MECHA ✓" : $"MECHA x{mechaGoal.Remaining}";
                    txt.color = mechaGoal.IsCompleted ? successAccentColor : mechaAccentColor;
                    mTextObj.transform.DOKill();
                    mTextObj.transform.DOPunchScale(Vector3.one * goalTickTextPunchStrength, goalTickTextPunchDuration, 7, 0.9f);
                }
            }

            if (mBadgeObj != null)
            {
                mBadgeObj.transform.DOKill();
                mBadgeObj.transform.DOPunchScale(Vector3.one * goalTickCardPunchStrength, goalTickCardPunchDuration, 8, 0.8f);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Mecha identification (unchanged gameplay: tap to reveal, tap again to vanish)
        // ---------------------------------------------------------------------------------------------

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
                    name.Contains("bodycollider") || name.Contains("mixamorig") || name.Contains("hullmesh") ||
                    name.Contains("bone") || name.Contains("arm") || name.Contains("leg") || name.Contains("head"))
                {
                    return true;
                }
                if (t.GetComponent<MechaRagdollSpawner>() != null || t.GetComponent<SkinnedMeshRenderer>() != null) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>Called when the mecha is found or vanishes - credits its goal and ticks the badge.</summary>
        public void OnMechaVanished()
        {
            if (MatchGoalManager.Instance != null)
            {
                MatchGoalManager.Instance.RegisterMatchedItem(ObjectShapeType.Cube, "Mecha", 1);
            }
            SetMechaBadgeTick();
        }

        // ---------------------------------------------------------------------------------------------
        // Collecting into the dock
        // ---------------------------------------------------------------------------------------------

        public bool TryCollectItemToDock(FindTargetObject item)
        {
            return TryCollectItemToDock(item, null);
        }

        public bool TryCollectItemToDock(FindTargetObject item, Collider hitCollider)
        {
            if (item == null || gameOverTriggered) return false;

            bool isMecha = hitCollider != null
                ? IsHitOnMechaCollider(item, hitCollider)
                : item.name.Contains("Mecha") || item.name.Contains("meccha")
                  || (item.colorName != null && item.colorName.Equals("mecha", System.StringComparison.OrdinalIgnoreCase));

            if (isMecha)
            {
                HandleMechaTap(item, hitCollider);
                return false;
            }

            // A host object carrying a hidden mecha cannot be collected until the mecha is tapped off it.
            if (HasChildMecha(item)) return false;

            if (dockItems.Count >= DockCapacity || dockItems.Count >= slotRects.Count) return false;

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
                if (r == null) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            foreach (Collider c in item.GetComponentsInChildren<Collider>())
            {
                if (c != null) c.enabled = false;
            }

            DockItemData data = new DockItemData
            {
                targetObject = item,
                shapeType = item.shapeType,
                colorName = item.colorName,
                objectColor = item.objectColor
            };

            int insertIndex = GetInsertIndexForType(item.colorName);
            dockItems.Insert(insertIndex, data);

            UpdateSlotVisuals();
            RefreshOrderCardCounts(item.colorName);

            string collectedType = item.colorName;
            AnimateItemIntoSlot(item.gameObject, insertIndex, () => EvaluateDockAfterLanding(collectedType));
            return true;
        }

        private void HandleMechaTap(FindTargetObject item, Collider hitCollider)
        {
            MechaRunnerBehavior runner = item.GetComponentInChildren<MechaRunnerBehavior>();
            if (runner == null && hitCollider != null) runner = hitCollider.GetComponentInParent<MechaRunnerBehavior>();

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
                runner.StartRunningMode(item.gameObject);
            }
            else if (runner.currentState == MechaRunnerBehavior.MechaState.RunningInArea)
            {
                runner.VanishAndDisappear();
            }
        }

        /// <summary>
        /// Freshly tapped items slot in sequential arrival order at the end of the dock.
        /// They are NOT artificially re-ordered into the middle of the tray.
        /// </summary>
        private int GetInsertIndexForType(string itemId)
        {
            return dockItems.Count;
        }

        private int CountInDock(string itemId)
        {
            return CountMaxContiguousInDock(itemId);
        }

        private int CountMaxContiguousInDock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int maxStreak = 0;
            int currentStreak = 0;
            for (int i = 0; i < dockItems.Count; i++)
            {
                if (dockItems[i] != null && dockItems[i].colorName != null &&
                    dockItems[i].colorName.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentStreak++;
                    if (currentStreak > maxStreak) maxStreak = currentStreak;
                }
                else
                {
                    currentStreak = 0;
                }
            }
            return maxStreak;
        }

        private void EvaluateDockAfterLanding(string itemId)
        {
            if (TryDeliverGroup(itemId)) return;

            // Only the LAST item to land decides the level is lost. Tapping fast puts several items in the
            // air at once, and the tray is already full while they fly - one of them may still be the piece
            // that completes an order, so judging on the first landing would lose a level that was won.
            if (dockItems.Count >= DockCapacity && !AnyDockItemInFlight()) TriggerGameOver();
        }

        private bool AnyDockItemInFlight()
        {
            foreach (DockItemData data in dockItems)
            {
                if (data?.targetObject == null) continue;
                if (tweeningDockObjects.Contains(data.targetObject.gameObject)) return true;
            }
            return false;
        }

        /// <summary>Re-checks every type in the tray - used after a new customer arrives who may already be satisfied.</summary>
        private void CheckDockForCompletions()
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var types = new List<string>();
            foreach (DockItemData data in dockItems)
            {
                if (data?.colorName == null) continue;
                if (seen.Add(data.colorName)) types.Add(data.colorName);
            }

            foreach (string type in types)
            {
                if (TryDeliverGroup(type)) return;
            }
        }

        /// <summary>
        /// Finds a strictly CONTIGUOUS (side-by-side) group of requiredCount items of itemId.
        /// Returns null if another item type is sitting between them, or if any item in the streak is still landing.
        /// </summary>
        private List<DockItemData> FindContiguousGroupForOrder(string itemId, int requiredCount)
        {
            if (string.IsNullOrEmpty(itemId) || requiredCount <= 0) return null;

            int currentStreak = 0;
            int streakStartIndex = -1;

            for (int i = 0; i < dockItems.Count; i++)
            {
                DockItemData data = dockItems[i];
                if (data != null && data.colorName != null &&
                    data.colorName.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    // If any item in the streak is still flying to its slot, wait until it finishes landing
                    if (data.targetObject != null && tweeningDockObjects.Contains(data.targetObject.gameObject))
                    {
                        return null;
                    }

                    if (currentStreak == 0) streakStartIndex = i;
                    currentStreak++;

                    if (currentStreak >= requiredCount)
                    {
                        var result = new List<DockItemData>();
                        for (int k = streakStartIndex; k < streakStartIndex + requiredCount; k++)
                        {
                            result.Add(dockItems[k]);
                        }
                        return result;
                    }
                }
                else
                {
                    // Different item type sitting in between breaks the contiguous streak!
                    currentStreak = 0;
                    streakStartIndex = -1;
                }
            }

            return null;
        }

        /// <summary>
        /// Launches the tray's group of <paramref name="itemId"/> at the order that wants it, ONLY if the items
        /// are CONTIGUOUS (side-by-side with no other item types in between) AND all items have finished landing.
        /// </summary>
        private bool TryDeliverGroup(string itemId)
        {
            if (CustomerOrderManager.Instance == null) return false;

            CustomerOrder order = CustomerOrderManager.Instance.FindOrderForItem(itemId);
            if (order == null) return false;

            List<DockItemData> group = FindContiguousGroupForOrder(itemId, order.requiredCount);
            if (group == null || group.Count < order.requiredCount) return false;

            foreach (DockItemData data in group) dockItems.Remove(data);
            UpdateSlotVisuals();

            LaunchGroupAtOrderCard(group, order);
            return true;
        }

        private void LaunchGroupAtOrderCard(List<DockItemData> group, CustomerOrder order)
        {
            Transform card = FindOrderCard(order.orderId);
            Vector3 target = card is RectTransform cardRect
                ? GetUIWorldPosition(cardRect)
                : GetUIWorldPosition(topGoalContainer);

            // The dock plane sits a fixed depth in front of a steeply pitched camera, so "up" for this
            // flight is the camera's own up - world +Y would drive the items into the lens.
            Vector3 lift = mainCamera != null ? mainCamera.transform.up * matchLiftDistance : Vector3.up * matchLiftDistance;

            int landed = 0;
            int total = group.Count;

            for (int i = 0; i < group.Count; i++)
            {
                DockItemData data = group[i];
                if (data?.targetObject == null)
                {
                    landed++;
                    continue;
                }

                GameObject obj = data.targetObject.gameObject;
                obj.transform.DOKill();
                tweeningDockObjects.Add(obj);

                float startScale = obj.transform.localScale.x;

                Sequence seq = DOTween.Sequence();
                seq.AppendInterval(i * matchStaggerDelay);
                seq.Append(obj.transform.DOMove(obj.transform.position + lift, matchLiftDuration).SetEase(Ease.OutQuad));
                seq.Join(obj.transform.DOScale(startScale * 1.25f, matchLiftDuration).SetEase(Ease.OutQuad));
                seq.Append(obj.transform.DOMove(target, matchFlightDuration).SetEase(Ease.InBack, 1.15f));
                seq.Join(obj.transform.DORotate(new Vector3(0f, 720f, 0f), matchFlightDuration, RotateMode.FastBeyond360));
                seq.Join(obj.transform.DOScale(startScale * 0.15f, matchFlightDuration).SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    tweeningDockObjects.Remove(obj);
                    PunchOrderCard(card);
                    SafeDestroy(obj);

                    if (++landed >= total) CompleteDeliveredOrder(order, total);
                });
                seq.Play();
            }

            if (total == 0) CompleteDeliveredOrder(order, 0);
        }

        private void CompleteDeliveredOrder(CustomerOrder order, int deliveredCount)
        {
            if (MatchGoalManager.Instance != null && deliveredCount > 0)
            {
                MatchGoalManager.Instance.RegisterMatchedItem(ObjectShapeType.Cube, order.itemId, deliveredCount);
            }

            Transform card = FindOrderCard(order.orderId);

            RetireOrderCard(card, () =>
            {
                if (CustomerOrderManager.Instance != null) CustomerOrderManager.Instance.CompleteOrder(order);

                SyncOrderCards();
                RefreshOrderCardCounts(order.itemId);

                // The customer who just slid in may already be satisfied by items sitting in the tray.
                CheckDockForCompletions();
            });
        }

        private void TriggerGameOver()
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;

            if (dockPanelRect != null)
            {
                dockPanelRect.DOKill(true);
                dockPanelRect.DOShakeAnchorPos(0.5f, new Vector2(18f, 8f), 14, 90f);
            }

            foreach (Image slot in slotImages)
            {
                if (slot == null) continue;
                slot.DOKill();
                slot.DOColor(new Color(0.75f, 0.18f, 0.22f, 0.95f), 0.25f);
            }

            if (MatchGoalManager.Instance != null) MatchGoalManager.Instance.TriggerLose();
        }

        // ---------------------------------------------------------------------------------------------
        // Dock placement
        // ---------------------------------------------------------------------------------------------

        private Camera CanvasEventCamera()
        {
            return (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? mainCanvas.worldCamera : null;
        }

        /// <summary>The point in front of the camera that lines up with a UI rect, at the dock's depth.</summary>
        private Vector3 GetUIWorldPosition(RectTransform rect)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (rect == null || mainCamera == null) return Vector3.zero;

            Vector3 rectWorldCenter = rect.TransformPoint(rect.rect.center);
            Vector2 screenPos2D = RectTransformUtility.WorldToScreenPoint(CanvasEventCamera(), rectWorldCenter);
            return mainCamera.ScreenToWorldPoint(new Vector3(screenPos2D.x, screenPos2D.y, dockCameraDepth));
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slotRects.Count) return Vector3.zero;
            Vector3 pos = GetUIWorldPosition(slotRects[slotIndex]);
            if (mainCamera != null && pos != Vector3.zero)
            {
                pos += mainCamera.transform.up * 0.02f;
            }
            return pos;
        }

        private float ComputeFitScaleForSlot(int slotIndex, GameObject obj3D)
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

            float targetWorldSize = Vector3.Distance(worldEdgeA, worldEdgeB) * dockItemFillRatio;

            float localMax = GetObjectStaticUnscaledMaxExtent(obj3D);
            return localMax > 1e-4f ? targetWorldSize / localMax : targetWorldSize;
        }

        private static readonly Dictionary<int, float> unscaledExtentCache = new Dictionary<int, float>();

        private static float GetObjectStaticUnscaledMaxExtent(GameObject obj)
        {
            if (obj == null) return 1f;

            int instanceId = obj.GetInstanceID();
            if (unscaledExtentCache.TryGetValue(instanceId, out float cachedExtent) && cachedExtent > 1e-4f) return cachedExtent;

            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return 1f;

            Vector3 origScale = obj.transform.localScale;
            Quaternion origRot = obj.transform.rotation;

            obj.transform.localScale = Vector3.one;
            obj.transform.rotation = Quaternion.identity;

            Bounds combined = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].enabled) combined.Encapsulate(rends[i].bounds);
            }

            obj.transform.localScale = origScale;
            obj.transform.rotation = origRot;

            float maxExtent = Mathf.Max(combined.size.x, combined.size.y, combined.size.z);

            // SkinnedMeshRenderers can report 1000+ bounds before the Animator updates on frame 1. An
            // absurd value is not cached, and a safe fallback is returned so the item isn't microscopic.
            if (maxExtent > 500f) return 48f;

            float finalExtent = maxExtent > 1e-4f ? maxExtent : 1f;
            unscaledExtentCache[instanceId] = finalExtent;
            return finalExtent;
        }

        private static Quaternion GetDockItemRotation()
        {
            return Quaternion.Euler(12f, 25f, 0f);
        }

        /// <summary>
        /// The arc into the tray is capped so it never overshoots into the camera: the dock plane sits only
        /// <see cref="dockCameraDepth"/> units in front of a steeply pitched camera, so world +Y points
        /// largely back INTO the lens and every unit of jump power eats view depth.
        /// </summary>
        private float GetDockJumpPower(float maxPower)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null) return maxPower;

            const float maxDepthFractionSpent = 0.25f;

            float depthLostPerUnit = Mathf.Abs(Vector3.Dot(Vector3.up, mainCamera.transform.forward));
            if (depthLostPerUnit < 0.05f) return maxPower;

            return Mathf.Min(maxPower, dockCameraDepth * maxDepthFractionSpent / depthLostPerUnit);
        }

        private void AnimateItemIntoSlot(GameObject obj3D, int slotIndex, System.Action onComplete)
        {
            if (obj3D == null)
            {
                onComplete?.Invoke();
                return;
            }

            // A slot that could not be measured returns Vector3.zero, and jumping there flings the item to
            // the world origin - the middle of the tray. That is what a missing slot looked like on screen,
            // so it is treated as "stay put" instead.
            Vector3 slotWorldPos = GetSlotWorldPosition(slotIndex);
            if (slotWorldPos == Vector3.zero) slotWorldPos = obj3D.transform.position;

            Vector3 targetScale = Vector3.one * ComputeFitScaleForSlot(slotIndex, obj3D);

            tweeningDockObjects.Add(obj3D);
            obj3D.transform.DOKill();

            Sequence seq = DOTween.Sequence();
            seq.Append(obj3D.transform.DOJump(slotWorldPos, GetDockJumpPower(1.15f), 1, collectFlightDuration).SetEase(Ease.OutCubic));
            seq.Join(obj3D.transform.DOScale(targetScale, collectFlightDuration).SetEase(Ease.OutQuad));
            seq.Join(obj3D.transform.DORotateQuaternion(GetDockItemRotation(), collectFlightDuration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);
                if (obj3D != null) obj3D.transform.DOPunchScale(targetScale * 0.18f, 0.20f, 5, 0.5f);
                onComplete?.Invoke();
            });
            seq.Play();
        }

        /// <summary>
        /// Slides docked items to whichever slot they currently occupy. Items shift along whenever a group
        /// forms ahead of them or a delivered group leaves, so their slot is not fixed at collection time.
        /// </summary>
        private void AlignDockItemsWithSlots()
        {
            for (int i = 0; i < dockItems.Count && i < slotRects.Count; i++)
            {
                DockItemData data = dockItems[i];
                if (data?.targetObject == null) continue;

                GameObject obj = data.targetObject.gameObject;
                if (tweeningDockObjects.Contains(obj)) continue;

                Vector3 slotWorldPos = GetSlotWorldPosition(i);
                if (slotWorldPos != Vector3.zero &&
                    (obj.transform.position - slotWorldPos).sqrMagnitude > 0.0000001f)
                {
                    obj.transform.position = Vector3.Lerp(obj.transform.position, slotWorldPos, Time.deltaTime * 18f);
                }

                obj.transform.localScale = Vector3.one * ComputeFitScaleForSlot(i, obj);

                Quaternion targetRot = GetDockItemRotation();
                if (Quaternion.Angle(obj.transform.rotation, targetRot) > 0.05f)
                {
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, targetRot, Time.deltaTime * 15f);
                }
            }
        }

        private void UpdateSlotVisuals()
        {
            for (int i = 0; i < slotImages.Count; i++)
            {
                if (slotImages[i] == null) continue;
                slotImages[i].DOKill();
                slotImages[i].color = i < dockItems.Count ? dockSlotFilledColor : dockSlotEmptyColor;
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Level lifecycle
        // ---------------------------------------------------------------------------------------------

        public void HideAllOverlayPanels()
        {
            if (WinLosePanelController.Instance != null) WinLosePanelController.Instance.HideAll();

            foreach (DockItemData data in dockItems)
            {
                if (data?.targetObject == null) continue;
                data.targetObject.transform.DOKill();
                SafeDestroy(data.targetObject.gameObject);
            }
            dockItems.Clear();
            tweeningDockObjects.Clear();
            gameOverTriggered = false;

            UpdateSlotVisuals();
        }
    }
}
