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
        [SerializeField] private int activeOrderCount = 3;

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

        public void SetupCustomerOrders()
        {
            activeOrders.Clear();
            pendingOrders.Clear();
            nextOrderId = 0;

            BuildOrderQueue();

            for (int i = 0; i < Mathf.Max(1, activeOrderCount); i++)
            {
                activeOrders.Add(pendingOrders.Count > 0 ? pendingOrders.Dequeue() : null);
            }
        }

        private void BuildOrderQueue()
        {
            var built = new List<CustomerOrder>();
            LevelDataSO levelData = LevelManager.Instance != null ? LevelManager.Instance.ActiveLevelData : null;

            if (levelData != null && levelData.targetGoals != null && levelData.targetGoals.Count > 0)
            {
                foreach (var goal in levelData.targetGoals)
                {
                    if (goal == null || goal.itemData == null) continue;
                    SplitGoalIntoOrders(built, goal.itemData.GetEffectiveItemId(), goal.itemData.targetColor, goal.itemData.icon, goal.requiredCount);
                }
            }

            if (built.Count == 0)
            {
                string[] sampleItems = { "watermelon", "pear", "sausage", "fish" };
                Color[] sampleColors = { Color.red, Color.green, new Color(0.8f, 0.4f, 0.2f), Color.cyan };
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
            int minSize = Mathf.Max(1, orderSizeRange.x);
            int maxSize = Mathf.Max(minSize, orderSizeRange.y);

            int remaining = Mathf.Max(1, totalRequired);
            while (remaining > 0)
            {
                int batch;
                if (remaining <= maxSize)
                {
                    batch = remaining;
                }
                else
                {
                    // Never leave a tail smaller than minSize behind: splitting 4 into 3+1 produced a
                    // one-item order, which completes the instant it is tapped and reads as a bug.
                    batch = Random.Range(minSize, maxSize + 1);
                    batch = Mathf.Max(minSize, Mathf.Min(batch, remaining - minSize));
                }

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
