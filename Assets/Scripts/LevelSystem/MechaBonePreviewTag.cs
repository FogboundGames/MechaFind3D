using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    [ExecuteAlways]
    public class MechaBonePreviewTag : MonoBehaviour
    {
        public LevelDataSO levelData;
        public int mechaIndex;
        public GameObject mechaInstance;
        public string selectedBoneKeyword;

        [System.Serializable]
        public class BoneData
        {
            public string keyword;
            public Transform boneTransform;
            public Quaternion baseLocalRotation;
        }

        public List<BoneData> boneDataList = new List<BoneData>();

        public bool isEditingRoot = false;

        public static MechaSpawnEntry GetMechaEntry(LevelDataSO level, int index)
        {
            if (level == null) return null;
            var entries = level.GetAllMechaEntries();
            if (entries != null && index >= 0 && index < entries.Count)
            {
                return entries[index];
            }
            return null;
        }

        public static List<MechaBoneOverride> GetBoneOverrides(LevelDataSO level, int index)
        {
            if (level == null) return null;
            if (index == 0)
            {
                if (level.boneOverrides == null) level.boneOverrides = new List<MechaBoneOverride>();
                return level.boneOverrides;
            }

            int addIdx = index - 1;
            if (level.additionalMechas != null && addIdx >= 0 && addIdx < level.additionalMechas.Count)
            {
                if (level.additionalMechas[addIdx] != null)
                {
                    if (level.additionalMechas[addIdx].boneOverrides == null)
                        level.additionalMechas[addIdx].boneOverrides = new List<MechaBoneOverride>();
                    return level.additionalMechas[addIdx].boneOverrides;
                }
            }

            return null;
        }

        public static bool IsBoneMatch(string transformName, string keyword)
        {
            return ChameleonCamouflage.IsBoneMatch(transformName, keyword);
        }

        public void Initialize(LevelDataSO level, int index, GameObject mechaRoot)
        {
            levelData = level;
            mechaIndex = index;
            mechaInstance = mechaRoot;

            if (boneDataList == null) boneDataList = new List<BoneData>();
            else boneDataList.Clear();

            if (mechaRoot == null || levelData == null) return;

            Transform[] allTransforms = mechaRoot.GetComponentsInChildren<Transform>(true);
            if (allTransforms == null || allTransforms.Length == 0) return;

            List<MechaBoneOverride> overrides = GetBoneOverrides(levelData, mechaIndex);

            // Access bone keywords safely without requiring UnityEditor namespace at runtime
            string[] presets = GetStandardBonePresets();

            foreach (string preset in presets)
            {
                if (string.IsNullOrEmpty(preset)) continue;

                Transform foundBone = null;
                foreach (Transform t in allTransforms)
                {
                    if (t != null && !string.IsNullOrEmpty(t.name))
                    {
                        if (IsBoneMatch(t.name, preset))
                        {
                            foundBone = t;
                            break;
                        }
                    }
                }

                if (foundBone != null)
                {
                    Vector3 currentOffset = Vector3.zero;
                    if (overrides != null)
                    {
                        var ovr = overrides.Find(o => o != null && !string.IsNullOrEmpty(o.boneKeyword) &&
                            IsBoneMatch(o.boneKeyword, preset));
                        if (ovr != null) currentOffset = ovr.rotationOffset;
                    }

                    Quaternion baseRot = foundBone.localRotation * Quaternion.Inverse(Quaternion.Euler(currentOffset));

                    boneDataList.Add(new BoneData
                    {
                        keyword = preset,
                        boneTransform = foundBone,
                        baseLocalRotation = baseRot
                    });
                }
            }
        }

        public static string[] GetStandardBonePresets()
        {
            return new string[]
            {
                "upper_arm_L", "upper_arm_R",
                "forearm_L", "forearm_R",
                "hand_L", "hand_R",
                "shoulder_L", "shoulder_R",
                "thigh_L", "thigh_R",
                "shin_L", "shin_R",
                "foot_L", "foot_R",
                "spine", "spine_001", "spine_002", "spine_003", "spine_004", "spine_005", "spine_006",
                "pelvis_L", "pelvis_R",
                "toe_L", "toe_R",
            };
        }
    }
}
