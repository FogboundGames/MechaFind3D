using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    [CreateAssetMenu(fileName = "NewMechaPosePreset", menuName = "MechaFind3D/Mecha Pose Preset", order = 3)]
    public class MechaPosePresetSO : ScriptableObject
    {
        [Tooltip("Şablonun açıklayıcı adı (ör. Avokado Sarılma Pozu 1).")]
        public string presetName = "Yeni Poz Şablonu";

        [Tooltip("Bu poz şablonunun özel olarak tasarlandığı hedef obje (isteğe bağlı).")]
        public ItemDataSO targetHostItem;

        [Range(0.05f, 1.0f)]
        public float mechaScaleRatio = 0.25f;

        [Range(0f, 1f)]
        public float mechaWrapAmount = 0f;

        public float mechaWorldSize = 0.5f;

        [Range(0.1f, 1.0f)]
        public float mechaOpacity = 0.22f;

        public Vector3 mechaLocalOffset = Vector3.zero;

        public Vector3 mechaRotationOffset = new Vector3(90f, 0f, 0f);

        public List<MechaBoneOverride> boneOverrides = new List<MechaBoneOverride>();

        /// <summary>
        /// Applies this preset's pose and bone overrides to a mecha spawn entry.
        /// </summary>
        public void ApplyTo(MechaSpawnEntry entry)
        {
            if (entry == null) return;

            entry.mechaScaleRatio = mechaScaleRatio;
            entry.mechaWrapAmount = mechaWrapAmount;
            entry.mechaWorldSize = mechaWorldSize;
            entry.mechaOpacity = mechaOpacity;
            entry.mechaLocalOffset = mechaLocalOffset;
            entry.mechaRotationOffset = mechaRotationOffset;

            entry.boneOverrides = new List<MechaBoneOverride>();
            if (boneOverrides != null)
            {
                foreach (var ovr in boneOverrides)
                {
                    if (ovr != null && !string.IsNullOrEmpty(ovr.boneKeyword))
                    {
                        entry.boneOverrides.Add(new MechaBoneOverride
                        {
                            boneKeyword = ovr.boneKeyword,
                            rotationOffset = ovr.rotationOffset
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Copies pose and bone overrides from a mecha spawn entry into this preset.
        /// </summary>
        public void CopyFrom(MechaSpawnEntry entry, ItemDataSO hostItem = null)
        {
            if (entry == null) return;

            targetHostItem = hostItem != null ? hostItem : entry.hostItemSO;
            mechaScaleRatio = entry.mechaScaleRatio;
            mechaWrapAmount = entry.mechaWrapAmount;
            mechaWorldSize = entry.mechaWorldSize;
            mechaOpacity = entry.mechaOpacity;
            mechaLocalOffset = entry.mechaLocalOffset;
            mechaRotationOffset = entry.mechaRotationOffset;

            boneOverrides = new List<MechaBoneOverride>();
            if (entry.boneOverrides != null)
            {
                foreach (var ovr in entry.boneOverrides)
                {
                    if (ovr != null && !string.IsNullOrEmpty(ovr.boneKeyword))
                    {
                        boneOverrides.Add(new MechaBoneOverride
                        {
                            boneKeyword = ovr.boneKeyword,
                            rotationOffset = ovr.rotationOffset
                        });
                    }
                }
            }
        }
    }
}
