using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Automatic scene builder for Match Factory Style 3D Search & Canvas UI Game.
    /// Configures high-angle camera, clean tray container, CanvasUIDesignManager, MatchGoalManager,
    /// PhysicsObjectSpawner, and FingerPhysicsInteraction.
    /// Includes 1-click Editor Menu items:
    /// - Tools > Setup 3D Physics Scene
    /// - Tools > Build Canvas UI Design
    /// </summary>
    [ExecuteAlways]
    public class ScenePhysicsSetup : MonoBehaviour
    {
        [Header("Play Area Line Boundary (Adjustable in Inspector)")]
        [Tooltip("Adjustable boundary line dimensions. Objects are strictly kept inside this line by code constraint.")]
        [SerializeField] private Vector2 boundaryAreaSize = new Vector2(6.35f, 6.35f);
        [Tooltip("Line colour drawn around the play area so its edge reads clearly against the solid navy background.")]
        [SerializeField] private Color boundaryLineColor = new Color(0.85f, 0.92f, 1f, 0.9f);
        [SerializeField] private float boundaryLineWidth = 0.06f;

        [Header("Match Factory Floor Tray Dimensions")]
        [SerializeField] private Vector3 containerSize = new Vector3(8.5f, 0.1f, 8.5f);

        private void Start()
        {
            if (Application.isPlaying)
            {
                SetupSceneEnvironment();
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Setup 3D Physics Scene")]
        public static void CreateOrSetupScene()
        {
            GameObject setupObj = GameObject.Find("Physics_Scene_Controller");
            if (setupObj == null)
            {
                setupObj = new GameObject("Physics_Scene_Controller");
            }

            ScenePhysicsSetup setup = setupObj.GetComponent<ScenePhysicsSetup>();
            if (setup == null)
            {
                setup = setupObj.AddComponent<ScenePhysicsSetup>();
            }

            setup.SetupSceneEnvironment();
            Selection.activeGameObject = setupObj;
            Debug.Log("✅ Match Factory Canvas UI & Scene Setup Completed Successfully!");
        }
#endif

        [ContextMenu("Build Scene Environment Now")]
        public void SetupSceneEnvironment()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Physics.gravity = new Vector3(0f, -15.0f, 0f);
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.defaultContactOffset = 0.008f;

            SetupCamera();
            SetupLighting();
            GameObject floorObj = CreateContainerTrayFloor();
            RemoveOldWallsAndCeiling();
            CreateVisualBoundaryLineFrame(floorObj.transform);
            SetupInteractionAndSpawner();
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 11.2f, -7.6f);
            cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            cam.depth = 0;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.fieldOfView = 58f;
        }

        private void SetupLighting()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l != null) l.shadows = LightShadows.None;
            }

            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            dirLight.color = new Color(1f, 0.97f, 0.92f);
            dirLight.intensity = 1.4f;
            dirLight.shadows = LightShadows.None;
        }

        private GameObject CreateContainerTrayFloor()
        {
            Transform existingFloor = transform.Find("Container_Tray_Floor");
            if (existingFloor != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingFloor.gameObject);
#else
                Destroy(existingFloor.gameObject);
#endif
            }

            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Container_Tray_Floor";
            floorObj.transform.SetParent(transform);
            floorObj.transform.position = new Vector3(0f, -containerSize.y * 0.5f, 0f);
            floorObj.transform.localScale = containerSize;

            // The floor stays invisible - CanvasUIDesignManager.EnsureBackgroundCanvas draws the
            // actual background (image or fallback color) via a dedicated screen-space camera/canvas
            // instead, so this cube only needs to exist for its BoxCollider.
            Renderer rend = floorObj.GetComponent<Renderer>();
            if (rend != null) DestroyImmediate(rend);
            MeshFilter mf = floorObj.GetComponent<MeshFilter>();
            if (mf != null) DestroyImmediate(mf);

            BoxCollider boxCol = floorObj.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.sharedMaterial = new PhysicsMaterial("TrayFloorPhysics")
                {
                    dynamicFriction = 0.35f,
                    staticFriction = 0.40f,
                    bounciness = 0.0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            // Drop any textured background quad left over from an older build - the background is a flat
            // navy fill from CanvasUIDesignManager.EnsureBackgroundCanvas now, not an image plane in the
            // 3D scene, so this quad would otherwise keep showing behind everything.
            Transform existingVisual = floorObj.transform.Find("Visual_Background");
            if (existingVisual != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingVisual.gameObject);
#else
                Destroy(existingVisual.gameObject);
#endif
            }

            Transform existingVisual2 = transform.Find("Visual_Background");
            if (existingVisual2 != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingVisual2.gameObject);
