using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    [System.Serializable]
    public class CustomerOrderItem
    {
        public string itemId;
        public Color itemColor;
        public Sprite itemIcon;
        public int requiredCount;
        public int packedCount;

        public bool IsFulfilled => packedCount >= requiredCount;
    }

    [System.Serializable]
    public class CustomerOrder
    {
        public string customerName;
        public int assignedBoxIndex = -1; // 0 for Box 1, 1 for Box 2
        public List<CustomerOrderItem> items = new List<CustomerOrderItem>();
        public bool isCompleted = false;

        public bool IsFullyFulfilled
        {
            get
            {
                if (items == null || items.Count == 0) return false;
                foreach (var item in items)
                {
                    if (!item.IsFulfilled) return false;
                }
                return true;
            }
        }
    }

    public class CustomerOrderManager : MonoBehaviour
    {
        public static CustomerOrderManager Instance { get; private set; }

        [Header("Active Customer Orders Queue")]
        public List<CustomerOrder> levelOrders = new List<CustomerOrder>();
        public CustomerOrder box0Order;
        public CustomerOrder box1Order;

        private void Awake()
        {
            Instance = this;
        }

        public void SetupCustomerOrders()
        {
            levelOrders.Clear();
            box0Order = null;
            box1Order = null;

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                if (levelData.targetGoals != null && levelData.targetGoals.Count > 0)
                {
                    int customerCounter = 1;
                    foreach (var goalReq in levelData.targetGoals)
                    {
                        if (goalReq == null || goalReq.itemData == null) continue;

                        string itemId = goalReq.itemData.GetEffectiveItemId();
                        int totalReq = goalReq.requiredCount;

                        // Create orders of 2 or 3 items per customer
                        int remaining = totalReq;
                        while (remaining > 0)
                        {
                            int batchCount = Mathf.Min(remaining, Random.Range(2, 4));
                            CustomerOrder order = new CustomerOrder
                            {
                                customerName = $"Müşteri #{customerCounter++}"
                            };
                            order.items.Add(new CustomerOrderItem
                            {
                                itemId = itemId,
                                itemColor = goalReq.itemData.targetColor,
                                itemIcon = (goalReq.itemData.icon != null) ? goalReq.itemData.icon : null,
                                requiredCount = batchCount,
                                packedCount = 0
                            });

                            levelOrders.Add(order);
                            remaining -= batchCount;
                        }
                    }
                }
            }

            // Fallback orders if levelData has no targetGoals
            if (levelOrders.Count == 0)
            {
                GenerateFallbackOrders();
            }

            AssignOrdersToBoxes();
        }

        private void GenerateFallbackOrders()
        {
            string[] sampleItems = { "watermelon", "pear", "sausage", "fish" };
            Color[] sampleColors = { Color.red, Color.green, new Color(0.8f, 0.4f, 0.2f), Color.cyan };

            for (int i = 0; i < 4; i++)
            {
                CustomerOrder order = new CustomerOrder
                {
                    customerName = $"Müşteri #{i + 1}"
                };
                order.items.Add(new CustomerOrderItem
                {
                    itemId = sampleItems[i % sampleItems.Length],
                    itemColor = sampleColors[i % sampleColors.Length],
                    requiredCount = Random.Range(2, 4),
                    packedCount = 0
                });
                levelOrders.Add(order);
            }
        }

        public void AssignOrdersToBoxes()
        {
            if (box0Order == null)
            {
                box0Order = GetNextUnassignedOrder();
                if (box0Order != null) box0Order.assignedBoxIndex = 0;
            }

            if (box1Order == null)
            {
                box1Order = GetNextUnassignedOrder();
                if (box1Order != null) box1Order.assignedBoxIndex = 1;
            }
        }

        private CustomerOrder GetNextUnassignedOrder()
        {
            foreach (var order in levelOrders)
            {
                if (!order.isCompleted && order.assignedBoxIndex == -1)
                {
                    return order;
                }
            }
            return null;
        }

        public int GetTargetBoxForCollectedItem(string itemId)
        {
            // First check Box 0
            if (box0Order != null && !box0Order.isCompleted)
            {
                foreach (var item in box0Order.items)
                {
                    if (item.itemId == itemId && !item.IsFulfilled)
                    {
                        return 0;
                    }
                }
            }

            // Next check Box 1
            if (box1Order != null && !box1Order.isCompleted)
            {
                foreach (var item in box1Order.items)
                {
                    if (item.itemId == itemId && !item.IsFulfilled)
                    {
                        return 1;
                    }
                }
            }

            // Neither active order wants this item - the caller treats this as a wrong pick rather than
            // stuffing it into whichever box happens to be free.
            return -1;
        }

        /// <summary>
        /// Whether some currently active box order still wants this item, independent of whether that box
        /// has room for it right now (e.g. mid-shipping). Lets the caller tell a genuinely wrong item apart
        /// from a right item that simply has nowhere to land this frame.
        /// </summary>
        public bool ItemMatchesAnyActiveOrder(string itemId)
        {
            return GetTargetBoxForCollectedItem(itemId) != -1;
        }

        public void RegisterItemPackedIntoBox(int boxIndex, string itemId)
        {
            CustomerOrder targetOrder = (boxIndex == 0) ? box0Order : box1Order;
            if (targetOrder == null) return;

            foreach (var item in targetOrder.items)
            {
                if (item.itemId == itemId && !item.IsFulfilled)
                {
                    item.packedCount++;
                    break;
                }
            }

            if (targetOrder.IsFullyFulfilled)
            {
                targetOrder.isCompleted = true;
            }
        }

        public bool IsOrderFulfilledForBox(int boxIndex)
        {
            CustomerOrder targetOrder = (boxIndex == 0) ? box0Order : box1Order;
            if (targetOrder == null) return false;
            return targetOrder.IsFullyFulfilled;
        }

        public void OnBoxShipped(int boxIndex)
        {
            if (boxIndex == 0 && box0Order != null)
            {
                box0Order.isCompleted = true;
                box0Order = null;
            }
            else if (boxIndex == 1 && box1Order != null)
            {
                box1Order.isCompleted = true;
                box1Order = null;
            }

            AssignOrdersToBoxes();
        }
    }
}
