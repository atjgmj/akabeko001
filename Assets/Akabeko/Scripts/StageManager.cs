using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// ステージ全体の雰囲気（背景、ライティング、床、スカイボックス）を管理する
    /// SpaceSkies Free アセットのスカイボックスをステージ切り替えで動的適用する
    /// さらに、ステージ遷移時に画面をフェードイン・フェードアウトさせる演出を提供します。
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light stageLight;

        [Header("Default Settings")]
        [SerializeField] private Color defaultLightColor = Color.white;
        [SerializeField] private Color defaultFloorColor = new Color(0.9f, 0.9f, 0.9f);

        [Header("Skybox Materials (SpaceSkies Free)")]
        [Tooltip("Space stage: SpaceSkies Free/Skybox_3/Purple_1K_Resolution (宇宙)")]
        [SerializeField] private Material spaceSkybox;
        [Tooltip("Sea stage: SpaceSkies Free/Skybox_2/Green_1K_Resolution (蒼海)")]
        [SerializeField] private Material seaSkybox;
        [Tooltip("Volcano stage: SpaceSkies Free/Skybox_1/Pink_1K_Resolution (業火)")]
        [SerializeField] private Material volcanSkybox;

        private Material defaultSkybox;
        private Color defaultAmbientLight;

        public string ActiveStage { get; private set; } = "default";

        /// <summary>ステージが切り替わったときに発火するイベント（引数: 新しいステージ名）</summary>
        public event Action<string> OnStageChanged;

        private Image transitionOverlay;
        private Coroutine transitionCoroutine;

        private void Start()
        {
            // デフォルトのスカイボックスと環境光を記憶
            defaultSkybox = RenderSettings.skybox;
            defaultAmbientLight = RenderSettings.ambientLight;

            // スカイボックスが Inspector でアサインされていない場合、自動探索でフォールバック
            if (spaceSkybox == null)
                spaceSkybox = Resources.Load<Material>("SpaceSkies Free/Skybox_3/Purple_1K_Resolution");
            if (seaSkybox == null)
                seaSkybox = Resources.Load<Material>("SpaceSkies Free/Skybox_2/Green_1K_Resolution");
            if (volcanSkybox == null)
                volcanSkybox = Resources.Load<Material>("SpaceSkies Free/Skybox_1/Pink_1K_Resolution");

            // StageActionConfig が存在しない場合は自動アタッチ
            if (GetComponent<StageActionConfig>() == null)
                gameObject.AddComponent<StageActionConfig>();

            // フェード用UIイメージの動的作成
            CreateTransitionOverlay();

            // 初期化（フェードなしで即時適用）
            ApplyResetScene();
        }

        private void CreateTransitionOverlay()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                // RectTransformを最初から持って作成します (Transformとの衝突エラーを防ぐため)
                GameObject fadeGo = new GameObject("TransitionOverlay", typeof(RectTransform));
                fadeGo.transform.SetParent(canvas.transform, false);

                RectTransform rect = fadeGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                transitionOverlay = fadeGo.AddComponent<Image>();
                transitionOverlay.color = new Color(0f, 0f, 0f, 0f); // 初期は完全に透明
                transitionOverlay.raycastTarget = false;             // 通常時はクリックを透過

                // UIの最前面に描画
                rect.SetAsLastSibling();
            }
        }

        /// <summary>
        /// フェード付きでシーンをデフォルトに戻す
        /// </summary>
        public void ResetScene()
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(FadeTransitionCoroutine(ApplyResetScene));
        }

        private void ApplyResetScene()
        {
            ActiveStage = "default";
            ApplySettings(defaultLightColor, defaultFloorColor, 1.0f, defaultSkybox, defaultAmbientLight);
            OnStageChanged?.Invoke(ActiveStage);
            Debug.Log("[StageManager] Reset to Default stage.");
        }

        /// <summary>
        /// フェード付きでシーンを切り替える
        /// </summary>
        public void ChangeScene(string sceneName)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(FadeTransitionCoroutine(() => ApplyChangeScene(sceneName)));
        }

        private void ApplyChangeScene(string sceneName)
        {
            ActiveStage = sceneName.ToLower();
            switch (sceneName.ToLower())
            {
                case "space": // 幽玄 (宇宙) - Purple スカイボックス
                    ApplySettings(
                        new Color(0.5f, 0.2f, 1.0f),
                        Color.black,
                        0.5f,
                        spaceSkybox,
                        new Color(0.05f, 0.02f, 0.12f)
                    );
                    break;

                case "sea": // 蒼海 (海辺) - Green スカイボックス
                    ApplySettings(
                        new Color(0.2f, 0.5f, 1.0f),
                        new Color(0.0f, 0.1f, 0.2f),
                        0.7f,
                        seaSkybox,
                        new Color(0.05f, 0.1f, 0.2f)
                    );
                    break;

                case "volcano": // 業火 (火山) - Pink/Red スカイボックス
                    ApplySettings(
                        new Color(1.0f, 0.3f, 0.1f),
                        new Color(0.2f, 0.05f, 0.0f),
                        1.2f,
                        volcanSkybox,
                        new Color(0.2f, 0.04f, 0.0f)
                    );
                    break;

                default:
                    ApplyResetScene();
                    break;
            }
            OnStageChanged?.Invoke(ActiveStage);
            Debug.Log($"[StageManager] Scene changed to: {sceneName}");
        }

        private IEnumerator FadeTransitionCoroutine(Action changeAction)
        {
            if (transitionOverlay == null)
            {
                changeAction?.Invoke();
                yield break;
            }

            // 1. フェードアウト (画面を徐々に黒くする)＆クリックをブロック
            transitionOverlay.raycastTarget = true;
            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                transitionOverlay.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            transitionOverlay.color = Color.black;

            // 2. ステージの切り替え処理を実行
            changeAction?.Invoke();

            // 暗転の短い余韻
            yield return new WaitForSeconds(0.1f);

            // 3. フェードイン (画面を徐々に透明に戻す)
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                transitionOverlay.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            transitionOverlay.color = new Color(0f, 0f, 0f, 0f);
            transitionOverlay.raycastTarget = false; // 入力ブロックを解除
        }

        private void ApplySettings(Color lightColor, Color floorColor, float lightIntensity,
                                   Material skyboxMaterial, Color ambientColor)
        {
            if (stageLight != null)
            {
                stageLight.color = lightColor;
                stageLight.intensity = lightIntensity;
            }

            // スカイボックスの適用とカメラのクリアフラグ切り替え
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
                if (Camera.main != null)
                    Camera.main.clearFlags = CameraClearFlags.Skybox;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                RenderSettings.skybox = null;
                if (Camera.main != null)
                    Camera.main.clearFlags = CameraClearFlags.SolidColor;
            }

            // 環境光の更新
            RenderSettings.ambientLight = ambientColor;
        }
    }
}
