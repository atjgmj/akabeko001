using UnityEngine;
using System;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// ステージ全体の雰囲気（背景、ライティング、床）を管理する
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeshRenderer stageFloor;
        [SerializeField] private Light stageLight;

        [Header("Default Settings")]
        [SerializeField] private Color defaultLightColor = Color.white;
        [SerializeField] private Color defaultFloorColor = new Color(0.9f, 0.9f, 0.9f);

        private void Start()
        {
            // 初期状態にリセット
            ResetScene();
        }

        public void ResetScene()
        {
            ApplySettings(defaultLightColor, defaultFloorColor, 1.0f);
        }

        /// <summary>
        /// 背景(シーン)を切り替える
        /// </summary>
        public void ChangeScene(string sceneName)
        {
            switch (sceneName.ToLower())
            {
                case "space": // 宇宙
                    ApplySettings(new Color(0.5f, 0.2f, 1.0f), Color.black, 0.5f);
                    break;
                case "sea": // 海
                    ApplySettings(new Color(0.2f, 0.5f, 1.0f), new Color(0.0f, 0.1f, 0.2f), 0.7f);
                    break;
                case "volcano": // 火山
                    ApplySettings(new Color(1.0f, 0.3f, 0.1f), new Color(0.2f, 0.05f, 0.0f), 1.2f);
                    break;
                default:
                    ResetScene();
                    break;
            }
            Debug.Log($"[StageManager] Scene changed to: {sceneName}");
        }

        private void ApplySettings(Color lightColor, Color floorColor, float lightIntensity)
        {
            if (stageLight != null)
            {
                stageLight.color = lightColor;
                stageLight.intensity = lightIntensity;
            }

            if (stageFloor != null)
            {
                stageFloor.material.color = floorColor;
            }
        }
    }
}
