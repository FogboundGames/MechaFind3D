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
    /// Professional Match Factory Style 3D Item Docking & Canvas UI Manager.
    /// Positions mini 3D objects (scale 0.10f) 100% PERFECTLY INSIDE the UI Slot Holders.
    /// </summary>
    public class CanvasUIDesignManager : MonoBehaviour
    {
        public static CanvasUIDesignManager Instance { get; private set; }

        [Header("Canvas Configuration")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);

        [Header("Mini 3D Object Slot Holder Alignment")]
        [Tooltip("Fallback world scale used only if the slot's on-screen size can't be measured yet.")]
        [SerializeField] private float miniObjectDockScale = 0.055f;
        [Tooltip("Fraction of the slot's shorter on-screen dimension the docked item should visually fill. Computed live from the slot's actual rect, so it stays correctly sized/centered on any resolution.")]
        [SerializeField] private float dockItemFillRatio = 0.62f;
        [SerializeField] private float dockCameraDepth = 1.6f;       // Near depth in front of camera

        [Header("UI Compositing (keeps docked 3D objects visible over the slot art)")]
        [Tooltip("Canvas distance from camera. Must stay greater than dockCameraDepth so docked mini objects render in FRONT of the slot UI instead of being hidden behind it.")]
        [SerializeField] private float uiPlaneDistance = 3.2f;

        private Canvas mainCanvas;
        private RectTransform topGoalContainer;
        private RectTransform bottomDockContainer;
        private RectTransform rightDeliveryBoxRect;

        private readonly List<RectTransform> slotRects = new List<RectTransform>();
        private readonly List<Text> slotBadgeTexts = new List<Text>();
        private readonly List<DockItemData> dockedItems = new List<DockItemData>();
        private readonly HashSet<GameObject> tweeningDockObjects = new HashSet<GameObject>();

        private const int MAX_SLOTS = 7;
        private Camera mainCamera;
        private bool isProcessingMatch = false;

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();

            EnsureCanvasStructure();
        }

        private void Start()
        {
            if (MatchGoalManager.Instance != null)
            {
                MatchGoalManager.Instance.SetupLevelGoals();
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

            // Continuously align mini 3D objects centered inside slot holders
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
            Debug.Log("🎨 Professional Canvas UI & 3D Slot Holder System Built Successfully!");
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
            // ScreenSpaceCamera (not Overlay) so the canvas is depth-tested against the 3D scene:
            // docked mini objects (placed nearer than uiPlaneDistance) draw in front of the slot art
            // instead of being unconditionally hidden behind it, as Overlay mode would force.
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
        }

        /// Without an EventSystem in the scene, GraphicRaycaster has nothing to route pointer
        /// events through and UI Button.onClick never fires — nothing in this project's scene
        /// setup was creating one.
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
            bg.color = new Color(0.10f, 0.14f, 0.20f, 0.92f);

            GameObject titleObj = new GameObject("Level_Badge");
            titleObj.transform.SetParent(headerObj.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 0);
            titleRect.sizeDelta = new Vector2(240, 45);

            Image titleBg = titleObj.AddComponent<Image>();
            titleBg.color = new Color(0.9f, 0.65f, 0.15f, 0.95f);

            Text titleTxt = CreateTextNode(titleObj.transform, "SEVİYE 1", 22, FontStyle.Bold, Color.black);
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
            dockRect.anchoredPosition = new Vector2(0, 80);
            dockRect.sizeDelta = new Vector2(1000, 200);

            Image bg = dockObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.24f, 0.94f);

            GameObject slotsContainerObj = new GameObject("Slots_Container");
            slotsContainerObj.transform.SetParent(dockObj.transform, false);

            bottomDockContainer = slotsContainerObj.AddComponent<RectTransform>();
            bottomDockContainer.anchorMin = Vector2.zero;
            bottomDockContainer.anchorMax = Vector2.one;
            bottomDockContainer.sizeDelta = new Vector2(-40, 0);

            HorizontalLayoutGroup layout = slotsContainerObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 15, 15);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            slotRects.Clear();
            slotBadgeTexts.Clear();
            dockedItems.Clear();

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                GameObject slotObj = new GameObject($"DockSlot_{i}");
                slotObj.transform.SetParent(slotsContainerObj.transform, false);

                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                Image slotBg = slotObj.AddComponent<Image>();
                slotBg.color = new Color(0.22f, 0.28f, 0.38f, 0.85f);

                // Slot Inset Border Frame
                GameObject borderObj = new GameObject("BorderFrame");
                borderObj.transform.SetParent(slotObj.transform, false);

                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.sizeDelta = new Vector2(-6, -6);

                Image borderImg = borderObj.AddComponent<Image>();
                borderImg.color = new Color(0.35f, 0.45f, 0.60f, 0.5f);

                GameObject labelObj = new GameObject("LabelText");
                labelObj.transform.SetParent(slotObj.transform, false);

                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;

                Text labelTxt = labelObj.AddComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.fontSize = 20;
                labelTxt.fontStyle = FontStyle.Bold;
                labelTxt.alignment = TextAnchor.MiddleCenter;
                labelTxt.color = new Color(0.6f, 0.7f, 0.85f, 0.4f);
                labelTxt.text = $"{i + 1}";

                slotRects.Add(slotRect);
                slotBadgeTexts.Add(labelTxt);
            }

            GameObject boxObj = new GameObject("Delivery_Box_Target");
            boxObj.transform.SetParent(dockObj.transform, false);

            rightDeliveryBoxRect = boxObj.AddComponent<RectTransform>();
            rightDeliveryBoxRect.anchorMin = new Vector2(1f, 0.5f);
            rightDeliveryBoxRect.anchorMax = new Vector2(1f, 0.5f);
            rightDeliveryBoxRect.pivot = new Vector2(0.5f, 0.5f);
            rightDeliveryBoxRect.anchoredPosition = new Vector2(170, 0);
            rightDeliveryBoxRect.sizeDelta = new Vector2(130, 130);

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.95f, 0.65f, 0.2f, 0.95f);

            Text boxTxt = CreateTextNode(boxObj.transform, "📦", 48, FontStyle.Bold, Color.white);
            boxTxt.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildShuffleButton(Transform parent)
        {
            GameObject btnObj = new GameObject("Shuffle_Button");
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(0f, 0f);
            btnRect.pivot = new Vector2(0f, 0f);
            btnRect.anchoredPosition = new Vector2(30, 290);
            btnRect.sizeDelta = new Vector2(220, 60);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.18f, 0.55f, 0.9f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                PhysicsObjectSpawner spawner = Object.FindFirstObjectByType<PhysicsObjectSpawner>();
                if (spawner != null) spawner.GatherAndReshuffleRemaining();
            });

            Text btnTxt = CreateTextNode(btnObj.transform, "🔄 KARIŞTIR", 20, FontStyle.Bold, Color.white);
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

            foreach (MatchGoal goal in goals)
            {
                GameObject cardObj = new GameObject($"GoalCard_{goal.colorName}_{goal.shapeType}");
                cardObj.transform.SetParent(topGoalContainer, false);

                RectTransform cardRect = cardObj.AddComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(400, 90);

                Image cardBg = cardObj.AddComponent<Image>();
                cardBg.color = new Color(goal.targetColor.r * 0.5f, goal.targetColor.g * 0.5f, goal.targetColor.b * 0.5f, 0.9f);

                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(cardObj.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(15, 0);
                iconRect.sizeDelta = new Vector2(60, 60);

                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.color = goal.targetColor;

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
            if (dockedItems.Count >= MAX_SLOTS || isProcessingMatch || item == null)
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

            Collider col = item.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            DockItemData data = new DockItemData
            {
                targetObject = item,
                shapeType = item.shapeType,
                colorName = item.colorName,
                objectColor = item.objectColor
            };

            int insertIndex = FindBestInsertIndex(data);
            dockedItems.Insert(insertIndex, data);

            Animate3DObjectToSlot(item.gameObject, insertIndex, CheckAndProcessDockMatches);

            UpdateSlotBadgesUI();
            return true;
        }

        private int FindBestInsertIndex(DockItemData newItem)
        {
            for (int i = dockedItems.Count - 1; i >= 0; i--)
            {
                if (dockedItems[i].Matches(newItem))
                {
                    return i + 1;
                }
            }
            return dockedItems.Count;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (slotIndex < 0 || slotIndex >= slotRects.Count || mainCamera == null) return Vector3.zero;

            // Use the rect's true visual center (TransformPoint of rect.center) rather than
            // RectTransform.position, which is the pivot and may not sit in the middle of the slot.
            RectTransform rect = slotRects[slotIndex];
            Vector3 slotWorldCenter = rect.TransformPoint(rect.rect.center);

            // RectTransform.position is a real WORLD position under ScreenSpaceCamera (not a pixel
            // coordinate like it was under Overlay), so it must go through WorldToScreenPoint first.
            Vector2 screenPos2D = RectTransformUtility.WorldToScreenPoint(CanvasEventCamera(), slotWorldCenter);
            Vector3 screenPoint = new Vector3(screenPos2D.x, screenPos2D.y, dockCameraDepth);
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }

        /// Measures the slot's actual on-screen size and reprojects it into world units at
        /// dockCameraDepth, so the docked item's scale always visually fits the slot art,
        /// regardless of resolution, device aspect ratio, or slot size tuning.
        private float ComputeFitScaleForSlot(int slotIndex)
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
            return slotWorldSize * dockItemFillRatio;
        }

        private Camera CanvasEventCamera()
        {
            return (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? mainCanvas.worldCamera : null;
        }

        private void Animate3DObjectToSlot(GameObject obj3D, int targetSlotIndex, System.Action onComplete)
        {
            if (obj3D == null) return;

            const float duration = 0.42f;
            Vector3 targetPos = GetSlotWorldPosition(targetSlotIndex);
            Vector3 targetScale = Vector3.one * ComputeFitScaleForSlot(targetSlotIndex);

            tweeningDockObjects.Add(obj3D);
            obj3D.transform.DOKill();

            Sequence seq = DOTween.Sequence();
            // Falling feel: accelerate into the slot like gravity (Ease.InQuad), then a small
            // squash-bounce on the landing scale instead of an up-and-over toss arc.
            seq.Join(obj3D.transform.DOMove(targetPos, duration).SetEase(Ease.InQuad));
            seq.Join(obj3D.transform.DOScale(targetScale, duration).SetEase(Ease.OutBounce));
            seq.Join(obj3D.transform.DORotateQuaternion(Quaternion.Euler(15f, 25f, 0f), duration).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                tweeningDockObjects.Remove(obj3D);
                onComplete?.Invoke();
            });
        }

        private void AlignDocked3DObjectsWithSlots()
        {
            for (int i = 0; i < dockedItems.Count; i++)
            {
                DockItemData data = dockedItems[i];
                if (data != null && data.targetObject != null && !tweeningDockObjects.Contains(data.targetObject.gameObject))
                {
                    Vector3 targetWorldPos = GetSlotWorldPosition(i);
                    data.targetObject.transform.position = Vector3.Lerp(data.targetObject.transform.position, targetWorldPos, Time.deltaTime * 22f);
                    data.targetObject.transform.localScale = Vector3.one * ComputeFitScaleForSlot(i);
                    data.targetObject.transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.Self);
                }
            }
        }

        private void UpdateSlotBadgesUI()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (i < dockedItems.Count)
                {
                    slotBadgeTexts[i].text = "";
                }
                else
                {
                    slotBadgeTexts[i].text = $"{i + 1}";
                }
            }
        }

        private void CheckAndProcessDockMatches()
        {
            if (isProcessingMatch) return;

            for (int i = 0; i <= dockedItems.Count - 3; i++)
            {
                if (dockedItems[i].Matches(dockedItems[i + 1]) && dockedItems[i].Matches(dockedItems[i + 2]))
                {
                    ProcessMatchThree3DBoxing(i);
                    return;
                }
            }
        }

        private void ProcessMatchThree3DBoxing(int startIndex)
        {
            isProcessingMatch = true;

            List<DockItemData> matchedGroup = new List<DockItemData>
            {
                dockedItems[startIndex],
                dockedItems[startIndex + 1],
                dockedItems[startIndex + 2]
            };

            dockedItems.RemoveRange(startIndex, 3);

            if (MatchGoalManager.Instance != null)
            {
                MatchGoalManager.Instance.RegisterMatchedItem(matchedGroup[0].shapeType, matchedGroup[0].colorName);
                RefreshTargetGoalsUI();
            }

            Vector3 boxWorldCenter = rightDeliveryBoxRect.TransformPoint(rightDeliveryBoxRect.rect.center);
            Vector2 rightBoxScreenPos2D = RectTransformUtility.WorldToScreenPoint(CanvasEventCamera(), boxWorldCenter);
            Vector3 rightBoxWorldTarget = mainCamera.ScreenToWorldPoint(new Vector3(rightBoxScreenPos2D.x, rightBoxScreenPos2D.y, dockCameraDepth));

            const float duration = 0.45f;
            Sequence flightSeq = DOTween.Sequence();

            foreach (DockItemData item in matchedGroup)
            {
                if (item.targetObject == null) continue;

                Transform itemTransform = item.targetObject.transform;
                tweeningDockObjects.Add(itemTransform.gameObject);
                itemTransform.DOKill();

                flightSeq.Join(itemTransform.DOJump(rightBoxWorldTarget, 1.0f, 1, duration).SetEase(Ease.InSine));
                flightSeq.Join(itemTransform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));
            }

            flightSeq.OnComplete(() =>
            {
                foreach (DockItemData item in matchedGroup)
                {
                    if (item.targetObject != null)
                    {
                        tweeningDockObjects.Remove(item.targetObject.gameObject);
                        Destroy(item.targetObject.gameObject);
                    }
                }

                UpdateSlotBadgesUI();
                isProcessingMatch = false;
                CheckAndProcessDockMatches();
            });
        }

        private Text CreateTextNode(Transform parent, string defaultContent, int fontSize, FontStyle style, Color textColor)
        {
            GameObject txtObj = new GameObject("TextNode");
            txtObj.transform.SetParent(parent, false);

            RectTransform rect = txtObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = textColor;
            txt.text = defaultContent;

            return txt;
        }
    }
}
