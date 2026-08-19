using DG.Tweening;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public enum ObjectShapeType
    {
        Cube,
        Sphere
    }

    /// <summary>
    /// Component attached to each interactable physics object in the pile.
    /// Stores shape type, color, and handles click/tap selection for the Search Game.
    /// </summary>
    public class FindTargetObject : MonoBehaviour
    {
        public ObjectShapeType shapeType;
        public Color objectColor;
        public string colorName;
        [Tooltip("Set true once this item has been collected into a dock slot, so a pile reshuffle leaves it alone.")]
        public bool isDocked;

        private Renderer objectRenderer;
        private Vector3 originalScale;

        public Vector3 OriginalScale => (originalScale != Vector3.zero ? originalScale : Vector3.one);

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            originalScale = transform.localScale;
        }

        public void Initialize(ObjectShapeType shape, Color color, string nameStr)
        {
            shapeType = shape;
            objectColor = color;
            colorName = nameStr;
            originalScale = transform.localScale;
        }

        public bool MatchesTarget(ObjectShapeType targetShape, string targetColorName)
        {
            return shapeType == targetShape && colorName.Equals(targetColorName, System.StringComparison.OrdinalIgnoreCase);
        }

        public void PlayFoundAnimation()
        {
            transform.DOKill();
            transform.localScale = originalScale;
            transform.DOPunchScale(originalScale * 0.4f, 0.5f, 8, 1f);
        }
    }
}
