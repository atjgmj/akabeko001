using System;
using System.Collections.Generic;
using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// 赤べこが実行できるアクションの種類
    /// </summary>
    public enum AkabekoAction
    {
        None,            // 特殊アクションなし
        Bobbing,         // ぷかぷか浮遊（Bubble / 宇宙等で有効）
        FlyAway,         // カメラ外へ飛んでいく（首振り1回ごとに確率判定）
        Spin,            // クルクル回転する（首振り1回ごとに確率判定）
        Shake,           // ブルブル震える（首振り1回ごとに確率判定）
        ScalePulse,      // 伸縮ぷよぷよバウンド（首振り1回ごとに確率判定）
        SuperNova,       // 超新星爆発 / ビッグバン（画面ズーム＋ショックウェーブ）
        Wormhole,        // ワームホール吸い込まれ（ワームホール吸入＋ポッピン復帰）
        Clones,          // 分身の術 / ホログラムトリオ（左右に分身出現＋シンクロ首振り）
        MatrixGlitch,    // サイバー・グリッチ（RGBセパレート＆デジタルコマ送り）
        DiscoParty,      // ディスコ・ミラーボールダンス（光線照射＋フィーバーダンス）
        Tornado,         // 超高速竜巻スピン（嵐を纏う高速独楽回転）
        Sound,           // ステージ専用の環境音を再生する（入場時に自動発動）
    }

    /// <summary>
    /// カラーごとの確率設定エントリー
    /// </summary>
    [Serializable]
    public class ColorProbabilityEntry
    {
        public string colorName = "Normal"; // Normal / Gold / Silver / Rainbow
        [Range(0f, 1f)] public float probability = 0.9f;
    }

    /// <summary>
    /// ステージごとの確率設定エントリー
    /// </summary>
    [Serializable]
    public class StageProbabilityEntry
    {
        public string stageName = "Default"; // Default / Space / Sea / Volcano
        [Range(0f, 1f)] public float probability = 0.9f;
    }

    /// <summary>
    /// 1ステージ分のアクション確率テーブル
    /// </summary>
    [Serializable]
    public class StageActionEntry
    {
        public string stageName = "default"; // "default" / "space" / "sea" / "volcano"

        [Range(0f, 1f)] public float none        = 0.9f;
        [Range(0f, 1f)] public float bobbing     = 0f;
        [Range(0f, 1f)] public float flyAway     = 0f;
        [Range(0f, 1f)] public float spin        = 0f;
        [Range(0f, 1f)] public float shake       = 0f;
        [Range(0f, 1f)] public float scalePulse  = 0f;
        [Range(0f, 1f)] public float superNova    = 0f;
        [Range(0f, 1f)] public float wormhole     = 0f;
        [Range(0f, 1f)] public float clones       = 0f;
        [Range(0f, 1f)] public float matrixGlitch = 0f;
        [Range(0f, 1f)] public float discoParty   = 0f;
        [Range(0f, 1f)] public float tornado      = 0f;
        [Range(0f, 1f)] public float sound       = 0f;

        /// <summary>
        /// 指定アクションの確率値を取得する
        /// </summary>
        public float GetProbability(AkabekoAction action)
        {
            return action switch
            {
                AkabekoAction.None         => none,
                AkabekoAction.Bobbing      => bobbing,
                AkabekoAction.FlyAway     => flyAway,
                AkabekoAction.Spin        => spin,
                AkabekoAction.Shake       => shake,
                AkabekoAction.ScalePulse  => scalePulse,
                AkabekoAction.SuperNova    => superNova,
                AkabekoAction.Wormhole     => wormhole,
                AkabekoAction.Clones       => clones,
                AkabekoAction.MatrixGlitch => matrixGlitch,
                AkabekoAction.DiscoParty   => discoParty,
                AkabekoAction.Tornado      => tornado,
                AkabekoAction.Sound       => sound,
                _                         => 0f,
            };
        }

        /// <summary>
        /// 指定アクションの確率値をセットする（アドミンUIから動的変更用）
        /// </summary>
        public void SetProbability(AkabekoAction action, float value)
        {
            float clamped = Mathf.Clamp01(value);
            switch (action)
            {
                case AkabekoAction.None:          none        = clamped; break;
                case AkabekoAction.Bobbing:       bobbing     = clamped; break;
                case AkabekoAction.FlyAway:       flyAway     = clamped; break;
                case AkabekoAction.Spin:          spin        = clamped; break;
                case AkabekoAction.Shake:         shake       = clamped; break;
                case AkabekoAction.ScalePulse:    scalePulse  = clamped; break;
                case AkabekoAction.SuperNova:      superNova   = clamped; break;
                case AkabekoAction.Wormhole:       wormhole    = clamped; break;
                case AkabekoAction.Clones:         clones      = clamped; break;
                case AkabekoAction.MatrixGlitch:   matrixGlitch = clamped; break;
                case AkabekoAction.DiscoParty:     discoParty  = clamped; break;
                case AkabekoAction.Tornado:        tornado     = clamped; break;
                case AkabekoAction.Sound:         sound       = clamped; break;
            }
        }
    }

    /// <summary>
    /// ステージ × アクション × カラーの確率マトリクスおよび状態持続時間の統括設定クラス。
    /// StageManager と同じ GameObject にアタッチし、StageActionController / DynamicUIOverlay から参照して使う。
    /// </summary>
    public class StageActionConfig : MonoBehaviour
    {
        private static StageActionConfig instance;
        public static StageActionConfig Instance => instance;

        private const string PREFS_KEY = "Akabeko_StageActionConfig_Save_V1";

        [Header("1. Color Probabilities")]
        [SerializeField]
        private List<ColorProbabilityEntry> colors = new List<ColorProbabilityEntry>
        {
            new ColorProbabilityEntry { colorName = "Normal",  probability = 0.90f },
            new ColorProbabilityEntry { colorName = "Gold",    probability = 0.01f },
            new ColorProbabilityEntry { colorName = "Silver",  probability = 0.01f },
            new ColorProbabilityEntry { colorName = "Rainbow", probability = 0.01f },
        };

        [Header("2. Stage Probabilities")]
        [SerializeField]
        private List<StageProbabilityEntry> stageProbabilities = new List<StageProbabilityEntry>
        {
            new StageProbabilityEntry { stageName = "Default", probability = 0.90f },
            new StageProbabilityEntry { stageName = "Space",   probability = 0.01f },
            new StageProbabilityEntry { stageName = "Sea",     probability = 0.01f },
            new StageProbabilityEntry { stageName = "Volcano", probability = 0.01f },
        };

        [Header("3. Stage Action Matrix")]
        [SerializeField]
        private List<StageActionEntry> stages = new List<StageActionEntry>
        {
            new StageActionEntry { stageName = "default", none = 0.80f, bobbing = 0.00f, flyAway = 0.05f, spin = 0.02f, shake = 0.02f, scalePulse = 0.02f, superNova = 0.01f, wormhole = 0.01f, clones = 0.02f, matrixGlitch = 0.02f, discoParty = 0.01f, tornado = 0.02f, sound = 0f },
            new StageActionEntry { stageName = "space",   none = 0.00f, bobbing = 1.00f, flyAway = 0.05f, spin = 0.05f, shake = 0.00f, scalePulse = 0.05f, superNova = 0.15f, wormhole = 0.15f, clones = 0.10f, matrixGlitch = 0.05f, discoParty = 0.05f, tornado = 0.05f, sound = 0f },
            new StageActionEntry { stageName = "sea",     none = 0.60f, bobbing = 0.50f, flyAway = 0.05f, spin = 0.05f, shake = 0.05f, scalePulse = 0.05f, superNova = 0.02f, wormhole = 0.10f, clones = 0.05f, matrixGlitch = 0.02f, discoParty = 0.10f, tornado = 0.05f, sound = 0f },
            new StageActionEntry { stageName = "volcano", none = 0.50f, bobbing = 0.00f, flyAway = 0.10f, spin = 0.05f, shake = 0.10f, scalePulse = 0.05f, superNova = 0.15f, wormhole = 0.05f, clones = 0.05f, matrixGlitch = 0.10f, discoParty = 0.05f, tornado = 0.15f, sound = 0f },
        };

        [Header("4. Duration Settings (Head Bob Counts)")]
        [Tooltip("ステージ持続首振り数（最小〜最大）")]
        public int minStageBobs = 20;
        public int maxStageBobs = 50;

        [Tooltip("カラー持続首振り数（最小〜最大）")]
        public int minColorBobs = 15;
        public int maxColorBobs = 35;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                LoadConfig();
            }
            else
            {
                Destroy(this);
            }
        }

        // --- Color Config Accessors ---

        public List<ColorProbabilityEntry> GetAllColors() => colors;

        public float GetColorProbability(string colorName)
        {
            var c = colors.Find(x => x.colorName.Equals(colorName, StringComparison.OrdinalIgnoreCase));
            return c != null ? c.probability : 0f;
        }

        public void SetColorProbability(string colorName, float value)
        {
            var c = colors.Find(x => x.colorName.Equals(colorName, StringComparison.OrdinalIgnoreCase));
            if (c != null)
            {
                c.probability = Mathf.Clamp01(value);
            }
        }

        // --- Stage Config Accessors ---

        public List<StageProbabilityEntry> GetAllStageProbabilities() => stageProbabilities;

        public float GetStageProbability(string stageName)
        {
            var s = stageProbabilities.Find(x => x.stageName.Equals(stageName, StringComparison.OrdinalIgnoreCase));
            return s != null ? s.probability : 0f;
        }

        public void SetStageProbability(string stageName, float value)
        {
            var s = stageProbabilities.Find(x => x.stageName.Equals(stageName, StringComparison.OrdinalIgnoreCase));
            if (s != null)
            {
                s.probability = Mathf.Clamp01(value);
            }
        }

        // --- Action Config Accessors ---

        public float GetProbability(string stageName, AkabekoAction action)
        {
            var entry = GetEntry(stageName);
            return entry?.GetProbability(action) ?? 0f;
        }

        public void SetProbability(string stageName, AkabekoAction action, float value)
        {
            var entry = GetEntry(stageName);
            if (entry == null)
            {
                var newEntry = new StageActionEntry { stageName = stageName.ToLower() };
                newEntry.SetProbability(action, value);
                stages.Add(newEntry);
            }
            else
            {
                entry.SetProbability(action, value);
            }
        }

        public StageActionEntry GetEntry(string stageName)
        {
            string key = stageName.ToLower();
            foreach (var s in stages)
                if (s.stageName.ToLower() == key) return s;
            return null;
        }

        public List<StageActionEntry> GetAllEntries() => stages;

        // --- Persistence (Save / Load / Reset) ---

        [Serializable]
        private class SaveDataContainer
        {
            public List<ColorProbabilityEntry> colors;
            public List<StageProbabilityEntry> stageProbabilities;
            public List<StageActionEntry> stages;
            public int minStageBobs;
            public int maxStageBobs;
            public int minColorBobs;
            public int maxColorBobs;
        }

        public void SaveConfig()
        {
            try
            {
                SaveDataContainer data = new SaveDataContainer
                {
                    colors = this.colors,
                    stageProbabilities = this.stageProbabilities,
                    stages = this.stages,
                    minStageBobs = this.minStageBobs,
                    maxStageBobs = this.maxStageBobs,
                    minColorBobs = this.minColorBobs,
                    maxColorBobs = this.maxColorBobs
                };
                string json = JsonUtility.ToJson(data, true);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
                Debug.Log("[StageActionConfig] Config saved successfully to PlayerPrefs!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageActionConfig] Failed to save config: {ex.Message}");
            }
        }

        public void LoadConfig()
        {
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return;

            try
            {
                string json = PlayerPrefs.GetString(PREFS_KEY);
                SaveDataContainer data = JsonUtility.FromJson<SaveDataContainer>(json);
                if (data != null)
                {
                    if (data.colors != null && data.colors.Count > 0) this.colors = data.colors;
                    if (data.stageProbabilities != null && data.stageProbabilities.Count > 0) this.stageProbabilities = data.stageProbabilities;
                    if (data.stages != null && data.stages.Count > 0) this.stages = data.stages;
                    if (data.minStageBobs > 0) this.minStageBobs = data.minStageBobs;
                    if (data.maxStageBobs > 0) this.maxStageBobs = data.maxStageBobs;
                    if (data.minColorBobs > 0) this.minColorBobs = data.minColorBobs;
                    if (data.maxColorBobs > 0) this.maxColorBobs = data.maxColorBobs;
                    Debug.Log("[StageActionConfig] Config loaded successfully from PlayerPrefs!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageActionConfig] Failed to load config: {ex.Message}");
            }
        }

        public void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
            colors = new List<ColorProbabilityEntry>
            {
                new ColorProbabilityEntry { colorName = "Normal",  probability = 0.90f },
                new ColorProbabilityEntry { colorName = "Gold",    probability = 0.01f },
                new ColorProbabilityEntry { colorName = "Silver",  probability = 0.01f },
                new ColorProbabilityEntry { colorName = "Rainbow", probability = 0.01f },
            };
            stageProbabilities = new List<StageProbabilityEntry>
            {
                new StageProbabilityEntry { stageName = "Default", probability = 0.90f },
                new StageProbabilityEntry { stageName = "Space",   probability = 0.01f },
                new StageProbabilityEntry { stageName = "Sea",     probability = 0.01f },
                new StageProbabilityEntry { stageName = "Volcano", probability = 0.01f },
            };
            stages = new List<StageActionEntry>
            {
                new StageActionEntry { stageName = "default", none = 0.90f, bobbing = 0.00f, flyAway = 0.10f, spin = 0.01f, shake = 0.01f, scalePulse = 0.01f, sound = 0f },
                new StageActionEntry { stageName = "space",   none = 0.00f, bobbing = 1.00f, flyAway = 0.10f, spin = 0.05f, shake = 0.00f, scalePulse = 0.05f, sound = 0f },
                new StageActionEntry { stageName = "sea",     none = 0.80f, bobbing = 0.50f, flyAway = 0.05f, spin = 0.02f, shake = 0.02f, scalePulse = 0.02f, sound = 0f },
                new StageActionEntry { stageName = "volcano", none = 0.70f, bobbing = 0.00f, flyAway = 0.15f, spin = 0.10f, shake = 0.20f, scalePulse = 0.10f, sound = 0f },
            };
            minStageBobs = 20;
            maxStageBobs = 50;
            minColorBobs = 15;
            maxColorBobs = 35;
            Debug.Log("[StageActionConfig] Config reset to defaults!");
        }
    }
}

