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

            if (levelData != null)
            {
                List<ItemDataSO> spawnItems = levelData.BuildSpawnItemList();
                if (spawnItems != null && spawnItems.Count > 0)
                {
                    Dictionary<ItemDataSO, int> itemCounts = new Dictionary<ItemDataSO, int>();
                    foreach (var item in spawnItems)
                    {
                        if (item == null) continue;
                        if (itemCounts.ContainsKey(item)) itemCounts[item]++;
                        else itemCounts[item] = 1;
                    }

                    foreach (var kvp in itemCounts)
                    {
                        ItemDataSO itemData = kvp.Key;
                        int totalRequired = kvp.Value;
                        SplitGoalIntoOrders(built, itemData.GetEffectiveItemId(), itemData.targetColor, itemData.icon, totalRequired);
                    }
                }
            }

            if (built.Count == 0)
            {
                string[] sampleItems = { "watermelon", "pear", "sausage", "fish", "onion", "banana" };
                Color[] sampleColors = { Color.red, Color.green, new Color(0.8f, 0.4f, 0.2f), Color.cyan, Color.magenta, Color.yellow };
                for (int i = 0; i < sampleItems.Length; i++)
                {
                    SplitGoalIntoOrders(built, sampleItems[i], sampleColors[i], null, 6);
                }
            }

            // Shuffled, because building goal by goal put every order for the first item type at the front -
            // so all three cards on screen asked for the same thing and the tray never had to hold anything
            // the player could not immediately deliver.
            for (int i = built.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (built[i], built[j]) = (built[j], built[i]);
            }

            // Numbered after the shuffle so the customers read 1, 2, 3... down the queue.
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
