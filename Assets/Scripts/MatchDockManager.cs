using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Fallback Dock Data structures (Pure Canvas UI Docking is handled by CanvasUIDesignManager).
    /// </summary>
    public class MatchDockManager : MonoBehaviour
    {
        public static MatchDockManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
    }
}
