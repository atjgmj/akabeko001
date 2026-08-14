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
        Bobbing,     // ぷかぷか浮遊（0.0=無効, 1.0=必ず発動）
        FlyAway,     // カメラ外へ飛んでいく（首振り1回ごとに確率判定）
        Spin,        // クルクル回転する（首振り1回ごとに確率判定）
        Shake,       // ブルブル震える（首振り1回ごとに確率判定）
        ScalePulse,  // サイズが大きくなったり小さくなったりする（首振り1回ごとに確率判定）
        Sound,       // ステージ専用の環境音を再生する（入場時に自動発動）
    }

    /// <summary>
    /// 1ステージ分のアクション確率テーブル
    /// </summary>
    [Serializable]
    public class StageActionEntry
    {
        public string stageName = "default"; // "default" / "space" / "sea" / "volcano"

        [Range(0f, 1f)] public float bobbing     = 0f;
        [Range(0f, 1f)] public float flyAway     = 0f;
        [Range(0f, 1f)] public float spin        = 0f;
        [Range(0f, 1f)] public float shake       = 0f;
        [Range(0f, 1f)] public float scalePulse  = 0f;
        [Range(0f, 1f)] public float sound       = 0f;

        /// <summary>
        /// 指定アクションの確率値を取得する
        /// </summary>
        public float GetProbability(AkabekoAction action)
        {
            return action switch
            {
                AkabekoAction.Bobbing    => bobbing,
                AkabekoAction.FlyAway   => flyAway,
                AkabekoAction.Spin      => spin,
                AkabekoAction.Shake     => shake,
                AkabekoAction.ScalePulse => scalePulse,
                AkabekoAction.Sound     => sound,
                _                       => 0f,
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
                case AkabekoAction.Bobbing:     bobbing     = clamped; break;
                case AkabekoAction.FlyAway:     flyAway     = clamped; break;
                case AkabekoAction.Spin:        spin        = clamped; break;
                case AkabekoAction.Shake:       shake       = clamped; break;
                case AkabekoAction.ScalePulse:  scalePulse  = clamped; break;
                case AkabekoAction.Sound:       sound       = clamped; break;
            }
        }
    }

    /// <summary>
    /// ステージ × アクションの確率マトリクス管理クラス。
    /// StageManager と同じ GameObject にアタッチし、StageActionController から参照して使う。
    ///
    /// 【マトリクス仕様】
    ///   - bobbing:    ステージ入場時に1回だけ判定。1.0=必ず発動 / 0.0=発動しない。
    ///   - flyAway:    首振り1回ごとに確率判定。
    ///   - spin:       首振り1回ごとに確率判定。
    ///   - shake:      首振り1回ごとに確率判定。
    ///   - scalePulse: 首振り1回ごとに確率判定。
    ///   - sound:      ステージ入場時に自動再生/停止。1.0=再生 / 0.0=再生しない。
    /// </summary>
    public class StageActionConfig : MonoBehaviour
    {
        private static StageActionConfig instance;
        public static StageActionConfig Instance => instance;

        [Header("Stage × Action Probability Matrix")]
        [Tooltip("各ステージのアクション確率テーブル。ステージ名は小文字で設定してください。")]
        [SerializeField]
        private List<StageActionEntry> stages = new List<StageActionEntry>
        {
            new StageActionEntry { stageName = "default", bobbing = 0f,   flyAway = 0f,    spin = 0f, shake = 0f, scalePulse = 0f, sound = 0f },
            new StageActionEntry { stageName = "space",   bobbing = 1.0f, flyAway = 0.05f, spin = 0f, shake = 0f, scalePulse = 0f, sound = 0f },
            new StageActionEntry { stageName = "sea",     bobbing = 0f,   flyAway = 0f,    spin = 0f, shake = 0f, scalePulse = 0f, sound = 0f },
            new StageActionEntry { stageName = "volcano", bobbing = 0f,   flyAway = 0f,    spin = 0f, shake = 0f, scalePulse = 0f, sound = 0f },
        };

        private void Awake()
        {
            if (instance == null) instance = this;
            else Destroy(this);
        }

        /// <summary>
        /// 指定ステージ・アクションの確率を返す（ステージが未定義の場合は0を返す）
        /// </summary>
        public float GetProbability(string stageName, AkabekoAction action)
        {
            var entry = GetEntry(stageName);
            return entry?.GetProbability(action) ?? 0f;
        }

        /// <summary>
        /// 指定ステージ・アクションの確率を設定する（アドミンUIからのリアルタイム変更用）
        /// </summary>
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

        /// <summary>
        /// 指定ステージのエントリを返す
        /// </summary>
        public StageActionEntry GetEntry(string stageName)
        {
            string key = stageName.ToLower();
            foreach (var s in stages)
                if (s.stageName.ToLower() == key) return s;
            return null;
        }

        /// <summary>
        /// 全ステージ名のリストを返す（アドミンUI描画用）
        /// </summary>
        public List<StageActionEntry> GetAllEntries() => stages;
    }
}
