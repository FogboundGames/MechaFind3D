using UnityEngine;

namespace MechaFind3D.UI
{
    public class UIRotator : MonoBehaviour
    {
        public Vector3 rotationSpeed = new Vector3(0, 45f, 0);

        void Update()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
