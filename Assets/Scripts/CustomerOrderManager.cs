using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// One customer's request: a single item type and how many of it they want. The dock delivers a whole
    /// group at once, so an order never mixes types.
    /// </summary>
    [System.Serializable]
    public class CustomerOrder
    {
        public int orderId;
        public string customerName;
        public string itemId;
        public Color itemColor;
        public Sprite itemIcon;
        public int requiredCount;
        public bool isCompleted;
    }

    /// <summary>
    /// Keeps a small row of active orders on screen and refills it from a queue as each one is delivered.
    ///
    /// [ExecuteAlways] to match <see cref="CanvasUIDesignManager"/>, which builds the whole HUD in edit mode.
    /// Without it this component's Awake never runs outside Play, <c>Instance</c> stays null, and the order
    /// card row is silently empty in the Editor - so card layout could never be seen or tuned there.
    /// </summary>
    [ExecuteAlways]
    public class CustomerOrderManager : MonoBehaviour
    {
        private static CustomerOrderManager instance;

        /// <summary>
        /// Falls back to a scene lookup instead of relying purely on Awake. In edit mode Awake does not run
        /// when this component is added via AddComponent, so the HUD - which [ExecuteAlways] builds in the
        /// Editor - found a null manager and drew an empty order row.
        /// </summary>
        public static CustomerOrderManager Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<CustomerOrderManager>(FindObjectsInactive.Include);
                return instance;
            }
            private set => instance = value;
        }

        [Tooltip("Kaç sipariş kartı aynı anda üstte dursun.")]
        [Min(1)]
        [SerializeField] private int activeOrderCount = 4;

        [Tooltip("Bir siparişin isteyebileceği en az / en çok adet. Seviye hedefi bu aralıkta parçalara bölünür.")]
        [SerializeField] private Vector2Int orderSizeRange = new Vector2Int(2, 3);

        // Fixed length == activeOrderCount. A null entry means the queue ran dry and that card slot stays
        // empty rather than collapsing the row, so the remaining cards don't jump sideways mid-level.
        private readonly List<CustomerOrder> activeOrders = new List<CustomerOrder>();
        private readonly Queue<CustomerOrder> pendingOrders = new Queue<CustomerOrder>();
        private int nextOrderId;

        public IReadOnlyList<CustomerOrder> ActiveOrders => activeOrders;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            if (!Application.isPlaying)
            {
                SetupCustomerOrders();
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
            SetupCustomerOrders();
            if (CanvasUIDesignManager.Instance != null)
            {
                CanvasUIDesignManager.Instance.RefreshTargetGoalsUI();
            }
        }

        [ContextMenu("Setup Customer Orders")]
        public void SetupOrdersContextMenu()
        {
            SetupCustomerOrders();
            if (CanvasUIDesignManager.Instance != null)
            {
                CanvasUIDesignManager.Instance.RefreshTargetGoalsUI();
            }
        }
