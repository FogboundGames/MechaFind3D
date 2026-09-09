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

        [Tooltip("Pozun kaydedildiği pivot (objenin hangi yüzeyine yaslanıyordu). Aşağıdaki offset ve " +
                 "rotasyon değerleri bu pivota göre anlamlı - pivot değişirse aynı sayılar bambaşka bir " +
                 "yere/yöne gider, o yüzden pozun ayrılmaz parçası.")]
        public MechaPivotSelection targetPivot = MechaPivotSelection.PivotTop;

        [Tooltip("Pozun ayarlandığı andaki host objesinin dünya boyu. Offset ve mecha boyu mutlak birim " +
                 "olduğu için, farklı foodTargetSize'a sahip bir seviyede aynı sayılar farklı görünür - " +
                 "bu değer sayesinde poz oraya oranlanarak taşınır. 0 = bilinmiyor (eski şablonlar).")]
        public float authoredHostSize = 0f;

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
        ///
        /// The pivot goes across with the rest. It decides which face of the host the mecha is laid
        /// against and, through the surface normal, the base rotation everything else is measured from -
        /// and the offset below is applied along the mecha's OWN axes
        /// (ChameleonCamouflage.EmbedMechaInHostObject), so carrying the numbers without the pivot they
        /// were authored under lands the mecha somewhere else entirely.
        /// </summary>
        public void ApplyTo(MechaSpawnEntry entry, float targetHostSize = 0f)
        {
            if (entry == null) return;

            // Offset and mecha size are ABSOLUTE world units, but the host they were authored against is
            // sized by the level's foodTargetSize - so the identical numbers sit differently on a level
            // whose host is 1.10 wide than on the 1.20 one the pose was tuned on. Re-scale both by the
            // size ratio and the pose reproduces proportionally, which is what "restore what I saved"
            // has to mean. Presets saved before this was recorded carry 0 and are left untouched.
            float sizeScale = GetHostSizeScale(targetHostSize);

            entry.targetPivot = targetPivot;
            entry.mechaScaleRatio = mechaScaleRatio;   // already host-relative, nothing to scale
            entry.mechaWrapAmount = mechaWrapAmount;
            entry.mechaWorldSize = mechaWorldSize * sizeScale;
            entry.mechaOpacity = mechaOpacity;
            entry.mechaLocalOffset = mechaLocalOffset * sizeScale;
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
        /// Ratio to re-scale absolute pose values by when this preset is applied on a host of a different
        /// size than the one it was authored on. 1 (no change) whenever either size is unknown.
        /// </summary>
        public float GetHostSizeScale(float targetHostSize)
        {
            if (authoredHostSize <= 1e-4f || targetHostSize <= 1e-4f) return 1f;
            return targetHostSize / authoredHostSize;
        }

        /// <summary>
        /// Copies pose and bone overrides from a mecha spawn entry into this preset.
        /// </summary>
        public void CopyFrom(MechaSpawnEntry entry, ItemDataSO hostItem = null, float hostSize = 0f)
        {
            if (entry == null) return;

            targetHostItem = hostItem != null ? hostItem : entry.hostItemSO;
            targetPivot = entry.targetPivot;
            authoredHostSize = hostSize;
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
