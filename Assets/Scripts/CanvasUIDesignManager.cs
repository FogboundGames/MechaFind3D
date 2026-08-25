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

        [Header("Order Card 3D Icon")]
        [SerializeField] private float goalCard3DModelTargetSize = 100f;
        [SerializeField] private Vector3 goalCard3DModelLocalPosition = new Vector3(0f, 0f, -25f);
        [SerializeField] private Vector3 goalCard3DModelRotation = new Vector3(15f, -25f, 0f);

        [Header("Order Card 3D Bob Animation")]
        [SerializeField] private float goalCardBobAmplitude = 4f;
        [SerializeField] private float goalCardBobDuration = 1.8f;
        [SerializeField] private float goalCardRotateAmplitude = 12f;
        [SerializeField] private float goalCardRotateDuration = 3.2f;

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
        [SerializeField] private Vector3 dockItemDefaultRotation = new Vector3(0f, 15f, 0f);

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

        [Header("Undo Booster Button (Joker 1)")]
        [SerializeField] private Vector2 undoButtonPosition = new Vector2(160f, 240f);
        [SerializeField] private Vector2 undoButtonSize = new Vector2(80f, 80f);
        [SerializeField] private float undoIconSize = 45f;

        [Header("Mecha Reveal Booster Button (Joker 2)")]
        [SerializeField] private Vector2 revealButtonPosition = new Vector2(160f, 340f);
        [SerializeField] private Vector2 revealButtonSize = new Vector2(80f, 80f);
        [SerializeField] private float revealIconSize = 45f;
        [SerializeField] private Color revealOutlineColor = new Color(0f, 1f, 0.85f, 1f);
        private bool revealOnCooldown = false;

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
        private const string TemplateCardName = "GoalCard_Template";

        private Camera mainCamera;
        private Canvas mainCanvas;
        private RectTransform topGoalContainer;
        private RectTransform dockPanelRect;
        private GameObject orderCardTemplate;

        private readonly List<RectTransform> slotRects = new List<RectTransform>();
        private readonly List<Image> slotImages = new List<Image>();
        private readonly List<Transform> slot3DTransforms = new List<Transform>();

        // Left to right, one entry per occupied slot. Same-type entries are always contiguous because
        // inserts land right after the last item of their own kind.
        private readonly List<DockItemData> dockItems = new List<DockItemData>();

        // Objects whose transform is owned by a tween right now, so the per-frame slot alignment leaves
        // them alone instead of fighting the animation.
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private bool gameOverTriggered;

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private static ItemDataSO[] _itemDataCache;
        private static ItemDataSO[] CachedItemData => _itemDataCache ??= Resources.LoadAll<ItemDataSO>("");

        private static ItemDataSO FindItemDataSO(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (var so in CachedItemData)
            {
                if (so != null && so.GetEffectiveItemId().Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                    return so;
            }
            return null;
        }

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

        private static void MarkTransientIfEditMode(GameObject go)
        {
            if (Application.isPlaying || go == null) return;
            go.hideFlags = HideFlags.DontSave;
            foreach (Transform child in go.transform)
                MarkTransientIfEditMode(child.gameObject);
        }

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            FindExistingUIReferences();
            WireButtons();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild UI (creates missing objects)")]
        public void RebuildUIFromContextMenu()
        {
            EnsureCanvasStructure();
        }
#endif

        private void Start()
        {
            if (orderCardTemplate != null) orderCardTemplate.SetActive(false);
            CleanupLegacyPackagingObjects();
            EnsureEventSystem();
            Setup3DDockSlots();
            StartConveyorDecor();
            EnsureSingleOrderManager();
            StartCoroutine(RefreshAfterLayout());
        }

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

        /// <summary>
        /// Finds existing scene objects by name and caches references. Does NOT create or modify anything.
        /// Ezgi and Emre design the UI by hand in the scene — this code only reads what is already there.
        /// </summary>
        private void FindExistingUIReferences()
        {
            Transform canvasTr = transform.Find("MatchFactory_Canvas");
            if (canvasTr != null)
            {
                mainCanvas = canvasTr.GetComponent<Canvas>();
                if (mainCanvas != null && mainCanvas.worldCamera == null)
                    mainCanvas.worldCamera = mainCamera;

                Transform headerTr = canvasTr.Find("Header_Goal_Panel");
                if (headerTr != null)
                {
                    Transform goalsTr = headerTr.Find("Goals_Container");
                    if (goalsTr != null)
                    {
                        topGoalContainer = goalsTr.GetComponent<RectTransform>();
                        Transform tmpl = goalsTr.Find(TemplateCardName);
                        if (tmpl != null) orderCardTemplate = tmpl.gameObject;
                    }
                }
            }
        }

        private void WireButtons()
        {
            if (mainCanvas == null) return;
            Transform canvasTr = mainCanvas.transform;

            WireButton(canvasTr, "Shuffle_Button", OnShuffleButtonClicked);
            WireButton(canvasTr, "Undo_Booster_Button", OnUndoButtonClicked);
            WireButton(canvasTr, "Reveal_Booster_Button", OnRevealButtonClicked);
        }

        private static void WireButton(Transform parent, string childName, UnityEngine.Events.UnityAction action)
        {
            Transform t = parent.Find(childName);
            if (t == null) return;
            Button btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
            Image bg = t.GetComponent<Image>();
            if (bg != null) btn.targetGraphic = bg;
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
        [MenuItem("Tools/Build Canvas UI Design (creates missing objects)")]
        public static void BuildCanvasUIDesignTool()
        {
            GameObject sceneController = GameObject.Find("Physics_Scene_Controller");
            if (sceneController == null) sceneController = new GameObject("Physics_Scene_Controller");

            CanvasUIDesignManager manager = sceneController.GetComponent<CanvasUIDesignManager>();
            if (manager == null) manager = sceneController.AddComponent<CanvasUIDesignManager>();

            manager.EnsureCanvasStructure();
            Selection.activeGameObject = sceneController;
            Debug.Log("UI yapısı oluşturuldu. Artık sahnede elle düzenleyebilirsiniz.");
        }

        [MenuItem("Tools/Preview Order Cards (Edit Mode)")]
        [ContextMenu("Preview Order Cards")]
        public static void PreviewOrderCardsInEditor()
        {
            var manager = FindFirstObjectByType<CanvasUIDesignManager>();
            if (manager == null) { Debug.LogWarning("CanvasUIDesignManager bulunamadı."); return; }

            manager.FindExistingUIReferences();
            if (manager.topGoalContainer == null) { Debug.LogWarning("Goals_Container bulunamadı."); return; }

            // Clear old previews
            var toRemove = new List<Transform>();
            foreach (Transform child in manager.topGoalContainer)
            {
                if (child.name.StartsWith(OrderCardPrefix) && child.name != TemplateCardName)
                    toRemove.Add(child);
            }
            foreach (var t in toRemove) DestroyImmediate(t.gameObject);

            // Find level data
            var levelMgr = FindFirstObjectByType<LevelManager>();
            LevelDataSO levelData = null;
            if (levelMgr != null) levelData = levelMgr.ActiveLevelData;
            if (levelData == null)
            {
                var allLevels = Resources.FindObjectsOfTypeAll<LevelDataSO>();
                if (allLevels.Length > 0) levelData = allLevels[0];
            }
            if (levelData == null || levelData.targetGoals == null)
            {
                Debug.LogWarning("LevelDataSO bulunamadı — preview kartları oluşturulamıyor.");
                return;
            }

            // Find template
            Transform template = manager.topGoalContainer.Find(TemplateCardName);

            int cardIndex = 0;
            foreach (var goal in levelData.targetGoals)
            {
                if (goal == null || goal.itemData == null) continue;

                string itemId = goal.itemData.GetEffectiveItemId();
                string cardName = $"{OrderCardPrefix}Preview_{cardIndex}_{itemId}";

                GameObject cardObj;
                if (template != null)
                {
                    cardObj = Instantiate(template.gameObject, manager.topGoalContainer, false);
                    cardObj.SetActive(true);
                }
                else
                {
                    cardObj = manager.BuildDefaultOrderCardShell(cardName);
                }
                cardObj.name = cardName;

                // Place 3D icon
                Transform windowTr = cardObj.transform.Find("Inner_Window");
                Transform iconTr = windowTr != null ? windowTr.Find("Icon") : cardObj.transform.Find("Icon");
                if (iconTr != null && goal.itemData.prefab != null)
                {
                    var existingIcon = iconTr.Find("3D_Icon_Wrapper");
                    if (existingIcon != null) DestroyImmediate(existingIcon.gameObject);

                    var existingImg = iconTr.GetComponent<UnityEngine.UI.Image>();
                    if (existingImg != null) existingImg.enabled = false;

                    Quaternion modelRotation = Quaternion.Euler(manager.goalCard3DModelRotation);

                    GameObject wrapper = new GameObject("3D_Icon_Wrapper");
                    wrapper.transform.SetParent(iconTr, false);
                    wrapper.transform.localPosition = new Vector3(0f, 0f, manager.goalCard3DModelLocalPosition.z);
                    wrapper.transform.localRotation = modelRotation;

                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(goal.itemData.prefab);
                    model.name = "3D_Icon_Model";
                    model.transform.SetParent(wrapper.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;

                    foreach (var c in model.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
                    foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
                    foreach (var m in model.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(m);

                    int uiLayer = LayerMask.NameToLayer("UI");
                    foreach (Transform tr in wrapper.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = uiLayer;

                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
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
                        Vector3 localCenterOffset = wrapper.transform.InverseTransformPoint(combinedBounds.center);
                        float maxDim = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
                        float worldUnitInUI = wrapper.transform.lossyScale.x;
                        float rawSize = (worldUnitInUI > 0.00001f) ? (maxDim / worldUnitInUI) : maxDim;
                        float scale = (rawSize > 0.0001f) ? (manager.goalCard3DModelTargetSize / rawSize) : 1f;
                        model.transform.localScale = Vector3.one * scale;
                        model.transform.localPosition = -localCenterOffset * scale;
                    }
                }

                // Set count text
                var countTxt = cardObj.GetComponentInChildren<Text>();
                if (countTxt != null) countTxt.text = $"{goal.requiredCount}";

                cardIndex++;
            }

            // Hide template
            if (template != null) template.gameObject.SetActive(false);

            Debug.Log($"✅ {cardIndex} preview kart oluşturuldu. Sahnede düzenleyebilirsin. Play'e girince gerçek kartlarla değiştirilecek.");
        }
#endif

        // ---------------------------------------------------------------------------------------------
        // Canvas construction — ONLY called from the MenuItem or ContextMenu, never automatically.
        // After running once, design the UI by hand in the scene. Play mode will not touch your layout.
        // ---------------------------------------------------------------------------------------------

        public void EnsureCanvasStructure()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

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

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCamera;
                canvas.planeDistance = uiPlaneDistance;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceResolution;
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            mainCanvas = canvasObj.GetComponent<Canvas>();

            EnsureBackgroundCanvas();
            BuildHeaderOrderPanel(canvasObj.transform);
            BuildBottomDockPanel(canvasObj.transform);
            Setup3DDockSlots();
            BuildShuffleButton(canvasObj.transform);
            BuildUndoBoosterButton(canvasObj.transform);
            BuildRevealBoosterButton(canvasObj.transform);
            RemoveTrashButton(canvasObj.transform);
            Canvas.ForceUpdateCanvases();

            EnsureSingleOrderManager();
            FindExistingUIReferences();

            Debug.Log("UI yapisi olusturuldu/guncellendi. Artik Inspector'dan elle duzenleme yapabilirsiniz.");
        }

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
        private static bool _legacyCleanupDoneThisSession;

        private void CleanupLegacyPackagingObjects()
        {
            if (Application.isPlaying && _legacyCleanupDoneThisSession) return;

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

            if (Application.isPlaying) _legacyCleanupDoneThisSession = true;
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

            Transform bgCamTransform = transform.Find("Background_Camera");
            if (bgCamTransform != null) return;

            GameObject bgCamObj = new GameObject("Background_Camera");
            bgCamObj.transform.SetParent(transform, false);
            Camera bgCam = bgCamObj.AddComponent<Camera>();
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
            Transform existingHeader = parent.Find("Header_Goal_Panel");
            bool headerExisted = existingHeader != null;

            GameObject headerObj = GetOrCreateChild(parent, "Header_Goal_Panel");
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();

            if (!headerExisted)
            {
                headerRect.anchorMin = new Vector2(0.5f, 1f);
                headerRect.anchorMax = new Vector2(0.5f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.anchoredPosition = headerAnchoredPosition;
                headerRect.sizeDelta = headerSize;
            }

            DestroyChildIfExists(headerObj.transform, "Level_Badge");
            DestroyChildIfExists(headerObj.transform, "Level_Text");
            DestroyChildIfExists(headerObj.transform, "Mecha_Goal_Badge");
            DestroyChildIfExists(headerObj.transform, "Mecha_Goal_Text");

            Transform existingTimerBadge = headerObj.transform.Find("timer_badge");
            bool timerExisted = existingTimerBadge != null;

            GameObject timerBadgeObj = GetOrCreateChild(headerObj.transform, "timer_badge");
            RectTransform timerBadgeRect = timerBadgeObj.GetComponent<RectTransform>();
            if (!timerExisted)
            {
                timerBadgeRect.anchorMin = new Vector2(0.5f, 0.0f);
                timerBadgeRect.anchorMax = new Vector2(0.5f, 0.0f);
                timerBadgeRect.pivot = new Vector2(0.5f, 1.0f);
                timerBadgeRect.anchoredPosition = new Vector2(0f, -10f);
                timerBadgeRect.sizeDelta = new Vector2(210f, 44f);

                Image timerBadge = timerBadgeObj.GetComponent<Image>() ?? timerBadgeObj.AddComponent<Image>();
                ApplySlicedSprite(timerBadge, LoadUISprite("Buttons/Button Green"));
                timerBadge.color = new Color(0.20f, 0.25f, 0.32f, 0.98f);

                Outline timerOutline = timerBadgeObj.GetComponent<Outline>() ?? timerBadgeObj.AddComponent<Outline>();
                timerOutline.effectColor = new Color(0.42f, 0.52f, 0.65f, 0.95f);
                timerOutline.effectDistance = new Vector2(2f, -2f);

                Shadow timerDropShadow = timerBadgeObj.GetComponent<Shadow>() ?? timerBadgeObj.AddComponent<Shadow>();
                timerDropShadow.effectColor = new Color(0f, 0f, 0f, 0.40f);
                timerDropShadow.effectDistance = new Vector2(0f, -3f);
            }

            Transform existingTimerText = headerObj.transform.Find("timer_text");
            bool timerTextExisted = existingTimerText != null;

            GameObject timerTextObj = GetOrCreateChild(headerObj.transform, "timer_text");
            RectTransform timerTextRect = timerTextObj.GetComponent<RectTransform>();
            if (!timerTextExisted)
            {
                timerTextRect.anchorMin = new Vector2(0.5f, 0.0f);
                timerTextRect.anchorMax = new Vector2(0.5f, 0.0f);
                timerTextRect.pivot = new Vector2(0.5f, 1.0f);
                timerTextRect.anchoredPosition = new Vector2(0f, -10f);
                timerTextRect.sizeDelta = new Vector2(210f, 44f);

                Text timerTxt = timerTextObj.GetComponent<Text>() ?? timerTextObj.AddComponent<Text>();
                timerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                timerTxt.fontSize = 24;
                timerTxt.fontStyle = FontStyle.Bold;
                timerTxt.color = Color.white;
                timerTxt.text = "⏱️ 00:00";
                timerTxt.alignment = TextAnchor.MiddleCenter;

                Shadow timerTextShadow = timerTextObj.GetComponent<Shadow>() ?? timerTextObj.AddComponent<Shadow>();
                timerTextShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                timerTextShadow.effectDistance = new Vector2(1.5f, -1.5f);
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
            // Remove 2D UI dock panel so only 3D Cube slots in 3D scene space are rendered
            DestroyChildIfExists(parent, "Bottom_Dock_Panel");
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

        private void BuildUndoBoosterButton(Transform parent)
        {
            Transform existingBtn = parent.Find("Undo_Booster_Button");
            GameObject btnObj;
            if (existingBtn != null)
            {
                btnObj = existingBtn.gameObject;
            }
            else
            {
                btnObj = NewUIObject("Undo_Booster_Button", parent);

                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchorMin = Vector2.zero;
                btnRect.anchorMax = Vector2.zero;
                btnRect.pivot = Vector2.zero;
                btnRect.anchoredPosition = undoButtonPosition;
                btnRect.sizeDelta = undoButtonSize;

                Image btnBg = btnObj.AddComponent<Image>();
                ApplySlicedSprite(btnBg, LoadUISprite(UIAccentSquareButton));
                btnBg.color = new Color(0.95f, 0.55f, 0.10f, 1.0f); // Vibrant amber booster button

                GameObject btnIconObj = NewUIObject("Icon", btnObj.transform);

                RectTransform btnIconRect = btnIconObj.GetComponent<RectTransform>();
                btnIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnIconRect.pivot = new Vector2(0.5f, 0.5f);
                btnIconRect.anchoredPosition = Vector2.zero;
                btnIconRect.sizeDelta = new Vector2(undoIconSize, undoIconSize);

                Text iconText = btnIconObj.AddComponent<Text>();
                iconText.text = "↩";
                Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 60);

                iconText.font = defaultFont;
                iconText.fontSize = 54;
                iconText.fontStyle = FontStyle.Bold;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = Color.white;
            }

            Image bg = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
            bg.raycastTarget = true;

            Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnUndoButtonClicked);
        }

        private void BuildRevealBoosterButton(Transform parent)
        {
            Transform existingBtn = parent.Find("Reveal_Booster_Button");
            GameObject btnObj;
            if (existingBtn != null)
            {
                btnObj = existingBtn.gameObject;
            }
            else
            {
                btnObj = NewUIObject("Reveal_Booster_Button", parent);

                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchorMin = Vector2.zero;
                btnRect.anchorMax = Vector2.zero;
                btnRect.pivot = Vector2.zero;
                btnRect.anchoredPosition = revealButtonPosition;
                btnRect.sizeDelta = revealButtonSize;

                Image btnBg = btnObj.AddComponent<Image>();
                ApplySlicedSprite(btnBg, LoadUISprite(UIAccentSquareButton));
                btnBg.color = new Color(0.10f, 0.80f, 0.85f, 1.0f);

                GameObject btnIconObj = NewUIObject("Icon", btnObj.transform);

                RectTransform btnIconRect = btnIconObj.GetComponent<RectTransform>();
                btnIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnIconRect.pivot = new Vector2(0.5f, 0.5f);
                btnIconRect.anchoredPosition = Vector2.zero;
                btnIconRect.sizeDelta = new Vector2(revealIconSize, revealIconSize);

                Text iconText = btnIconObj.AddComponent<Text>();
                iconText.text = "👁";
                Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 60);

                iconText.font = defaultFont;
                iconText.fontSize = 48;
                iconText.fontStyle = FontStyle.Bold;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = Color.white;
            }

            Image bg = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
            bg.raycastTarget = true;

            Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnRevealButtonClicked);
        }

        public void OnRevealButtonClicked()
        {
            Transform btnObj = transform.Find("Reveal_Booster_Button");
            if (btnObj == null && transform.parent != null) btnObj = transform.parent.Find("Reveal_Booster_Button");

            if (revealOnCooldown || gameOverTriggered)
            {
                if (btnObj != null)
                {
                    btnObj.DOKill();
                    btnObj.DOShakeRotation(0.3f, new Vector3(0, 0, 15f), 15, 90f);
                }
                return;
            }

            if (btnObj != null)
            {
                btnObj.DOKill();
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 5, 0.5f);
            }

            MechaRunnerBehavior[] mechas = FindObjectsByType<MechaRunnerBehavior>(FindObjectsSortMode.None);
            if (mechas == null || mechas.Length == 0) return;

            revealOnCooldown = true;

            foreach (MechaRunnerBehavior mecha in mechas)
            {
                if (mecha == null || mecha.gameObject == null) continue;
                if (mecha.currentState == MechaRunnerBehavior.MechaState.Vanishing) continue;

                MechaOutlineReveal reveal = mecha.GetComponent<MechaOutlineReveal>();
                if (reveal == null) reveal = mecha.gameObject.AddComponent<MechaOutlineReveal>();
                reveal.ShowOutline(revealOutlineColor);
            }

            HapticHelper.Vibrate();

            if (btnObj != null)
            {
                Image btnBg = btnObj.GetComponent<Image>();
                if (btnBg != null)
                {
                    Color dimColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                    btnBg.DOColor(dimColor, 0.3f).SetUpdate(true);
                }
            }
        }

        /// <summary>
        /// Joker 1: Pops the last collected item out of the dock slot and returns it safely to the play area boundary.
        /// Restores colliders, shadows, physics, and its original 3D world scale.
        /// </summary>
        public void OnUndoButtonClicked()
        {
            Transform btnObj = transform.Find("Undo_Booster_Button");
            if (btnObj == null && transform.parent != null) btnObj = transform.parent.Find("Undo_Booster_Button");

            if (gameOverTriggered || dockItems == null || dockItems.Count == 0)
            {
                if (btnObj != null)
                {
                    btnObj.DOKill();
                    btnObj.DOShakeRotation(0.3f, new Vector3(0, 0, 15f), 15, 90f);
                }
                return;
            }

            if (btnObj != null)
            {
                btnObj.DOKill();
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 5, 0.5f);
            }

            int lastIdx = dockItems.Count - 1;
            DockItemData lastData = dockItems[lastIdx];
            dockItems.RemoveAt(lastIdx);

            if (lastData != null && lastData.targetObject != null)
            {
                FindTargetObject item = lastData.targetObject;
                item.isDocked = false;
                item.SetYellowOutlineActive(false);

                // Re-enable colliders
                foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
                {
                    if (c != null) c.enabled = true;
                }

                // Re-enable shadows
                foreach (Renderer r in item.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }

                // Calculate safe target position inside boundary tray area
                Vector3 targetPos;
                if (PhysicsObjectSpawner.Instance != null)
                {
                    Vector3 spawnCenter = PhysicsObjectSpawner.Instance.transform.position;
                    Vector2 areaSize = PhysicsObjectSpawner.Instance.SpawnAreaSize;
                    float rx = Random.Range(-areaSize.x * 0.30f, areaSize.x * 0.30f);
                    float rz = Random.Range(-areaSize.y * 0.30f, areaSize.y * 0.30f);
                    float ry = Random.Range(PhysicsObjectSpawner.Instance.SpawnHeightMin + 0.1f, PhysicsObjectSpawner.Instance.SpawnHeightMax + 0.3f);
                    targetPos = spawnCenter + new Vector3(rx, ry, rz);
                }
                else
                {
                    targetPos = (lastData.originalPosition != Vector3.zero) ? lastData.originalPosition : item.transform.position + Vector3.up * 0.5f;
                }

                Vector3 targetScale = (lastData.originalWorldScale != Vector3.zero) ? lastData.originalWorldScale : Vector3.one;

                // Animate object from dock slot back into boundary scene area
                item.transform.DOKill();
                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                Sequence seq = DOTween.Sequence();
                seq.Join(item.transform.DOMove(targetPos, 0.40f).SetEase(Ease.OutQuad));
                seq.Join(item.transform.DOScale(targetScale, 0.40f).SetEase(Ease.OutQuad));
                seq.Join(item.transform.DORotateQuaternion(Random.rotation, 0.40f).SetEase(Ease.OutQuad));
                seq.OnComplete(() =>
                {
                    if (item != null && rb != null && !item.isDocked)
                    {
                        rb.isKinematic = false;
                        rb.WakeUp();
                    }
                });

                // Touch ripple feedback
                if (VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayTouchRippleVFX(targetPos);
                }

                UpdateSlotVisuals();
                RefreshOrderCardCounts(item.colorName);
            }
            else
            {
                UpdateSlotVisuals();
            }
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
            if (dockItems.Count == 0 || gameOverTriggered)
            {
                return;
            }

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

        /// <summary>Destroys every order card so the next refresh builds them from scratch.</summary>
        public void ClearAllOrderCards()
        {
            if (topGoalContainer == null) return;
            var toRemove = new List<Transform>();
            foreach (Transform child in topGoalContainer)
            {
                if (child.name == TemplateCardName) continue;
                if (child.name.StartsWith(OrderCardPrefix) || child.name.StartsWith(RetiredCardPrefix))
                    toRemove.Add(child);
            }
            foreach (Transform child in toRemove)
            {
                KillCardTweens(child);
                child.SetParent(null, false);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

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

            ClearAllOrderCards();
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

            var seenOrderIds = new HashSet<int>();
            var toRemove = new List<Transform>();
            foreach (Transform child in topGoalContainer)
            {
                if (child.name == TemplateCardName) continue;
                if (!child.name.StartsWith(OrderCardPrefix)) continue;
                int orderId = GetCardOrderId(child);
                bool duplicate = !seenOrderIds.Add(orderId);
                if (duplicate || !live.Contains(orderId)) toRemove.Add(child);
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

                // Always runs, even for a card that already matches this order: BuildOrderCard only
                // constructs the shell for a genuinely new card, but it (and BuildOrderCardIcon) still need
                // to run every sync to populate/refresh the order's icon and remaining-count text - which is
                // the part a hand-authored scene card never has to begin with.
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
                if (child.name == TemplateCardName) continue;
                if (!child.name.StartsWith(OrderCardPrefix)) continue;
                if (GetCardOrderId(child) == orderId) return child;
            }
            return null;
        }

        private string NormalizeItemName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string name = raw.ToLowerInvariant().Replace("(clone)", "").Trim();

            string[] prefixes = { "sm_food_", "sm_item_", "sm_", "food_", "item_", "pf_", "prefab_", "levelobj_" };
            foreach (var p in prefixes)
            {
                if (name.StartsWith(p))
                {
                    name = name.Substring(p.Length);
                    break;
                }
            }

            int underscoreIdx = name.LastIndexOf('_');
            if (underscoreIdx > 0)
            {
                string suffix = name.Substring(underscoreIdx + 1);
                if (int.TryParse(suffix, out _) || suffix.Length <= 2)
                {
                    name = name.Substring(0, underscoreIdx);
                }
            }

            return name.Trim();
        }

        /// <summary>Finds the level-goal entry or ItemDataSO asset for an item id, to reuse its 3D display prefab for the order-card icon.</summary>
        public GameObject FindDisplayPrefabForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            string targetNorm = NormalizeItemName(itemId);

            // 1. Check ActiveLevelData target goals & filler items
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                if (levelData != null)
                {
                    if (levelData.targetGoals != null)
                    {
                        foreach (var goal in levelData.targetGoals)
                        {
                            if (goal != null && goal.itemData != null)
                            {
                                string gId = goal.itemData.GetEffectiveItemId();
                                if (gId.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    if (goal.itemData.prefab != null) return goal.itemData.prefab;
                                }
                            }
                        }
                    }

                    if (levelData.fillerItems != null)
                    {
                        foreach (var filler in levelData.fillerItems)
                        {
                            if (filler != null)
                            {
                                string fId = filler.GetEffectiveItemId();
                                if (fId.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    if (filler.prefab != null) return filler.prefab;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Check active MatchGoalManager level goals
            if (MatchGoalManager.Instance != null && MatchGoalManager.Instance.levelGoals != null)
            {
                foreach (MatchGoal g in MatchGoalManager.Instance.levelGoals)
                {
                    if (g != null && !string.IsNullOrEmpty(g.colorName))
                    {
                        if (g.colorName.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (g.displayPrefab != null) return g.displayPrefab;
                        }
                    }
                }
            }

            // 3. Check PhysicsObjectSpawner foodModels
            PhysicsObjectSpawner spawner = Object.FindFirstObjectByType<PhysicsObjectSpawner>();
            if (spawner != null && spawner.foodModels != null)
            {
                foreach (GameObject model in spawner.foodModels)
                {
                    if (model == null) continue;
                    if (model.name.Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return model;
                    }
                }
            }

            // 4. Check all ItemDataSO assets in project / Resources by exact itemId
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDataSO");
            if (guids != null)
            {
                foreach (string g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    ItemDataSO itemSO = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
                    if (itemSO != null)
                    {
                        if (itemSO.GetEffectiveItemId().Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (itemSO.prefab != null) return itemSO.prefab;
                        }
                    }
                }
            }
#endif
            foreach (var itemSO in CachedItemData)
            {
                if (itemSO != null && itemSO.GetEffectiveItemId().Equals(itemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (itemSO.prefab != null) return itemSO.prefab;
                }
            }

            // 5. Normalized fallback matching for LevelData target goals & filler items
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                if (levelData != null)
                {
                    if (levelData.targetGoals != null)
                    {
                        foreach (var goal in levelData.targetGoals)
                        {
                            if (goal != null && goal.itemData != null)
                            {
                                string gNorm = NormalizeItemName(goal.itemData.GetEffectiveItemId());
                                if (gNorm.Equals(targetNorm, System.StringComparison.OrdinalIgnoreCase) ||
                                    gNorm.Contains(targetNorm) || targetNorm.Contains(gNorm))
                                {
                                    if (goal.itemData.prefab != null) return goal.itemData.prefab;
                                }
                            }
                        }
                    }
                }
            }

            // 6. Check PhysicsObjectSpawner foodModels by normalized name
            if (spawner != null && spawner.foodModels != null)
            {
                foreach (GameObject model in spawner.foodModels)
                {
                    if (model == null) continue;
                    string mNorm = NormalizeItemName(model.name);
                    if (mNorm.Equals(targetNorm, System.StringComparison.OrdinalIgnoreCase) ||
                        mNorm.Contains(targetNorm) || targetNorm.Contains(mNorm))
                    {
                        return model;
                    }
                }
            }

            return null;
        }

        private GameObject BuildOrderCard(CustomerOrder order, int spawnIndex)
        {
            string cardName = $"{OrderCardPrefix}{order.orderId}_{order.itemId}";

            // Matched by orderId alone (same lookup FindOrderCard uses elsewhere), not the full name.
            // CustomerOrderManager re-rolls which item occupies each order slot every time
            // SetupCustomerOrders() runs (and it runs more than once during startup), while orderId itself
            // is always renumbered 1..N the same way each time. Matching on the full "orderId_itemId" name
            // meant a slot whose item changed between rolls silently spawned a second card and never
            // cleaned up the first - the "too many cards" bug. Matching on orderId treats it as the same
            // slot and just renames/updates it in place.
            Transform existingCard = FindOrderCard(order.orderId);
            bool cardExisted = existingCard != null;

            GameObject cardObj;
            if (cardExisted)
            {
                cardObj = existingCard.gameObject;
                cardObj.name = cardName;
            }
            else
            {
                Transform template = FindOrderCardTemplate();
                if (template != null)
                {
                    cardObj = Instantiate(template.gameObject, topGoalContainer, false);
                    cardObj.SetActive(true);
                    cardObj.name = cardName;
                    MarkTransientIfEditMode(cardObj);
                }
                else
                {
                    Debug.LogWarning("[OrderCard] GoalCard_Template bulunamadı — Goals_Container altına sahnede bir template kart ekleyin.");
                    cardObj = BuildDefaultOrderCardShell(cardName);
                }
            }

            // The spawn animation (below) drives localScale from zero to one. A card cloned from
            // a template that is mid-animation inherits that zero scale, which collapses
            // Renderer.bounds to a zero-size point and makes InverseTransformPoint return NaN —
            // so BuildOrderCardIcon's 3D model ends up at NaN position and is invisible.
            // Resetting to one here ensures bounds math is valid; the spawn animation re-zeroes
            // it right after, so there is no visual glitch.
            cardObj.transform.localScale = Vector3.one;

            // Card background color comes from the template — not overwritten here.
            Transform windowTr = cardObj.transform.Find("Inner_Window");
            Transform iconTr = windowTr != null ? windowTr.Find("Icon") : cardObj.transform.Find("Icon");
            if (iconTr != null)
            {
                BuildOrderCardIcon(iconTr.gameObject, order);
            }

            Text countTxt = cardObj.GetComponentInChildren<Text>();
            if (countTxt != null)
            {
                int remCount = RemainingForOrder(order);
                countTxt.text = remCount > 0 ? $"{remCount}" : "✓";
            }

            if (Application.isPlaying && !cardExisted)
            {
                cardObj.transform.localScale = Vector3.zero;
                CanvasGroup cg = cardObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                Sequence spawnSeq = DOTween.Sequence();
                spawnSeq.AppendInterval(spawnIndex * goalSpawnStaggerDelay);
                spawnSeq.Append(cardObj.transform.DOScale(Vector3.one, goalSpawnScaleDuration).SetEase(Ease.OutBack, 1.6f));
                spawnSeq.Join(cg.DOFade(1f, goalSpawnFadeDuration));
                spawnSeq.Play();
            }
            else if (!cardExisted)
            {
                cardObj.transform.localScale = Vector3.one;
            }

            return cardObj;
        }

        /// <summary>Any already-built order card in the container, used as the visual template for a new one.</summary>
        private Transform FindOrderCardTemplate()
        {
            if (orderCardTemplate != null) return orderCardTemplate.transform;
            if (topGoalContainer == null) return null;
            foreach (Transform child in topGoalContainer)
            {
                if (child.name.StartsWith(OrderCardPrefix)) return child;
            }
            return null;
        }

        /// <summary>Hardcoded fallback shell, used only when the container has no existing card to clone from.</summary>
        private GameObject BuildDefaultOrderCardShell(string cardName)
        {
            GameObject cardObj = NewUIObject(cardName, topGoalContainer);
            MarkTransientIfEditMode(cardObj);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();

            Vector2 cardSize = new Vector2(140f, 155f);
            cardRect.sizeDelta = cardSize;

            LayoutElement le = cardObj.AddComponent<LayoutElement>();
            le.preferredWidth = cardSize.x;
            le.preferredHeight = cardSize.y;
            le.minWidth = cardSize.x;
            le.minHeight = cardSize.y;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = new Color(0f, 0f, 0f, 0f);

            GameObject windowObj = NewUIObject("Inner_Window", cardObj.transform);
            RectTransform windowRect = windowObj.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.06f, 0.28f);
            windowRect.anchorMax = new Vector2(0.94f, 0.95f);
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;

            Image windowBg = windowObj.AddComponent<Image>();
            windowBg.color = Color.white;

            Outline windowOutline = windowObj.AddComponent<Outline>();
            windowOutline.effectColor = new Color(0.12f, 0.12f, 0.12f, 0.90f);
            windowOutline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject iconObj = NewUIObject("Icon", windowObj.transform);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.04f, 0.04f);
            iconRect.anchorMax = new Vector2(0.96f, 0.96f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = Vector2.zero;

            GameObject dividerObj = NewUIObject("Divider_Line", cardObj.transform);
            RectTransform dividerRect = dividerObj.GetComponent<RectTransform>();
            dividerRect.anchorMin = new Vector2(0.06f, 0.26f);
            dividerRect.anchorMax = new Vector2(0.94f, 0.27f);
            dividerRect.sizeDelta = Vector2.zero;

            Image dividerImg = dividerObj.AddComponent<Image>();
            dividerImg.color = new Color(0.42f, 0.68f, 0.88f, 0.90f);

            GameObject footerObj = NewUIObject("Footer_Bar", cardObj.transform);
            RectTransform footerRect = footerObj.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0.05f, 0.05f);
            footerRect.anchorMax = new Vector2(0.95f, 0.25f);
            footerRect.offsetMin = Vector2.zero;
            footerRect.offsetMax = Vector2.zero;

            GameObject textObj = NewUIObject("Text", footerObj.transform);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return cardObj;
        }

        private void BuildOrderCardIcon(GameObject iconObj, CustomerOrder order)
        {
            // Cleared first so a repeat call on a card that already has an icon (matched to a live order on
            // every sync now, not just once) doesn't stack a second 3D model wrapper on top of the old one.
            // Detached before destroying: in Play, Destroy() only lands at end of frame, so an un-detached
            // stale wrapper would still answer Find()/GetComponentsInChildren for the rest of this call.
            Transform staleModel = iconObj.transform.Find("3D_Icon_Wrapper");
            if (staleModel != null)
            {
                staleModel.SetParent(null, false);
                SafeDestroy(staleModel.gameObject);
            }

            GameObject displayPrefab = FindDisplayPrefabForItem(order.itemId);

            if (displayPrefab == null)
            {
                Debug.LogWarning($"[OrderCard] '{order.itemId}' için 3D prefab bulunamadı — kart ikonsuz kalacak.");
                Image iconImg = iconObj.GetComponent<Image>() ?? iconObj.AddComponent<Image>();
                iconImg.enabled = true;
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

            // The 3D model wrapper renders on top of/instead of the flat icon sprite, so that Image (if any,
            // e.g. left over from a previous order on this same card) is hidden rather than destroyed - an
            // in-place disable is safe to repeat every call, unlike destroying and immediately re-adding one.
            Image existingIconImg = iconObj.GetComponent<Image>();
            if (existingIconImg != null) existingIconImg.enabled = false;

            Quaternion modelRotation = Quaternion.Euler(goalCard3DModelRotation);

            GameObject modelWrapper = new GameObject("3D_Icon_Wrapper");
            MarkTransientIfEditMode(modelWrapper);
            modelWrapper.transform.SetParent(iconObj.transform, false);
            modelWrapper.transform.localPosition = new Vector3(0f, 0f, goalCard3DModelLocalPosition.z);
            modelWrapper.transform.localRotation = modelRotation;
            modelWrapper.transform.localScale = Vector3.one;

            GameObject modelObj = Instantiate(displayPrefab, modelWrapper.transform);
            modelObj.name = "3D_Icon_Model";
            MarkTransientIfEditMode(modelObj);
            modelObj.transform.localPosition = Vector3.zero;
            modelObj.transform.localRotation = Quaternion.identity;
            modelObj.transform.localScale = Vector3.one;

            // Strip ALL physics, colliders, mono-behaviours, black locks, and UI canvas elements from UI 3D icon model immediately
            foreach (var c in modelObj.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var r in modelObj.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            foreach (var m in modelObj.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(m);
            foreach (var c in modelObj.GetComponentsInChildren<Canvas>(true)) DestroyImmediate(c.gameObject);

            // Restore clean materials if renderers have pitch black material override
            Renderer[] iconRenderers = modelObj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in iconRenderers)
            {
                if (r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("PitchBlack"))
                {
                    // Find original clean prefab to restore materials
                    GameObject origPrefab = FindDisplayPrefabForItem(order.itemId);
                    if (origPrefab != null && origPrefab != displayPrefab)
                    {
                        Renderer[] origRends = origPrefab.GetComponentsInChildren<Renderer>(true);
                        if (origRends != null && origRends.Length > 0 && origRends[0].sharedMaterials != null)
                        {
                            r.sharedMaterials = origRends[0].sharedMaterials;
                        }
                    }
                }
            }

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

                float effectiveTargetSize = goalCard3DModelTargetSize;
                float scaleFactor = (rawMeshSizeInUIPixels > 0.0001f) ? (effectiveTargetSize / rawMeshSizeInUIPixels) : 1f;

                modelObj.transform.localScale = Vector3.one * scaleFactor;
                modelObj.transform.localPosition = -localCenterOffset * scaleFactor;
            }
            else
            {
                modelObj.transform.localScale = Vector3.one * goalCard3DModelTargetSize;
            }

            if (Application.isPlaying)
            {
                modelWrapper.transform.DOLocalMoveY(goalCard3DModelLocalPosition.y + goalCardBobAmplitude, goalCardBobDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
                modelWrapper.transform.DORotate(modelRotation.eulerAngles + new Vector3(0f, goalCardRotateAmplitude, 0f), goalCardRotateDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            }
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
            if (topGoalContainer == null || CustomerOrderManager.Instance == null)
            {
                return;
            }

            foreach (CustomerOrder order in CustomerOrderManager.Instance.ActiveOrders)
            {
                if (order == null || order.isCompleted) continue;

                Transform card = FindOrderCard(order.orderId);
                if (card == null)
                {
                    continue;
                }

                // NOT card.Find("Text") - Find only searches DIRECT children, and the real card hierarchy
                // is card/Footer_Bar/Text (confirmed in the live scene), one level deeper. Find() returned
                // null every single call, so this silently never updated a card's number as items were
                // collected - only SyncOrderCards' BuildOrderCard (which already correctly uses
                // GetComponentInChildren<Text>() below) caught it back up, and only on a full sync at
                // order completion. That is exactly Emre's "numbers only update on a merge" report.
                Text txt = card.GetComponentInChildren<Text>();
                if (txt == null)
                {
                    continue;
                }
                Transform textObj = txt.transform;

                // Matches BuildOrderCard's own "0 -> ✓" formatting. Without this, the live tick briefly
                // showed a bare "0" the moment a group finished collecting but before it had flown into
                // the card (TryDeliverGroup fires later, in the collect animation's completion callback).
                int remCount = RemainingForOrder(order);
                string next = remCount > 0 ? $"{remCount}" : "✓";
                bool changed = txt.text != next;
                txt.text = next;

                if (!changed) continue;
                if (!order.itemId.Equals(changedItemId, System.StringComparison.OrdinalIgnoreCase)) continue;

                txt.DOKill(true);
                txt.DOColor(new Color(1f, 0.92f, 0.25f), 0.12f).OnComplete(() => txt.DOColor(Color.white, 0.3f));
                textObj.DOKill(true);
                textObj.localScale = Vector3.one;
                textObj.DOPunchScale(Vector3.one * goalTickTextPunchStrength, goalTickTextPunchDuration, 7, 0.9f);

                card.DOKill(true);
                card.localScale = Vector3.one;
                card.DOPunchScale(Vector3.one * goalTickCardPunchStrength, goalTickCardPunchDuration, 8, 0.8f);
            }
        }

        /// <summary>The hit an order card takes when a matched group slams into it.</summary>
        private void PunchOrderCard(Transform card)
        {
            if (card == null) return;

            card.DOKill(true);
            card.localScale = Vector3.one;
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
        // Mecha identification — delegated to MechaIdentifier for separation of concerns
        // ---------------------------------------------------------------------------------------------

        public static bool IsMechaItem(FindTargetObject item) => MechaIdentifier.IsMechaItem(item);
        public static bool HasChildMecha(FindTargetObject item) => MechaIdentifier.HasChildMecha(item);
        public static bool IsHitOnMechaCollider(FindTargetObject item, Collider hitCollider) => MechaIdentifier.IsHitOnMechaCollider(item, hitCollider);

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
            if (item == null) return false;
            if (gameOverTriggered) { return false; }

            bool isMecha = (hitCollider != null && IsHitOnMechaCollider(item, hitCollider))
                || item.name.Contains("Mecha") || item.name.Contains("meccha")
                || (item.colorName != null && item.colorName.Equals("mecha", System.StringComparison.OrdinalIgnoreCase))
                || HasChildMecha(item);

            if (isMecha)
            {
                HandleMechaTap(item, hitCollider);
                return false;
            }

            // A running mecha locks out collecting all other objects until it is caught!
            if (MechaRunnerBehavior.IsAnyMechaRunning()) { return false; }

            // A pitch-black locked item cannot be collected until its counter reaches 0!
            BlackLockItem blackLock = item.GetComponent<BlackLockItem>();
            if (blackLock != null && blackLock.IsLocked)
            {
                blackLock.PlayLockedWiggle();
                return false;
            }

            int maxCapacity = slot3DTransforms.Count > 0 ? slot3DTransforms.Count : slotRects.Count;
            if (dockItems.Count >= DockCapacity || dockItems.Count >= maxCapacity)
            {
                return false;
            }

            item.isDocked = true;
            // Activate the vibrant yellow outline on selection so the chosen item stands out clearly.
            item.SetYellowOutlineActive(true);

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
                objectColor = item.objectColor,
                originalWorldScale = item.transform.localScale,
                originalPosition = item.transform.position,
                originalRotation = item.transform.rotation
            };

            int insertIndex = GetInsertIndexForType(item.colorName);
            dockItems.Insert(insertIndex, data);

            UpdateSlotVisuals();
            RefreshOrderCardCounts(item.colorName);

            // Notify all black locked items in scene that an item was placed into the dock!
            BlackLockItem.NotifyItemDocked();

            string collectedType = item.colorName;
            AnimateItemIntoSlot(item.gameObject, insertIndex, () => EvaluateDockAfterLanding(collectedType));
            return true;
        }

        private void HandleMechaTap(FindTargetObject item, Collider hitCollider)
        {
            if (item == null) return;

            MechaRunnerBehavior runner = item.GetComponentInChildren<MechaRunnerBehavior>();
            if (runner == null && hitCollider != null)
            {
                runner = hitCollider.GetComponentInParent<MechaRunnerBehavior>();
            }

            if (runner == null)
            {
                Transform mechaChild = null;
                foreach (Transform child in item.transform)
                {
                    if (child == null) continue;
                    string n = child.name.ToLowerInvariant();
                    if (n.Contains("mecha") || n.Contains("meccha") || n.Contains("ragdoll") || n.Contains("chameleon") || child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    {
                        mechaChild = child;
                        break;
                    }
                }

                if (mechaChild != null)
                {
                    runner = mechaChild.GetComponent<MechaRunnerBehavior>() ?? mechaChild.gameObject.AddComponent<MechaRunnerBehavior>();
                }
            }

            if (runner == null) return;

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
        /// Returns the slot insertion index for a freshly collected item.
        /// Items are added strictly in raw arrival order (at the end of the dock array).
        /// If another item type is sitting between previous items of this type, the new item
        /// will NOT jump back between them, and orders will only complete when requiredCount
        /// items are strictly contiguous (adjacent without interrupting item types).
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
            if (dockItems.Count >= DockCapacity && !AnyDockItemInFlight())
            {
                TriggerGameOver();
            }
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
            if (CustomerOrderManager.Instance == null)
            {
                return false;
            }

            CustomerOrder order = CustomerOrderManager.Instance.FindOrderForItem(itemId);
            if (order == null)
            {
                return false;
            }

            List<DockItemData> group = FindContiguousGroupForOrder(itemId, order.requiredCount);
            if (group == null || group.Count < order.requiredCount)
            {
                return false;
            }

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
                    if (VFXManager.Instance != null)
                    {
                        VFXManager.Instance.PlayMatchBlastVFX(target, data.objectColor);
                    }
                    SafeDestroy(obj);

                    if (++landed >= total) CompleteDeliveredOrder(order, total);
                });
                seq.Play();
            }

            if (total == 0) CompleteDeliveredOrder(order, 0);
        }

        private void CompleteDeliveredOrder(CustomerOrder order, int deliveredCount)
        {
            if (deliveredCount > 0) HapticHelper.Vibrate();

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

        public Vector3 GetSlotWorldPosition(int slotIndex, GameObject obj3D = null)
        {
            if (slot3DTransforms != null && slotIndex >= 0 && slotIndex < slot3DTransforms.Count && slot3DTransforms[slotIndex] != null)
            {
                Transform t = slot3DTransforms[slotIndex];
                float tileHalfHeight = Mathf.Max(0.04f, t.localScale.y * 0.5f);
                float cubeTopY = t.position.y + tileHalfHeight;

                if (obj3D == null)
                {
                    return new Vector3(t.position.x, cubeTopY + 0.10f, t.position.z);
                }

                // Compute exact dock target scale and rotation for this object
                float fitScaleVal = ComputeFitScaleForSlot(slotIndex, obj3D);
                Vector3 targetScale = Vector3.one * fitScaleVal;
                Quaternion targetRot = GetDockItemRotation(slotIndex, obj3D);

                // Temporarily apply target dock transform to calculate exact bottom-most surface Y
                Vector3 origScale = obj3D.transform.localScale;
                Quaternion origRot = obj3D.transform.rotation;
                Vector3 origPos = obj3D.transform.position;

                obj3D.transform.localScale = targetScale;
                obj3D.transform.rotation = targetRot;

                Renderer[] rends = obj3D.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++)
                    {
                        if (rends[i] != null && rends[i].enabled) b.Encapsulate(rends[i].bounds);
                    }

                    // Restore original transform values
                    obj3D.transform.localScale = origScale;
                    obj3D.transform.rotation = origRot;

                    // Exact distance from obj3D's pivot to its lowest surface point when in dock orientation
                    float pivotToBottomDistanceY = origPos.y - b.min.y;
                    Vector3 centerOffset = origPos - b.center;

                    // Place target Y so that b.min.y sits exactly 0.005f (0.5mm) above cubeTopY
                    float targetY = cubeTopY + pivotToBottomDistanceY + 0.005f;
                    float targetX = t.position.x + centerOffset.x;
                    float targetZ = t.position.z + centerOffset.z;

                    return new Vector3(targetX, targetY, targetZ);
                }

                obj3D.transform.localScale = origScale;
                obj3D.transform.rotation = origRot;
                return new Vector3(t.position.x, cubeTopY + 0.10f, t.position.z);
            }

            if (slotIndex < 0 || slotIndex >= slotRects.Count) return Vector3.zero;
            Vector3 pos = GetUIWorldPosition(slotRects[slotIndex]);
            if (mainCamera != null && pos != Vector3.zero)
            {
                pos += mainCamera.transform.up * 0.02f;
            }
            return pos;
        }

        private float GetObjectBottomOffset(GameObject obj)
        {
            if (obj == null) return 0f;
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return 0f;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].enabled) b.Encapsulate(rends[i].bounds);
            }

            float pivotToBottom = obj.transform.position.y - b.min.y;
            return Mathf.Max(0f, pivotToBottom);
        }

        private readonly Dictionary<int, float> dockScaleCache = new Dictionary<int, float>();

        private float ComputeFitScaleForSlot(int slotIndex, GameObject obj3D)
        {
            if (obj3D == null) return 1f;

            if (slot3DTransforms != null && slotIndex >= 0 && slotIndex < slot3DTransforms.Count && slot3DTransforms[slotIndex] != null)
            {
                int instanceId = obj3D.GetInstanceID();
                if (dockScaleCache.TryGetValue(instanceId, out float cachedScale) && cachedScale > 1e-4f)
                {
                    return cachedScale;
                }

                float currentWorldExtent = GetCurrentWorldMaxExtent(obj3D);
                // Prominent size: 0.53f world units fills ~78% of slot tile top surface (width 0.68f) perfectly
                float targetDockWorldSize = 0.53f;
                float currentScale = obj3D.transform.localScale.x;

                float computedScale = currentScale * (targetDockWorldSize / currentWorldExtent);
                float finalScale = Mathf.Max(computedScale, 0.05f);

                dockScaleCache[instanceId] = finalScale;
                return finalScale;
            }

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
            float currentWorldExt = GetCurrentWorldMaxExtent(obj3D);
            return currentWorldExt > 1e-4f ? obj3D.transform.localScale.x * (targetWorldSize / currentWorldExt) : targetWorldSize;
        }

        private static float GetCurrentWorldMaxExtent(GameObject obj)
        {
            if (obj == null) return 1f;
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return 1f;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].enabled) b.Encapsulate(rends[i].bounds);
            }

            float maxExtent = Mathf.Max(b.size.x, b.size.y, b.size.z);
            return maxExtent > 1e-4f ? maxExtent : 1f;
        }

        private Quaternion GetDockItemRotation(int slotIndex = 0, GameObject obj3D = null)
        {
            Quaternion slotRot = (slot3DTransforms != null && slotIndex >= 0 && slotIndex < slot3DTransforms.Count && slot3DTransforms[slotIndex] != null)
                ? slot3DTransforms[slotIndex].rotation
                : Quaternion.identity;

            if (obj3D != null)
            {
                FindTargetObject fto = obj3D.GetComponent<FindTargetObject>();
                string itemId = fto != null ? fto.colorName : obj3D.name;
                ItemDataSO itemSO = FindItemDataSO(itemId);
                if (itemSO != null && itemSO.overrideDockRotation)
                {
                    return slotRot * Quaternion.Euler(itemSO.dockRotationEuler);
                }
            }

            return slotRot * Quaternion.Euler(dockItemDefaultRotation);
        }

        public void Setup3DDockSlots()
        {
            slot3DTransforms.Clear();

            GameObject container = GameObject.Find("Dock_3D_Slots");
            if (container == null)
            {
                container = new GameObject("Dock_3D_Slots");
                Transform psc = GameObject.Find("Physics_Scene_Controller")?.transform;
                if (psc != null) container.transform.SetParent(psc, false);
            }

            int slotCount = DockCapacity;
            for (int i = 0; i < slotCount; i++)
            {
                Transform existingSlot = container.transform.Find($"DockSlot_3D_{i}");
                if (existingSlot == null) existingSlot = container.transform.Find($"DockSlot_{i}");

                if (existingSlot == null)
                {
                    GameObject slotObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slotObj.name = $"DockSlot_3D_{i}";
                    slotObj.transform.SetParent(container.transform, false);

                    Collider col = slotObj.GetComponent<Collider>();
                    if (col != null) SafeDestroy(col);

                    slotObj.transform.localScale = new Vector3(0.68f, 0.08f, 0.68f);

                    float spacing = 0.75f;
                    float startX = -(slotCount - 1) * 0.5f * spacing;
                    slotObj.transform.localPosition = new Vector3(startX + i * spacing, 0.08f, -3.0f);
                    slotObj.transform.localRotation = Quaternion.identity;

                    ApplyDefaultSlotMaterial(slotObj.GetComponent<Renderer>());
                    existingSlot = slotObj.transform;
                }

                slot3DTransforms.Add(existingSlot);
            }
        }

        private static void ApplyDefaultSlotMaterial(Renderer rend)
        {
            if (rend == null) return;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = true;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = "Mat_SlateGrey_3D_Slot";
            mat.color = new Color(0.26f, 0.30f, 0.36f, 1.0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
            else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.45f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.20f);
            rend.material = mat;
        }

        private void UpdateSlotVisuals()
        {
            for (int i = 0; i < slotImages.Count; i++)
            {
                if (slotImages[i] == null) continue;
                slotImages[i].DOKill();
                slotImages[i].color = i < dockItems.Count ? dockSlotFilledColor : dockSlotEmptyColor;
            }

            for (int i = 0; i < slot3DTransforms.Count; i++)
            {
                Transform t = slot3DTransforms[i];
                if (t == null) continue;

                Renderer rend = t.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = Application.isPlaying ? rend.material : rend.sharedMaterial;
                    if (mat == null) continue;
                    bool isFilled = i < dockItems.Count;

                    if (isFilled)
                    {
                        Color glowColor = new Color(0.38f, 0.82f, 0.20f, 1.0f);
                        mat.color = glowColor;

                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", new Color(0.20f, 0.70f, 0.12f) * 1.5f);
                        }
                    }
                    else
                    {
                        Color slateGrey = new Color(0.26f, 0.30f, 0.36f, 1.0f);
                        mat.color = slateGrey;

                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.DisableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", Color.black);
                        }
                    }
                }
            }
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

            Vector3 slotWorldPos = GetSlotWorldPosition(slotIndex, obj3D);
            if (slotWorldPos == Vector3.zero) slotWorldPos = obj3D.transform.position;

            Vector3 targetScale = Vector3.one * ComputeFitScaleForSlot(slotIndex, obj3D);

            tweeningDockObjects.Add(obj3D);
            obj3D.transform.DOKill();

            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            // Ensure yellow outline is active during selection & flight
            FindTargetObject targetComp = obj3D.GetComponent<FindTargetObject>();
            if (targetComp != null)
            {
                targetComp.SetYellowOutlineActive(true);
            }

            // Phase 1: Zoom towards screen/camera to clearly highlight selection
            Vector3 startPos = obj3D.transform.position;
            Vector3 camPos = mainCamera != null ? mainCamera.transform.position : startPos + Vector3.up * 2f;
            Vector3 popPos = Vector3.Lerp(startPos, camPos, 0.32f) + Vector3.up * 0.50f;
            Vector3 popScale = obj3D.transform.localScale * 1.40f;

            float popDuration = 0.20f;
            float flightDuration = collectFlightDuration > 0f ? collectFlightDuration : 0.44f;

            Sequence seq = DOTween.Sequence();

            // 1. Pop towards screen
            seq.Append(obj3D.transform.DOMove(popPos, popDuration).SetEase(Ease.OutBack, 1.5f));
            seq.Join(obj3D.transform.DOScale(popScale, popDuration).SetEase(Ease.OutQuad));
            seq.AppendInterval(0.04f);

            // 2. Flight arc into 3D slot - gently floats & falls into slot
            seq.Append(obj3D.transform.DOJump(slotWorldPos, GetDockJumpPower(1.20f), 1, flightDuration).SetEase(Ease.OutCubic));
            seq.Join(obj3D.transform.DOScale(targetScale, flightDuration).SetEase(Ease.OutQuad));
            seq.Join(obj3D.transform.DORotateQuaternion(GetDockItemRotation(slotIndex, obj3D), flightDuration).SetEase(Ease.OutQuad));

            // 3. Touchdown into slot - turn off yellow outline ONLY when settled into slot!
            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);
                if (targetComp != null)
                {
                    targetComp.SetYellowOutlineActive(false);
                }

                if (obj3D != null)
                {
                    obj3D.transform.DOPunchScale(targetScale * 0.18f, 0.20f, 5, 0.5f);
                }

                if (slot3DTransforms != null && slotIndex >= 0 && slotIndex < slot3DTransforms.Count && slot3DTransforms[slotIndex] != null)
                {
                    slot3DTransforms[slotIndex].DOPunchScale(Vector3.one * 0.08f, 0.20f, 5, 0.5f);
                }

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
            int maxSlots = slot3DTransforms.Count > 0 ? slot3DTransforms.Count : slotRects.Count;
            for (int i = 0; i < dockItems.Count && i < maxSlots; i++)
            {
                DockItemData data = dockItems[i];
                if (data?.targetObject == null) continue;

                GameObject obj = data.targetObject.gameObject;
                if (tweeningDockObjects.Contains(obj)) continue;

                Vector3 slotWorldPos = GetSlotWorldPosition(i, obj);
                if (slotWorldPos != Vector3.zero &&
                    (obj.transform.position - slotWorldPos).sqrMagnitude > 0.0000001f)
                {
                    obj.transform.position = Vector3.Lerp(obj.transform.position, slotWorldPos, Time.deltaTime * 18f);
                }

                obj.transform.localScale = Vector3.one * ComputeFitScaleForSlot(i, obj);

                Quaternion targetRot = GetDockItemRotation(i, obj);
                if (Quaternion.Angle(obj.transform.rotation, targetRot) > 0.05f)
                {
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, targetRot, Time.deltaTime * 15f);
                }
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
