using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    [CreateAssetMenu(fileName = "New_ItemData", menuName = "MechaFind3D/Item Data", order = 1)]
    public class ItemDataSO : ScriptableObject
    {
        [Header("Item Identification")]
        [Tooltip("Unique ID for this item (e.g. 'apple', 'banana', 'toy_car'). If empty, uses prefab name.")]
        public string itemId = "";

        [Tooltip("Human readable name for UI and editor display.")]
        public string displayName = "Yeni Obje";

        [Header("3D Model Prefab")]
        [Tooltip("The 3D GameObject prefab spawned in the level pile.")]
        public GameObject prefab;

        [Header("Visual Properties")]
        [Tooltip("Color tint used for UI badges and target matching fallback.")]
        public Color targetColor = new Color(0.9f, 0.2f, 0.2f);

        [Tooltip("Optional 2D UI icon sprite for level goal displays.")]
        public Sprite icon;

        [Header("Dock & Card Orientation")]
        [Tooltip("If enabled, overrides the default dock slot rotation for this item.")]
        public bool overrideDockRotation;
        public Vector3 dockRotationEuler;

        [Tooltip("If enabled, overrides the default order-card 3D icon rotation.")]
        public bool overrideCardRotation;
        public Vector3 cardRotationEuler;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId) && prefab != null)
            {
                itemId = prefab.name.ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(displayName) && prefab != null)
            {
                displayName = prefab.name;
            }
        }

        public string GetEffectiveItemId()
        {
            if (!string.IsNullOrEmpty(itemId)) return itemId;
            if (prefab != null) return prefab.name.ToLowerInvariant();
            return name.ToLowerInvariant();
        }
    }
}
