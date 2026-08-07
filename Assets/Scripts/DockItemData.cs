using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public class DockItemData
    {
        public FindTargetObject targetObject;
        public ObjectShapeType shapeType;
        public string colorName;
        public Color objectColor;

        public bool Matches(DockItemData other)
        {
            if (other == null) return false;
            return shapeType == other.shapeType && colorName.Equals(other.colorName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
