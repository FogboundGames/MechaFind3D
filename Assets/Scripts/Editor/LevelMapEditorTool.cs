using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MechaFind3D.PhysicsInteraction;

namespace MechaFind3D.EditorTools
{
    public static class LevelMapEditorTool
    {
        [MenuItem("Tools/MechaFind3D/Seviye Haritasi (Level Map Canvas) Sahneye Ekle", false, 50)]
        [MenuItem("GameObject/UI/MechaFind3D/Level Map Canvas", false, 10)]
        public static void CreateLevelMapCanvasInScene()
        {
            // Check if already exists in scene
            var existing = GameObject.Find("LevelMap_Canvas");
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Level Map Canvas Zaten Var",
                    "Sahnede zaten 'LevelMap_Canvas' isimli bir nesne var. Yeni bir tane daha oluşturulsun mu?",
                    "Evet, Yenisini Oluştur",
                    "Hayır, Mevcut Olanı Seç");

                if (!overwrite)
                {
                    Selection.activeGameObject = existing;
                    EditorGUIUtility.PingObject(existing);
                    return;
                }
            }

            // 1. Create Canvas Root
            GameObject canvasGO = new GameObject("LevelMap_Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Level Map Canvas");

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // Above gameplay UI

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Load UI Sprites
            Sprite starSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Star.png");
            Sprite starHollowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Star Hollow.png");
            Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Coin.png");
            Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Close.png");
            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 2. Background Panel (Vibrant Sky Blue matching user sketch)
            GameObject bgGO = CreateUIObject("Background", canvasGO.transform);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            StretchFull(bgRT);
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.01f, 0.62f, 0.95f, 1f); // #029EFA sky blue

            // 3. Header / Top Bar
            GameObject topBarGO = CreateUIObject("TopBar", canvasGO.transform);
            RectTransform topBarRT = topBarGO.GetComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0f, 1f);
            topBarRT.anchorMax = new Vector2(1f, 1f);
            topBarRT.pivot = new Vector2(0.5f, 1f);
            topBarRT.sizeDelta = new Vector2(0f, 160f);
            topBarRT.anchoredPosition = new Vector2(0f, -40f);

            // Close (Back) Button
            GameObject closeBtnGO = CreateUIObject("CloseButton", topBarGO.transform);
            RectTransform closeBtnRT = closeBtnGO.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0f, 0.5f);
            closeBtnRT.anchorMax = new Vector2(0f, 0.5f);
            closeBtnRT.pivot = new Vector2(0f, 0.5f);
            closeBtnRT.sizeDelta = new Vector2(90f, 90f);
            closeBtnRT.anchoredPosition = new Vector2(50f, 0f);
            Image closeBtnImg = closeBtnGO.AddComponent<Image>();
            if (closeSprite != null) closeBtnImg.sprite = closeSprite;
            closeBtnImg.color = Color.white;
            Button closeBtn = closeBtnGO.AddComponent<Button>();

            // Title "BÖLÜMLER"
            GameObject titleGO = CreateUIObject("TitleText", topBarGO.transform);
            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.5f);
            titleRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleRT.sizeDelta = new Vector2(400f, 80f);
            titleRT.anchoredPosition = Vector2.zero;
            Text titleTxt = titleGO.AddComponent<Text>();
            titleTxt.font = defaultFont;
            titleTxt.fontSize = 54;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            titleTxt.text = "BÖLÜMLER";

            // Coin Badge on Top-Right
            GameObject coinBadgeGO = CreateUIObject("CoinBadge", topBarGO.transform);
            RectTransform coinBadgeRT = coinBadgeGO.GetComponent<RectTransform>();
            coinBadgeRT.anchorMin = new Vector2(1f, 0.5f);
            coinBadgeRT.anchorMax = new Vector2(1f, 0.5f);
            coinBadgeRT.pivot = new Vector2(1f, 0.5f);
            coinBadgeRT.sizeDelta = new Vector2(220f, 75f);
            coinBadgeRT.anchoredPosition = new Vector2(-50f, 0f);
            Image coinBadgeBg = coinBadgeGO.AddComponent<Image>();
            coinBadgeBg.color = new Color(0f, 0f, 0f, 0.35f);

            GameObject coinIconGO = CreateUIObject("CoinIcon", coinBadgeGO.transform);
            RectTransform coinIconRT = coinIconGO.GetComponent<RectTransform>();
            coinIconRT.anchorMin = new Vector2(0f, 0.5f);
            coinIconRT.anchorMax = new Vector2(0f, 0.5f);
            coinIconRT.pivot = new Vector2(0f, 0.5f);
            coinIconRT.sizeDelta = new Vector2(60f, 60f);
            coinIconRT.anchoredPosition = new Vector2(10f, 0f);
            Image coinIconImg = coinIconGO.AddComponent<Image>();
            if (coinSprite != null) coinIconImg.sprite = coinSprite;

            GameObject coinTxtGO = CreateUIObject("CoinText", coinBadgeGO.transform);
            RectTransform coinTxtRT = coinTxtGO.GetComponent<RectTransform>();
            coinTxtRT.anchorMin = new Vector2(0f, 0f);
            coinTxtRT.anchorMax = new Vector2(1f, 1f);
            coinTxtRT.offsetMin = new Vector2(75f, 0f);
            coinTxtRT.offsetMax = new Vector2(-15f, 0f);
            Text coinTxt = coinTxtGO.AddComponent<Text>();
            coinTxt.font = defaultFont;
            coinTxt.fontSize = 38;
            coinTxt.fontStyle = FontStyle.Bold;
            coinTxt.alignment = TextAnchor.MiddleLeft;
            coinTxt.color = new Color(1f, 0.92f, 0.23f, 1f);
            coinTxt.text = "100";

            // 4. Nodes Container / Path Area
            GameObject nodesContainerGO = CreateUIObject("NodesContainer", canvasGO.transform);
            RectTransform nodesContainerRT = nodesContainerGO.GetComponent<RectTransform>();
            nodesContainerRT.anchorMin = new Vector2(0f, 0f);
            nodesContainerRT.anchorMax = new Vector2(1f, 1f);
            nodesContainerRT.offsetMin = new Vector2(40f, 260f);
            nodesContainerRT.offsetMax = new Vector2(-40f, -220f);

            // Node coordinates (zigzag path ascending from bottom to top)
            Vector2[] nodePositions = new Vector2[]
            {
                new Vector2(-140f, -480f), // Level 1
                new Vector2(140f, -240f),  // Level 2
                new Vector2(-120f, 40f),   // Level 3
                new Vector2(130f, 320f),   // Level 4
                new Vector2(0f, 580f)      // Level 5
            };

            List<GameObject> createdNodes = new List<GameObject>();

            for (int i = 0; i < nodePositions.Length; i++)
            {
                int levelNum = i + 1;
                Vector2 pos = nodePositions[i];

                // Node root
                GameObject nodeGO = CreateUIObject($"LevelNode_{levelNum}", nodesContainerGO.transform);
                RectTransform nodeRT = nodeGO.GetComponent<RectTransform>();
                nodeRT.sizeDelta = new Vector2(170f, 170f);
                nodeRT.anchoredPosition = pos;

                // Connecting dots leading from previous node to this node (matching sketch: 3 yellow dots)
                if (i > 0)
                {
                    Vector2 prevPos = nodePositions[i - 1];
                    GameObject dotsGroup = CreateUIObject("IncomingDots", nodeGO.transform);
                    RectTransform dotsGroupRT = dotsGroup.GetComponent<RectTransform>();
                    dotsGroupRT.sizeDelta = Vector2.zero;
                    dotsGroupRT.anchoredPosition = Vector2.zero;

                    int dotCount = 3;
                    for (int d = 1; d <= dotCount; d++)
                    {
                        float t = (float)d / (dotCount + 1);
                        Vector2 dotWorldPosInContainer = Vector2.Lerp(prevPos, pos, t);
                        Vector2 dotLocalToNode = dotWorldPosInContainer - pos;

                        GameObject dotGO = CreateUIObject($"PathDot_{d}", dotsGroup.transform);
                        RectTransform dotRT = dotGO.GetComponent<RectTransform>();
                        dotRT.sizeDelta = new Vector2(28f, 28f);
                        dotRT.anchoredPosition = dotLocalToNode;

                        Image dotImg = dotGO.AddComponent<Image>();
                        dotImg.color = (i == 1) ? new Color(1f, 0.88f, 0.15f, 1f) : new Color(0.35f, 0.5f, 0.65f, 0.5f);
                    }
                }

                // Star Image
                GameObject starImgGO = CreateUIObject("StarImage", nodeGO.transform);
                RectTransform starRT = starImgGO.GetComponent<RectTransform>();
                starRT.sizeDelta = new Vector2(150f, 150f);
                starRT.anchoredPosition = Vector2.zero;
                Image starImg = starImgGO.AddComponent<Image>();
                if (starSprite != null) starImg.sprite = starSprite;
                starImg.preserveAspect = true;

                if (i == 0) starImg.color = new Color(1f, 0.85f, 0.1f, 1f); // Completed
                else if (i == 1) starImg.color = new Color(1f, 0.95f, 0.3f, 1f); // Current
                else starImg.color = new Color(0.45f, 0.55f, 0.65f, 0.7f); // Locked

                // Level Number Text (inside/on star, exactly like sketch)
                GameObject numTxtGO = CreateUIObject("LevelText", nodeGO.transform);
                RectTransform numTxtRT = numTxtGO.GetComponent<RectTransform>();
                numTxtRT.sizeDelta = new Vector2(100f, 100f);
                numTxtRT.anchoredPosition = new Vector2(0f, -4f);
                Text numTxt = numTxtGO.AddComponent<Text>();
                numTxt.font = defaultFont;
                numTxt.fontSize = 62;
                numTxt.fontStyle = FontStyle.Bold;
                numTxt.alignment = TextAnchor.MiddleCenter;
                numTxt.color = Color.white;
                numTxt.text = levelNum.ToString();

                // Button
                Button btn = nodeGO.AddComponent<Button>();
                btn.targetGraphic = starImg;

                createdNodes.Add(nodeGO);
            }

            // 5. Bottom "BAŞLA / PLAY" Button (Matching user's green button style)
            GameObject bottomBarGO = CreateUIObject("BottomBar", canvasGO.transform);
            RectTransform bottomBarRT = bottomBarGO.GetComponent<RectTransform>();
            bottomBarRT.anchorMin = new Vector2(0.5f, 0f);
            bottomBarRT.anchorMax = new Vector2(0.5f, 0f);
            bottomBarRT.pivot = new Vector2(0.5f, 0f);
            bottomBarRT.sizeDelta = new Vector2(700f, 200f);
            bottomBarRT.anchoredPosition = new Vector2(0f, 60f);

            GameObject playBtnGO = CreateUIObject("PlayButton", bottomBarGO.transform);
            RectTransform playBtnRT = playBtnGO.GetComponent<RectTransform>();
            playBtnRT.sizeDelta = new Vector2(460f, 115f);
            playBtnRT.anchoredPosition = Vector2.zero;
            Image playBtnImg = playBtnGO.AddComponent<Image>();
            playBtnImg.color = new Color(0.42f, 0.77f, 0.17f, 1f); // Vibrant lime green #6CC52B

            Outline playBtnOutline = playBtnGO.AddComponent<Outline>();
            playBtnOutline.effectColor = new Color(0.12f, 0.35f, 0.05f, 1f);
            playBtnOutline.effectDistance = new Vector2(3f, -4f);

            Button playBtn = playBtnGO.AddComponent<Button>();

            GameObject playTxtGO = CreateUIObject("PlayText", playBtnGO.transform);
            RectTransform playTxtRT = playTxtGO.GetComponent<RectTransform>();
            StretchFull(playTxtRT);
            Text playTxt = playTxtGO.AddComponent<Text>();
            playTxt.font = defaultFont;
            playTxt.fontSize = 44;
            playTxt.fontStyle = FontStyle.Bold;
            playTxt.alignment = TextAnchor.MiddleCenter;
            playTxt.color = Color.white;
            playTxt.text = "BAŞLA";

            // 6. Attach & Setup LevelMapManager Component
            LevelMapManager mapManager = canvasGO.AddComponent<LevelMapManager>();

            // Connect references via SerializedObject
            SerializedObject so = new SerializedObject(mapManager);
            so.FindProperty("mapCanvasObject").objectReferenceValue = canvasGO;
            so.FindProperty("nodesContainer").objectReferenceValue = nodesContainerGO.transform;
            so.FindProperty("playButton").objectReferenceValue = playBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("coinCounterText").objectReferenceValue = coinTxt;
            so.ApplyModifiedProperties();

            mapManager.FindAndSetupNodes();

            // Set canvas initially active in scene so the user can inspect & edit it!
            canvasGO.SetActive(true);

            Selection.activeGameObject = canvasGO;
            EditorGUIUtility.PingObject(canvasGO);

            EditorUtility.SetDirty(canvasGO);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[MechaFind3D]</color> Level Map Canvas başarıyla sahneye eklendi!");
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            if (go.layer < 0) go.layer = 5;
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
