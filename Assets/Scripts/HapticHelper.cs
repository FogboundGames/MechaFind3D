using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public static class HapticHelper
    {
        private const string PrefKey = "MechaFind3D_HapticsEnabled";

        private static bool? _cached;

        public static bool Enabled
        {
            get
            {
                _cached ??= PlayerPrefs.GetInt(PrefKey, 1) == 1;
                return _cached.Value;
            }
            set
            {
                _cached = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            }
        }

        public static void Vibrate()
        {
            if (Enabled) Handheld.Vibrate();
        }
    }
}