#endif

        public void SetupCustomerOrders()
        {
            activeOrders.Clear();
            pendingOrders.Clear();
            nextOrderId = 0;

            BuildOrderQueue();

            if (activeOrderCount < 4) activeOrderCount = 4;

            for (int i = 0; i < activeOrderCount; i++)
            {
                activeOrders.Add(pendingOrders.Count > 0 ? pendingOrders.Dequeue() : null);
            }
        }

        private void BuildOrderQueue()
        {
            var built = new List<CustomerOrder>();
            LevelDataSO levelData = LevelManager.Instance != null ? LevelManager.Instance.ActiveLevelData : null;

            Dictionary<string, (Color color, Sprite icon, int count)> itemMap = new Dictionary<string, (Color, Sprite, int)>();

            // 1. Primary Source: Exact LevelDataSO items spawned in the physics pile for this level
            if (levelData != null)
            {
                List<ItemDataSO> spawnItems = levelData.BuildSpawnItemList();
                if (spawnItems != null && spawnItems.Count > 0)
                {
                    foreach (var item in spawnItems)
                    {
                        if (item == null) continue;
                        string id = item.GetEffectiveItemId();
                        if (itemMap.ContainsKey(id))
                        {
                            var prev = itemMap[id];
                            itemMap[id] = (prev.color, prev.icon, prev.count + 1);
                        }
                        else
                        {
                            itemMap[id] = (item.targetColor, item.icon, 1);
                        }
                    }
                }
            }

            // 2. Secondary Source: If no LevelDataSO items exist, check actual objects spawned in scene
            if (itemMap.Count == 0)
            {
                FindTargetObject[] sceneItems = Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
                if (sceneItems != null && sceneItems.Length > 0)
                {
                    foreach (var targetObj in sceneItems)
                    {
                        if (targetObj == null || targetObj.isDocked) continue;
                        string id = targetObj.colorName;
                        if (string.IsNullOrEmpty(id)) continue;
                        if (itemMap.ContainsKey(id))
                        {
                            var prev = itemMap[id];
                            itemMap[id] = (prev.color, prev.icon, prev.count + 1);
                        }
                        else
                        {
                            itemMap[id] = (targetObj.objectColor, null, 1);
                        }
                    }
                }
            }

            // 3. Fallback: Check PhysicsObjectSpawner food models or ItemDataSO assets so all cards have valid 3D prefabs
            if (itemMap.Count == 0)
            {
                PhysicsObjectSpawner spawner = Object.FindFirstObjectByType<PhysicsObjectSpawner>();
                if (spawner != null && spawner.foodModels != null && spawner.foodModels.Length > 0)
                {
                    foreach (var model in spawner.foodModels)
                    {
                        if (model == null) continue;
                        string id = model.name.ToLowerInvariant();
                        if (!itemMap.ContainsKey(id))
                        {
                            itemMap[id] = (Color.white, null, 3);
                        }
                    }
                }
            }

            // 4. Emergency fallback if no items found anywhere: use valid watermelon/apple/pear items from project
            if (itemMap.Count == 0)
            {
                string[] validProjectItems = { "watermelon_001", "watermelon_002", "watermelon_003", "apple_001", "pear_001" };
                Color[] validColors = { Color.red, Color.red, Color.red, Color.red, Color.green };
                for (int i = 0; i < validProjectItems.Length; i++)
                {
                    itemMap[validProjectItems[i]] = (validColors[i], null, 3);
                }
            }

            // 4. Split all items present in the level into exact triplet customer orders
            foreach (var kvp in itemMap)
            {
                SplitGoalIntoOrders(built, kvp.Key, kvp.Value.color, kvp.Value.icon, kvp.Value.count);
            }

            // 5. Shuffle customer orders so variety is displayed across active slots
            for (int i = built.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (built[i], built[j]) = (built[j], built[i]);
            }

            // 6. Enqueue numbered orders
            foreach (CustomerOrder order in built)
            {
                order.orderId = ++nextOrderId;
                order.customerName = $"Müşteri #{order.orderId}";
                pendingOrders.Enqueue(order);
            }
        }

        private void SplitGoalIntoOrders(List<CustomerOrder> into, string itemId, Color color, Sprite icon, int totalRequired)
        {
            int remaining = Mathf.Max(3, totalRequired);
            while (remaining > 0)
            {
                int batch = Mathf.Min(3, remaining);
                into.Add(new CustomerOrder
                {
                    itemId = itemId,
                    itemColor = color,
                    itemIcon = icon,
                    requiredCount = batch
                });
                remaining -= batch;
            }
        }

        /// <summary>The active order that wants this item type, or null when nothing on screen asks for it.</summary>
        public CustomerOrder FindOrderForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            foreach (CustomerOrder order in activeOrders)
            {
                if (order == null || order.isCompleted) continue;
                if (order.itemId.Equals(itemId, System.StringComparison.OrdinalIgnoreCase)) return order;
            }
            return null;
        }

        /// <summary>Retires a delivered order and slides the next queued customer into the same card position.</summary>
        public void CompleteOrder(CustomerOrder order)
        {
            if (order == null) return;

            order.isCompleted = true;

            int index = activeOrders.IndexOf(order);
            if (index < 0) return;

            activeOrders[index] = pendingOrders.Count > 0 ? pendingOrders.Dequeue() : null;
        }
    }
}
