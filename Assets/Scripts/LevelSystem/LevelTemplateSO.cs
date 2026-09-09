using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// A themed pool of items ("İtalyan Mutfağı", "Abur Cubur") used as the starting point when a new
    /// level is authored.
    ///
    /// SNAPSHOT semantics: <see cref="ApplyTo"/> copies values into the level once and nothing reads a
    /// template at runtime, so editing a template later never touches levels already built from it.
    ///
    /// Only what actually varies by THEME lives here. The difficulty knobs - time limit, goal counts,
    /// black/frozen objects, boosters, sticky/magnet mecha, extra mechas - stay on the level, because
    /// they follow the progression curve: level 3 and level 30 can share a theme and share none of them.
    /// Mecha pose data is absent for the same reason - a pose belongs to the HOST OBJECT, so it comes
    /// from that item's <see cref="MechaPosePresetSO"/> instead.
    /// </summary>
    [CreateAssetMenu(fileName = "Template_New", menuName = "MechaFind3D/Level Template", order = 4)]
    public class LevelTemplateSO : ScriptableObject
    {
        [Header("Şablon Kimliği")]
        [Tooltip("Şablonun görünen adı (ör. İtalyan Mutfağı, Abur Cubur, Kahvaltı).")]
        public string templateName = "Yeni Şablon";

        [Tooltip("Editör listesinde ve ileride seviye haritasında kullanılabilecek tema rengi.")]
        public Color themeColor = new Color(0.20f, 0.55f, 0.95f);

        [Tooltip("İsteğe bağlı tema ikonu.")]
        public Sprite icon;

        [Header("Obje Havuzu")]
        [Tooltip("Bu temaya ait objeler. Yeni seviye bu havuzdan doldurulur; havuz hedef sayısından " +
                 "büyük olmalı ki aynı şablondan üretilen seviyeler birbirinin kopyası olmasın.")]
        public List<ItemDataSO> itemPool = new List<ItemDataSO>();

        [Header("Tema Varsayılanları")]
        [Tooltip("Bu temanın modelleri için doğru görsel ölçek. Seviyeye kopyalanır, sonra elle değiştirilebilir.")]
        public float foodTargetSize = 1.2f;

        [Tooltip("Yeni seviyede kaç ÇEŞİT hedef obje olsun (havuzdan bu kadar obje seçilir).")]
        public int defaultGoalVariety = 5;

        [Tooltip("Her hedeften kaç adet istensin. 3'ün katına yuvarlanır.")]
        public int defaultCountPerGoal = 6;

        [Tooltip("Havuzun geri kalanından kaç obje dolgu (filler) olarak eklensin.")]
        public int defaultFillerCount = 1;

        public string GetDisplayName()
        {
            return !string.IsNullOrEmpty(templateName) ? templateName : name;
        }

        /// <summary>Pool with nulls and duplicates stripped out.</summary>
        public List<ItemDataSO> GetValidPool()
        {
            var result = new List<ItemDataSO>();
            if (itemPool == null) return result;

            foreach (ItemDataSO item in itemPool)
            {
                if (item != null && !result.Contains(item)) result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Fills <paramref name="level"/>'s goals, fillers and visual scale from this template, replacing
        /// whatever was there. Returns false (and leaves the level untouched) when the pool is empty.
        ///
        /// With <paramref name="shuffle"/> on, the goals are drawn randomly from the pool, so ten levels
        /// off one template read as the same theme without being ten identical levels. Off, the pool's
        /// own order is used and the result is reproducible.
        ///
        /// The mecha host is set to the first goal item as a safe default - a host that is not in the
        /// pile has nothing to hide in. Editor code refines that choice using pose presets.
        /// </summary>
        public bool ApplyTo(LevelDataSO level, bool shuffle = true)
        {
            if (level == null) return false;

            List<ItemDataSO> pool = GetValidPool();
            if (pool.Count == 0) return false;
            if (shuffle) ShufflePool(pool);

            level.foodTargetSize = foodTargetSize;

            if (level.targetGoals == null) level.targetGoals = new List<LevelGoalRequirement>();
            if (level.fillerItems == null) level.fillerItems = new List<ItemDataSO>();
            level.targetGoals.Clear();
            level.fillerItems.Clear();

            int variety = Mathf.Clamp(defaultGoalVariety, 1, pool.Count);
            int perGoal = Mathf.Max(3, defaultCountPerGoal);
            if (perGoal % 3 != 0) perGoal += 3 - (perGoal % 3);

            for (int i = 0; i < variety; i++)
            {
                level.targetGoals.Add(new LevelGoalRequirement
                {
                    itemData = pool[i],
                    requiredCount = perGoal
                });
            }

            int fillers = Mathf.Clamp(defaultFillerCount, 0, pool.Count - variety);
            for (int i = 0; i < fillers; i++)
            {
                level.fillerItems.Add(pool[variety + i]);
            }

            ItemDataSO host = level.targetGoals[0].itemData;
            if (host != null)
            {
                level.hostItemSO = host;
                level.mechaHostKeyword = host.GetEffectiveItemId();
            }

            level.totalPileCount = level.GetTotalGoalRequiredCount();
            return true;
        }

        private static void ShufflePool(List<ItemDataSO> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                (list[i], list[rnd]) = (list[rnd], list[i]);
            }
        }
    }
}
