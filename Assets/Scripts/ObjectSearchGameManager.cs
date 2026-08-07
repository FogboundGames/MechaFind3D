using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Legacy Game Manager (State handled cleanly by MatchGoalManager & CanvasUIDesignManager).
    /// </summary>
    public class ObjectSearchGameManager : MonoBehaviour
    {
        public static ObjectSearchGameManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
    }
}
