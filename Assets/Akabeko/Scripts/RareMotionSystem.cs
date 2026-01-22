using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace Akabeko
{
    /// <summary>
    /// レアモーションの判定と実行を管理するシステム
    /// </summary>
    public class RareMotionSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string motionDataPath = "Data/RareMotions";

        private List<RareMotionData> rareMotions = new List<RareMotionData>();
        private DataManager dataManager;
        private UIManager uiManager;
        private StageManager stageManager;
        private AkabekoController akabekoController;

        public event Action<RareMotionData> OnRareMotionTriggered;

        private void Awake()
        {
            dataManager = FindFirstObjectByType<DataManager>();
            uiManager = FindFirstObjectByType<UIManager>();
            stageManager = FindFirstObjectByType<StageManager>();
            akabekoController = FindFirstObjectByType<AkabekoController>();
            LoadRareMotions();
        }

        /// <summary>
        /// レアモーションデータの読み込み
        /// </summary>
        private void LoadRareMotions()
        {
            // Resources フォルダからJSONファイルを読み込み
            TextAsset[] motionFiles = Resources.LoadAll<TextAsset>(motionDataPath);

            foreach (TextAsset file in motionFiles)
            {
                try
                {
                    RareMotionData motion = JsonUtility.FromJson<RareMotionData>(file.text);
                    rareMotions.Add(motion);
                    Debug.Log($"Loaded rare motion: {motion.motionName}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load motion file {file.name}: {e.Message}");
                }
            }

            Debug.Log($"Total rare motions loaded: {rareMotions.Count}");
        }

        /// <summary>
        /// レアモーション判定
        /// </summary>
        public void CheckRareMotion(SwipeData swipeData, int totalSwipeCount)
        {
            foreach (RareMotionData motion in rareMotions)
            {
                if (CheckConditions(motion, swipeData, totalSwipeCount))
                {
                    TriggerRareMotion(motion);
                    break; // 1回のスワイプで1つのレアモーションのみ発動
                }
            }
        }

        /// <summary>
        /// 条件チェック（AND条件）
        /// </summary>
        private bool CheckConditions(RareMotionData motion, SwipeData swipeData, int totalSwipeCount)
        {
            foreach (MotionCondition condition in motion.conditions)
            {
                if (!EvaluateCondition(condition, swipeData, totalSwipeCount))
                {
                    return false; // 1つでも条件を満たさなければfalse
                }
            }
            return true; // 全条件を満たした
        }

        /// <summary>
        /// 個別条件の評価
        /// </summary>
        private bool EvaluateCondition(MotionCondition condition, SwipeData swipeData, int totalSwipeCount)
        {
            switch (condition.type)
            {
                case ConditionType.PROB:
                    float rate = condition.GetFloatParam("rate", 0.01f);
                    return UnityEngine.Random.value < rate;

                case ConditionType.SWIPE_SPEED:
                    float minSpeed = condition.GetFloatParam("min", 0f);
                    return swipeData.speed >= minSpeed;

                case ConditionType.COUNT:
                    int targetCount = condition.GetIntParam("count", 100);
                    return totalSwipeCount == targetCount;

                case ConditionType.TIME:
                    int targetHour = condition.GetIntParam("hour", 0);
                    return DateTime.Now.Hour == targetHour;

                case ConditionType.DATE:
                    int month = condition.GetIntParam("month", 1);
                    int day = condition.GetIntParam("day", 1);
                    return DateTime.Now.Month == month && DateTime.Now.Day == day;

                // 将来実装予定
                case ConditionType.TAP_PART:
                case ConditionType.SWIPE_PATTERN:
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// レアモーション発動
        /// </summary>
        private void TriggerRareMotion(RareMotionData motion)
        {
            Debug.Log($"★ Rare Motion Triggered: {motion.motionName} ★");

            // データ記録
            if (dataManager != null)
            {
                dataManager.DiscoverMotion(motion.motionId);
            }

            // UI演出
            if (uiManager != null)
            {
                uiManager.ShowRareMotionEffect(motion);
            }

            // イベント発火
            OnRareMotionTriggered?.Invoke(motion);

            // モーションタイプ別の処理
            ExecuteMotion(motion);
        }

        /// <summary>
        /// モーション実行
        /// </summary>
        private void ExecuteMotion(RareMotionData motion)
        {
            switch (motion.type)
            {
                case MotionType.COLOR_CHANGE:
                    ChangeColor(motion);
                    break;

                case MotionType.BACKGROUND_CHANGE:
                    ChangeBackground(motion);
                    break;

                case MotionType.ANIMATION:
                    PlayAnimation(motion);
                    break;
            }
        }

        private void ChangeColor(RareMotionData motion)
        {
            if (akabekoController == null) return;

            // マテリアルをResourcesからロード
            Material newMat = Resources.Load<Material>($"Materials/{motion.materialName}");
            if (newMat != null)
            {
                // Akabeko のレンダラーに適用
                Renderer[] renderers = akabekoController.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.material = newMat;
                }
                Debug.Log($"[RareMotionSystem] Material changed to: {motion.materialName}");
            }
            else
            {
                Debug.LogWarning($"[RareMotionSystem] Material not found in Resources/Materials: {motion.materialName}");
            }
        }
        
        private void ChangeBackground(RareMotionData motion)
        {
            if (stageManager != null)
            {
                stageManager.ChangeScene(motion.backgroundName);
            }
        }

        private void PlayAnimation(RareMotionData motion)
        {
            if (akabekoController == null) return;
            
            Animator anim = akabekoController.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger(motion.animationName);
                Debug.Log($"[RareMotionSystem] Playing animation trigger: {motion.animationName}");
            }
        }
    }

    // ========== データ構造 ==========

    [Serializable]
    public class RareMotionData
    {
        public string motionId;
        public string motionName;
        public MotionType type;
        public List<MotionCondition> conditions;
        public RarityLevel rarity;

        // モーションタイプ別のパラメータ
        public string materialName;      // COLOR_CHANGE用
        public string backgroundName;    // BACKGROUND_CHANGE用
        public string animationName;     // ANIMATION用
    }

    [Serializable]
    public class MotionCondition
    {
        public ConditionType type;
        public List<MotionParameter> parameters = new List<MotionParameter>();

        // パラメータ取得ヘルパー
        public float GetFloatParam(string key, float defaultValue)
        {
            string value = GetStringParam(key, null);
            if (value != null && float.TryParse(value, out float floatValue))
                return floatValue;
            return defaultValue;
        }

        public int GetIntParam(string key, int defaultValue)
        {
            string value = GetStringParam(key, null);
            if (value != null && int.TryParse(value, out int intValue))
                return intValue;
            return defaultValue;
        }

        public string GetStringParam(string key, string defaultValue)
        {
            var param = parameters.Find(p => p.key == key);
            return param != null ? param.value : defaultValue;
        }
    }

    [Serializable]
    public class MotionParameter
    {
        public string key;
        public string value;
    }

    public enum MotionType
    {
        COLOR_CHANGE,       // 色変更
        BACKGROUND_CHANGE,  // 背景変更
        ANIMATION          // アニメーション
    }

    public enum ConditionType
    {
        PROB,              // 確率
        TAP_PART,          // 部位タップ
        SWIPE_PATTERN,     // スワイプパターン
        SWIPE_SPEED,       // スワイプ速度
        COUNT,             // 累計回数
        TIME,              // 時間帯
        DATE               // 日付
    }

    public enum RarityLevel
    {
        COMMON,            // ★
        RARE,              // ★★
        SUPER_RARE,        // ★★★
        ULTRA_RARE         // ★★★★
    }
}
