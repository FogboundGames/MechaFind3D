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
        [Header("Match Factory Container Box Dimensions")]
        [SerializeField] private Vector3 containerSize = new Vector3(8.0f, 0.4f, 8.0f);
        [SerializeField] private float borderWallHeight = 3.5f;
        [SerializeField] private float borderWallThickness = 0.5f;

        [Header("Match Factory Visual Colors")]
        [SerializeField] private Color trayFloorColor = new Color(0.12f, 0.15f, 0.20f);
        [SerializeField] private Color borderWallColor = new Color(0.22f, 0.26f, 0.35f);

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
            SetupCamera();
            SetupLighting();
            GameObject trayObj = CreateContainerTrayFloor();
            CreateContainerBorderWalls(trayObj.transform);
            CreateTopCeilingBarrier(trayObj.transform);
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

            cam.transform.position = new Vector3(0f, 9.5f, -6.5f);
            cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            cam.fieldOfView = 58f;
        }

        private void SetupLighting()
        {
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
            dirLight.shadows = LightShadows.Soft;
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

            Renderer rend = floorObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(shader)
                {
                    name = "TrayFloorMaterial"
                };

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", trayFloorColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", trayFloorColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);

                rend.sharedMaterial = mat;
            }

            BoxCollider boxCol = floorObj.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.sharedMaterial = new PhysicsMaterial("TrayFloorPhysics")
                {
                    dynamicFriction = 0.18f,
                    staticFriction = 0.22f,
                    bounciness = 0.1f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            return floorObj;
        }

        private void CreateContainerBorderWalls(Transform parent)
        {
            Transform existingWalls = transform.Find("Container_Border_Walls");
            if (existingWalls != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingWalls.gameObject);
#else
                Destroy(existingWalls.gameObject);
#endif
            }

            GameObject wallsContainer = new GameObject("Container_Border_Walls");
            wallsContainer.transform.SetParent(transform);

            float halfX = containerSize.x * 0.5f;
            float halfZ = containerSize.z * 0.5f;
            float posY = borderWallHeight * 0.5f;

            CreateWallNode(wallsContainer.transform, "Border_North", 
                new Vector3(0, posY, halfZ + borderWallThickness * 0.5f), 
                new Vector3(containerSize.x + borderWallThickness * 2, borderWallHeight, borderWallThickness));

            CreateWallNode(wallsContainer.transform, "Border_South", 
                new Vector3(0, posY, -halfZ - borderWallThickness * 0.5f), 
                new Vector3(containerSize.x + borderWallThickness * 2, borderWallHeight, borderWallThickness));

            CreateWallNode(wallsContainer.transform, "Border_East", 
                new Vector3(halfX + borderWallThickness * 0.5f, posY, 0), 
                new Vector3(borderWallThickness, borderWallHeight, containerSize.z));

            CreateWallNode(wallsContainer.transform, "Border_West", 
                new Vector3(-halfX - borderWallThickness * 0.5f, posY, 0), 
                new Vector3(borderWallThickness, borderWallHeight, containerSize.z));
        }

        private void CreateTopCeilingBarrier(Transform parent)
        {
            Transform existingRoof = transform.Find("Ceiling_Barrier");
            if (existingRoof != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingRoof.gameObject);
#else
                Destroy(existingRoof.gameObject);
#endif
            }

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Ceiling_Barrier";
            roof.transform.SetParent(transform);
            roof.transform.localPosition = new Vector3(0, borderWallHeight, 0);
            roof.transform.localScale = new Vector3(containerSize.x + 2f, 0.2f, containerSize.z + 2f);

            Renderer rend = roof.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            BoxCollider col = roof.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.sharedMaterial = new PhysicsMaterial("RoofPhysics")
                {
                    bounciness = 0.0f,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
        }

        private void CreateWallNode(Transform parent, string wallName, Vector3 localPos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;

            Renderer rend = wall.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(shader)
                {
                    name = "BorderWallMaterial"
                };

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", borderWallColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", borderWallColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);

                rend.sharedMaterial = mat;
            }

            BoxCollider col = wall.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.sharedMaterial = new PhysicsMaterial("BorderWallPhysics")
                {
                    dynamicFriction = 0.18f,
                    staticFriction = 0.22f,
                    bounciness = 0.1f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
        }

        private void SetupInteractionAndSpawner()
        {
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
        }
    }
}
