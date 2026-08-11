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

        private const int MAX_SLOTS = 2;
        private const int ITEMS_PER_BOX = 3;

        // Rigged flap bones (relative path from the box root) that fold shut for the lid-close animation.
        private static readonly string[] FlapBonePaths =
        {
            "Armature/Front.Top",
            "Armature/Back.Top",
            "Armature/Left.Top",
            "Armature/Right.Top"
        };

        // Local rotations captured from the "Cardboard Box (Opened)" / "Cardboard Box (Closed)" reference
        // prefabs, in the same Front/Back/Left/Right order as FlapBonePaths.
        private static readonly Quaternion[] FlapOpenLocalRotations =
        {
            new Quaternion(0.8997768f, 0f, 0f, 0.4363507f),
            new Quaternion(0.37796697f, 0f, 0f, 0.9258191f),
            new Quaternion(0.68000966f, -0.20325376f, 0.19386926f, 0.6772663f),
            new Quaternion(0.62172043f, 0.3368441f, -0.33684343f, 0.6217205f)
        };

        private static readonly Quaternion[] FlapClosedLocalRotations =
        {
            Quaternion.identity,
            new Quaternion(1f, 0f, 0f, 0f),
            new Quaternion(0.49653107f, 0.4965316f, -0.50344455f, 0.50344497f),
            new Quaternion(0.5f, -0.5f, 0.5f, 0.5f)
        };

        private readonly GameObject[] slotBox = new GameObject[MAX_SLOTS];
        private readonly string[] slotAssignedItemName = new string[MAX_SLOTS];
        private readonly List<DockItemData>[] slotBoxContents = new List<DockItemData>[MAX_SLOTS];

        private Canvas mainCanvas;
        private RectTransform topGoalContainer;
        private RectTransform bottomDockContainer;

        private readonly List<RectTransform> slotRects = new List<RectTransform>();
        private readonly List<Text> slotBadgeTexts = new List<Text>();
        private readonly List<Image> slotItemIconImages = new List<Image>();
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private Camera mainCamera;
        private bool isProcessingMatch = false;

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
            dockRect.sizeDelta = new Vector2(880, 210);

            Image bg = dockObj.AddComponent<Image>();
            ApplySlicedSprite(bg, ButtonSprite("Violet"));

            GameObject slotsContainerObj = new GameObject("Slots_Container");
            slotsContainerObj.transform.SetParent(dockObj.transform, false);

            bottomDockContainer = slotsContainerObj.AddComponent<RectTransform>();
            bottomDockContainer.anchorMin = Vector2.zero;
            bottomDockContainer.anchorMax = Vector2.one;
            bottomDockContainer.sizeDelta = new Vector2(-20, 0);

            HorizontalLayoutGroup layout = slotsContainerObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 16, 16);
            layout.spacing = 60;
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
                labelTxt.fontSize = 22;
                labelTxt.fontStyle = FontStyle.Bold;
                labelTxt.alignment = TextAnchor.MiddleCenter;
                labelTxt.color = Color.yellow;
                labelTxt.text = $"KUTU {i + 1}";

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

        public bool TryCollectItemToDock(FindTargetObject item)
        {
            if (isProcessingMatch || item == null) return false;

            string itemType = item.colorName;

            int targetSlot = -1;
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (slotAssignedItemName[i] == itemType && slotBoxContents[i].Count < ITEMS_PER_BOX)
                {
                    targetSlot = i;
                    break;
                }
            }

            if (targetSlot == -1)
            {
                for (int i = 0; i < MAX_SLOTS; i++)
                {
                    if (string.IsNullOrEmpty(slotAssignedItemName[i]))
                    {
                        slotAssignedItemName[i] = itemType;
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot == -1)
            {
                return false;
            }

            item.isDocked = true;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
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

            AnimateItemIntoBox(item.gameObject, targetSlot, () =>
            {
                if (slotBoxContents[targetSlot].Count >= ITEMS_PER_BOX)
                {
                    AnimateSlowBoxClosingAndShipping(targetSlot);
                }
            });

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
            if (cardboardBoxOpenedPrefab == null)
            {
                cardboardBoxOpenedPrefab = Resources.Load<GameObject>("CardboardBox/Cardboard Box (Opened)");
            }
        }

        private static void StripPhysicsFromVisual(GameObject go)
        {
            if (go == null) return;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        }

        private static Transform[] GetFlapBones(GameObject box)
        {
            if (box == null) return null;
            Transform[] bones = new Transform[FlapBonePaths.Length];
            for (int i = 0; i < FlapBonePaths.Length; i++)
            {
                bones[i] = box.transform.Find(FlapBonePaths[i]);
            }
            return bones;
        }

        private static void SetBoxFlapsInstant(GameObject box, bool closed)
        {
            Transform[] bones = GetFlapBones(box);
            if (bones == null) return;
            Quaternion[] targets = closed ? FlapClosedLocalRotations : FlapOpenLocalRotations;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null) bones[i].localRotation = targets[i];
            }
        }

        /// <summary>Joins per-flap rotation tweens into an existing sequence so all four lid flaps fold together, in place (no position/scale change on the box itself).</summary>
        private static void AnimateBoxFlaps(Sequence seq, GameObject box, bool closing, float duration)
        {
            Transform[] bones = GetFlapBones(box);
            if (bones == null) return;
            Quaternion[] targets = closing ? FlapClosedLocalRotations : FlapOpenLocalRotations;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    seq.Join(bones[i].DOLocalRotateQuaternion(targets[i], duration).SetEase(Ease.InOutQuad));
                }
            }
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
                if (go != null && (go.name.Contains("Slot3DBox") || go.name.Contains("Delivery_3DBox") || go.name.StartsWith("Cardboard Box")))
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
                    GameObject box = Instantiate(cardboardBoxOpenedPrefab, transform);
                    box.name = $"Slot3DBox_{i}";
                    StripPhysicsFromVisual(box);
                    SetBoxFlapsInstant(box, false);

                    float fitScale = ComputeFitScaleForSlot(i, box) * 1.25f;
                    box.transform.localScale = Vector3.one * fitScale;
                    box.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                    box.transform.position = GetSlotWorldPosition(i);

                    slotBox[i] = box;
                }
            }
        }

        private Quaternion GetDockItemSidewaysRotation()
        {
            return Quaternion.Euler(22f, 35f, 0f);
        }

        private void AnimateItemIntoBox(GameObject obj3D, int slotIndex, System.Action onComplete)
        {
            if (obj3D == null) return;

            const float duration = 0.45f;
            Vector3 slotWorldPos = GetSlotWorldPosition(slotIndex);
            int countInBox = slotBoxContents[slotIndex].Count;
            Vector3 boxItemPos = slotWorldPos + new Vector3((countInBox - 2f) * 0.06f, 0.01f, 0.03f);

            float targetScaleVal = ComputeFitScaleForSlot(slotIndex, obj3D) * 0.55f;
            Vector3 targetScale = Vector3.one * targetScaleVal;
            Quaternion targetRot = GetDockItemSidewaysRotation();

            tweeningDockObjects.Add(obj3D);
            obj3D.transform.DOKill();

            Sequence seq = DOTween.Sequence();
            seq.Join(obj3D.transform.DOJump(boxItemPos, 1.0f, 1, duration).SetEase(Ease.InQuad));
            seq.Join(obj3D.transform.DOScale(targetScale, duration).SetEase(Ease.OutBounce));
            seq.Join(obj3D.transform.DORotateQuaternion(targetRot, duration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);

                if (slotIndex >= 0 && slotIndex < MAX_SLOTS && slotBox[slotIndex] != null)
                {
                    GameObject box = slotBox[slotIndex];
                    box.transform.DOKill(true);
                    box.transform.DOPunchScale(new Vector3(0.18f, -0.12f, 0.18f), 0.28f, 5, 0.5f);
                }

                onComplete?.Invoke();
            });
        }

        private void AlignDocked3DObjectsWithSlots()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                Vector3 slotWorldPos = GetSlotWorldPosition(i);

                if (slotBox[i] != null && !tweeningDockObjects.Contains(slotBox[i]))
                {
                    slotBox[i].transform.position = Vector3.Lerp(slotBox[i].transform.position, slotWorldPos, Time.deltaTime * 22f);
                    float fitScale = ComputeFitScaleForSlot(i, slotBox[i]) * 1.25f;
                    slotBox[i].transform.localScale = Vector3.one * fitScale;
                    slotBox[i].transform.rotation = Quaternion.Slerp(slotBox[i].transform.rotation, Quaternion.Euler(18f, 0f, 0f), Time.deltaTime * 15f);
                }

                List<DockItemData> itemsInBox = slotBoxContents[i];
                if (itemsInBox != null)
                {
                    for (int k = 0; k < itemsInBox.Count; k++)
                    {
                        DockItemData data = itemsInBox[k];
                        if (data != null && data.targetObject != null && !tweeningDockObjects.Contains(data.targetObject.gameObject))
                        {
                            Vector3 boxItemPos = slotWorldPos + new Vector3((k - 1f) * 0.06f, 0.01f, 0.03f);
                            data.targetObject.transform.position = Vector3.Lerp(data.targetObject.transform.position, boxItemPos, Time.deltaTime * 22f);
                            float fitScale = ComputeFitScaleForSlot(i, data.targetObject.gameObject) * 0.55f;
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
            if (box == null) return;

            Transform existingGroup = box.transform.Find("Box_Item_Display");
            if (existingGroup != null) Destroy(existingGroup.gameObject);
            if (itemData == null || itemData.targetObject == null) return;

            // Find the box's own world-space top so the label sits on the lid regardless of the box's
            // current scale/rotation, then parent everything with worldPositionStays so it travels with it.
            Bounds boxBounds = default;
            bool hasBoxBounds = false;
            foreach (Renderer r in box.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (!hasBoxBounds) { boxBounds = r.bounds; hasBoxBounds = true; }
                else boxBounds.Encapsulate(r.bounds);
            }

            Vector3 topWorldPos = hasBoxBounds
                ? new Vector3(boxBounds.center.x, boxBounds.max.y, boxBounds.center.z)
                : box.transform.position;
            // Kept modest and mostly embedded in the lid (rather than sized to the box's full footprint)
            // so wildly different food-model proportions (a thin sausage vs. a wide watermelon slice)
            // don't end up towering over or swallowing the box.
            float displayWorldSize = hasBoxBounds ? Mathf.Min(boxBounds.size.x, boxBounds.size.z) * 0.42f : 0.10f;

            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            Vector3 faceDir = mainCamera != null
                ? (mainCamera.transform.position - topWorldPos).normalized
                : box.transform.up;

            // Single group anchors both pieces to the lid; everything below is positioned/rotated in
            // world space and then parented in at the end, so nothing is left floating loose.
            GameObject group = new GameObject("Box_Item_Display");
            group.transform.position = topWorldPos + box.transform.up * (displayWorldSize * 0.25f);
            group.transform.rotation = Quaternion.identity;

            // Rounded card backdrop, always facing the camera so it reads clearly no matter how the box
            // is tilted mid-animation.
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Collider backdropCollider = backdrop.GetComponent<Collider>();
            if (backdropCollider != null) Destroy(backdropCollider);

            Sprite badgeSprite = ButtonSprite("White");
            if (badgeSprite == null) badgeSprite = ButtonSprite("Yellow");
            if (badgeSprite != null && badgeSprite.texture != null)
            {
                Shader cardShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Lit");
                Material backdropMat = new Material(cardShader) { name = "BoxLabelBackdrop" };
                if (backdropMat.HasProperty("_BaseMap")) backdropMat.SetTexture("_BaseMap", badgeSprite.texture);
                if (backdropMat.HasProperty("_MainTex")) backdropMat.SetTexture("_MainTex", badgeSprite.texture);
                backdrop.GetComponent<Renderer>().material = backdropMat;
            }
            Renderer backdropRenderer = backdrop.GetComponent<Renderer>();
            backdropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;

            backdrop.transform.position = group.transform.position;
            backdrop.transform.rotation = Quaternion.LookRotation(faceDir);
            backdrop.transform.localScale = Vector3.one * (displayWorldSize * 1.8f);
            backdrop.transform.SetParent(group.transform, true);

            // The actual packed item's real mesh, cloned and stripped of physics/scripts, resting just
            // in front of the backdrop card.
            GameObject display = Instantiate(itemData.targetObject.gameObject);
            display.name = "Item";

            foreach (Component comp in display.GetComponentsInChildren<Component>(true))
            {
                if (comp is Transform || comp is Renderer || comp is MeshFilter) continue;
                Destroy(comp);
            }

            foreach (Renderer r in display.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            float itemLocalMax = GetObjectStaticUnscaledMaxExtent(display);
            float finalScale = itemLocalMax > 1e-4f ? (displayWorldSize * 0.85f) / itemLocalMax : displayWorldSize * 0.85f;

            display.transform.position = group.transform.position + faceDir * (displayWorldSize * 0.15f);
            display.transform.rotation = Quaternion.Euler(15f, 35f, 0f);
            display.transform.localScale = Vector3.one * finalScale;
            display.transform.SetParent(group.transform, true);

            group.transform.SetParent(box.transform, true);
        }

        private int completedBoxesCount = 0;
        private readonly List<GameObject> completedBoxObjects = new List<GameObject>();

        private const int COMPLETED_BOXES_PER_ROW = 2;

        /// <summary>
        /// Measures a live slot box's current on-screen footprint and scales it up by the same factor
        /// applied when a box finishes shipping (baseScale*1.30 on top of the dock's *1.25), so the shelf
        /// grid below can space boxes by their real rendered size instead of a guessed screen fraction.
        /// </summary>
        private float EstimateShippedBoxFootprint()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (slotBox[i] == null) continue;
                Renderer[] rends = slotBox[i].GetComponentsInChildren<Renderer>();
                if (rends == null || rends.Length == 0) continue;

                Bounds b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                {
                    if (rends[k] != null) b.Encapsulate(rends[k].bounds);
                }

                float maxSide = Mathf.Max(b.size.x, b.size.z);
                if (maxSide > 1e-4f) return maxSide * 1.30f;
            }

            return 0.5f;
        }

        public Vector3 GetRedMarkedCompletedBoxWorldPos(int index)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            int row = index / COMPLETED_BOXES_PER_ROW;
            int col = index % COMPLETED_BOXES_PER_ROW;

            if (mainCamera == null) return new Vector3(-1.0f + col * 1.0f, 0.60f + row * 0.7f, -1.6f);

            // Anchor point for the completed-boxes shelf: right above the "KARIŞTIR" button / purple dock
            // bar, in the gap between the dock and the loose pile (per the placement the user marked).
            Vector3 anchorWorld = mainCamera.ScreenToWorldPoint(new Vector3(
                Screen.width * 0.50f, Screen.height * 0.235f, dockCameraDepth + 0.40f));

            // Lay the grid out along the camera's own screen-facing right/up axes so it always reads as a
            // flat row to the player, and space cells by the box's real footprint so they never overlap
            // regardless of resolution or how big ComputeFitScaleForSlot ends up making the boxes.
            Vector3 camRight = mainCamera.transform.right;
            Vector3 camUp = mainCamera.transform.up;
            float cellSize = EstimateShippedBoxFootprint() * 1.15f;

            float rowWidth = (COMPLETED_BOXES_PER_ROW - 1) * cellSize;
            float colOffset = -rowWidth * 0.5f + col * cellSize;

            // Rows grow upward from this bottom anchor, into the gap between the dock and the pile,
            // instead of downward into the dock bar itself.
            Vector3 worldPos = anchorWorld + camRight * colOffset + camUp * (row * cellSize);
            return worldPos;
        }

        private void AnimateSlowBoxClosingAndShipping(int slotIndex)
        {
            isProcessingMatch = true;

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

            // Phase 5: Jump to the completed-boxes shelf, directly above KUTU 1 / next to the KARIŞTIR button
            Vector3 shipTargetWorld = GetRedMarkedCompletedBoxWorldPos(completedBoxesCount);
            if (box != null)
            {
                boxSeq.Append(box.transform.DOJump(shipTargetWorld, 0.9f, 1, 1.0f).SetEase(Ease.OutCubic));
                boxSeq.Join(box.transform.DOScale(baseScale * 1.15f, 1.0f));
                boxSeq.Join(box.transform.DORotate(new Vector3(16f, 10f, 0f), 1.0f));
            }

            boxSeq.OnComplete(() =>
            {
                if (box != null)
                {
                    tweeningDockObjects.Remove(box);
                    box.transform.DOPunchScale(new Vector3(0.14f, -0.10f, 0.14f), 0.32f);
                    completedBoxObjects.Add(box);
                }

                completedBoxesCount++;

                // Spawn a fresh open box for this slot so a new item can start filling it
                if (cardboardBoxOpenedPrefab != null)
                {
                    GameObject newBox = Instantiate(cardboardBoxOpenedPrefab, transform);
                    newBox.name = $"Slot3DBox_{slotIndex}";
                    StripPhysicsFromVisual(newBox);
                    SetBoxFlapsInstant(newBox, false);
                    newBox.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
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

                UpdateSlotBadgesUI();
                isProcessingMatch = false;
            });
        }

        private void UpdateSlotBadgesUI()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (i < slotRects.Count)
                {
                    int count = slotBoxContents[i].Count;
                    string assignedName = slotAssignedItemName[i];

                    if (!string.IsNullOrEmpty(assignedName))
                    {
                        slotBadgeTexts[i].text = $"{count}/{ITEMS_PER_BOX}";

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
                                    iconImg.color = Color.yellow;
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
            }

            tweeningDockObjects.Clear();
            isProcessingMatch = false;

            UpdateSlotBadgesUI();
        }
    }
}