#else
                Destroy(existingVisual2.gameObject);
#endif
            }

            return floorObj;
        }

        private void RemoveOldWallsAndCeiling()
        {
            Transform walls = transform.Find("Container_Border_Walls");
            if (walls != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(walls.gameObject);
#else
                Destroy(walls.gameObject);
#endif
            }

            Transform ceiling = transform.Find("Ceiling_Barrier");
            if (ceiling != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(ceiling.gameObject);
#else
                Destroy(ceiling.gameObject);
#endif
            }
        }

        private void CreateVisualBoundaryLineFrame(Transform parent)
        {
            Transform existingFrame = transform.Find("Boundary_Line_Frame");
            if (existingFrame != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingFrame.gameObject);
#else
                Destroy(existingFrame.gameObject);
#endif
            }

            GameObject lineObj = new GameObject("Boundary_Line_Frame");
            lineObj.transform.SetParent(transform);
            lineObj.transform.position = Vector3.zero;

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.enabled = true;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.numCornerVertices = 4;
            line.startWidth = boundaryLineWidth;
            line.endWidth = boundaryLineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material lineMat = new Material(lineShader);
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", boundaryLineColor);
            if (lineMat.HasProperty("_Color")) lineMat.SetColor("_Color", boundaryLineColor);
            line.sharedMaterial = lineMat;

            float halfX = boundaryAreaSize.x * 0.5f;
            float halfZ = boundaryAreaSize.y * 0.5f;
            float yPos = 0.02f;

            line.SetPosition(0, new Vector3(-halfX, yPos, -halfZ));
            line.SetPosition(1, new Vector3(halfX, yPos, -halfZ));
            line.SetPosition(2, new Vector3(halfX, yPos, halfZ));
            line.SetPosition(3, new Vector3(-halfX, yPos, halfZ));
        }



        private void SetupInteractionAndSpawner()
        {
            LevelManager levelManager = GetComponent<LevelManager>();
            if (levelManager == null)
            {
                levelManager = gameObject.AddComponent<LevelManager>();
            }
            levelManager.AutoFindLevelsIfEmpty();

            MatchGoalManager goalManager = GetComponent<MatchGoalManager>();
            if (goalManager == null)
            {
                goalManager = gameObject.AddComponent<MatchGoalManager>();
            }

            CanvasUIDesignManager canvasUIDesign = GetComponent<CanvasUIDesignManager>();
            if (canvasUIDesign == null)
            {
                canvasUIDesign = gameObject.AddComponent<CanvasUIDesignManager>();
            }
            canvasUIDesign.EnsureCanvasStructure();

            PhysicsObjectSpawner spawner = GetComponent<PhysicsObjectSpawner>();
            if (spawner == null)
            {
                spawner = gameObject.AddComponent<PhysicsObjectSpawner>();
            }

            FingerPhysicsInteraction interaction = GetComponent<FingerPhysicsInteraction>();
            if (interaction == null)
            {
                interaction = gameObject.AddComponent<FingerPhysicsInteraction>();
            }

            MechaRagdollSpawner mechaSpawner = GetComponent<MechaRagdollSpawner>();
            if (mechaSpawner == null)
            {
                mechaSpawner = gameObject.AddComponent<MechaRagdollSpawner>();
            }
            mechaSpawner.AutoFindCharacterModelsIfEmpty();
        }
    }
}
