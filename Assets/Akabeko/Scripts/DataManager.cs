using UnityEngine;
using System;
using System.Collections.Generic;

namespace Akabeko
{
    /// <summary>
    /// ゲームデータの保存・読み込みを管理
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        private const string KEY_SWIPE_COUNT = "SwipeCount";
        private const string KEY_DISCOVERED_MOTIONS = "DiscoveredMotions";
        private const string KEY_LAST_PLAY_DATE = "LastPlayDate";

        private static DataManager instance;
        public static DataManager Instance => instance;

        private HashSet<string> discoveredMotions = new HashSet<string>();

        private void Awake()
        {
            // シングルトン
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                LoadData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// データ読み込み
        /// </summary>
        private void LoadData()
        {
            // 発見済みモーションの読み込み
            string motionsJson = PlayerPrefs.GetString(KEY_DISCOVERED_MOTIONS, "");
            if (!string.IsNullOrEmpty(motionsJson))
            {
                try
                {
                    DiscoveredMotionsData data = JsonUtility.FromJson<DiscoveredMotionsData>(motionsJson);
                    discoveredMotions = new HashSet<string>(data.motionIds);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load discovered motions: {e.Message}");
                }
            }

            Debug.Log($"Data loaded. Swipe count: {GetSwipeCount()}, Discovered motions: {discoveredMotions.Count}");
        }

        /// <summary>
        /// データ保存
        /// </summary>
        private void SaveData()
        {
            // 発見済みモーションの保存
            DiscoveredMotionsData data = new DiscoveredMotionsData
            {
                motionIds = new List<string>(discoveredMotions)
            };
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(KEY_DISCOVERED_MOTIONS, json);

            // 最終プレイ日時
            PlayerPrefs.SetString(KEY_LAST_PLAY_DATE, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            PlayerPrefs.Save();
        }

        /// <summary>
        /// スワイプ回数を取得
        /// </summary>
        public int GetSwipeCount()
        {
            return PlayerPrefs.GetInt(KEY_SWIPE_COUNT, 0);
        }

        /// <summary>
        /// スワイプ回数を増加
        /// </summary>
        public void IncrementSwipeCount()
        {
            int count = GetSwipeCount() + 1;
            PlayerPrefs.SetInt(KEY_SWIPE_COUNT, count);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// モーションを発見済みとして記録
        /// </summary>
        public void DiscoverMotion(string motionId)
        {
            if (!discoveredMotions.Contains(motionId))
            {
                discoveredMotions.Add(motionId);
                SaveData();
                Debug.Log($"New motion discovered: {motionId}");
            }
        }

        /// <summary>
        /// モーションが発見済みかチェック
        /// </summary>
        public bool IsMotionDiscovered(string motionId)
        {
            return discoveredMotions.Contains(motionId);
        }

        /// <summary>
        /// 発見済みモーション数を取得
        /// </summary>
        public int GetDiscoveredMotionCount()
        {
            return discoveredMotions.Count;
        }

        /// <summary>
        /// 全データをリセット（デバッグ用）
        /// </summary>
        public void ResetAllData()
        {
            PlayerPrefs.DeleteAll();
            discoveredMotions.Clear();
            Debug.Log("All data has been reset.");
        }

        private void OnApplicationQuit()
        {
            SaveData();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                SaveData();
            }
        }
    }

    [Serializable]
    public class DiscoveredMotionsData
    {
        public List<string> motionIds;
    }
}
