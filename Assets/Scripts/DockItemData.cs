using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public class DockItemData
    {
        public FindTargetObject targetObject;
        public ObjectShapeType shapeType;
        public string colorName;
        public Color objectColor;

        public Vector3 originalWorldScale = Vector3.one;
        public Vector3 originalPosition;
        public Quaternion originalRotation;

        public bool Matches(DockItemData other)
        {
            if (other == null) return false;
            return shapeType == other.shapeType && colorName.Equals(other.colorName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
