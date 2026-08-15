using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

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

        private ScanlinePostProcess scanlinePostProcess;
        private Material monoLineMaterial;
        private Dictionary<Renderer, Material> originalModelMaterials = new Dictionary<Renderer, Material>();

        public Material GetMonoLineMaterial() => monoLineMaterial;

        private void EnsureScanlinePostProcess()
        {
            if (scanlinePostProcess == null && Camera.main != null)
            {
                scanlinePostProcess = Camera.main.GetComponent<ScanlinePostProcess>();
                if (scanlinePostProcess == null)
                    scanlinePostProcess = Camera.main.gameObject.AddComponent<ScanlinePostProcess>();
            }
        }

        private void ApplyMonoLineMaterial(bool active)
        {
            AkabekoController controller = FindFirstObjectByType<AkabekoController>();
            if (controller == null) return;

            Renderer[] renderers = controller.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            // 初回にモデルの元マテリアルを保存
            foreach (var r in renderers)
            {
                if (r != null && !originalModelMaterials.ContainsKey(r))
                {
                    originalModelMaterials[r] = r.sharedMaterial;
                }
            }

            if (active)
            {
                if (monoLineMaterial == null)
                {
                    Texture2D scanlineTex = Resources.Load<Texture2D>("Textures/Tex_Scanline");
                    if (scanlineTex == null)
                    {
                        scanlineTex = new Texture2D(1, 128, TextureFormat.RGBA32, false);
                        scanlineTex.wrapMode = TextureWrapMode.Repeat;
                        scanlineTex.filterMode = FilterMode.Point;
                        for (int y = 0; y < 128; y++)
                        {
                            float val = (y % 8 < 4) ? 0.05f : 0.95f;
                            scanlineTex.SetPixel(0, y, new Color(val, val, val, 1f));
                        }
                        scanlineTex.Apply();
                    }

                    Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
                    monoLineMaterial = new Material(shader);
                    monoLineMaterial.mainTexture = scanlineTex;
                    monoLineMaterial.mainTextureScale = new Vector2(1f, 24f);

                    if (monoLineMaterial.HasProperty("_BaseColor")) monoLineMaterial.SetColor("_BaseColor", Color.white);
                    else if (monoLineMaterial.HasProperty("_Color")) monoLineMaterial.SetColor("_Color", Color.white);
                    if (monoLineMaterial.HasProperty("_Glossiness")) monoLineMaterial.SetFloat("_Glossiness", 0.1f);
                    if (monoLineMaterial.HasProperty("_Metallic")) monoLineMaterial.SetFloat("_Metallic", 0f);
                }

                foreach (var r in renderers)
                {
                    if (r != null && monoLineMaterial != null)
                        r.sharedMaterial = monoLineMaterial;
                }
            }
            else
            {
                foreach (var r in renderers)
                {
                    if (r != null && originalModelMaterials.TryGetValue(r, out Material origMat))
                    {
                        r.sharedMaterial = origMat;
                    }
                }
            }
        }

        private void ApplyResetScene()
        {
            ActiveStage = "default";
            EnsureScanlinePostProcess();
            if (scanlinePostProcess != null) scanlinePostProcess.isEffectActive = false;
            ApplyMonoLineMaterial(false);

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
            EnsureScanlinePostProcess();

            // スキャンラインポスプロエフェクトの切り替え
            bool isMonoLine = ActiveStage == "monoline";
            if (scanlinePostProcess != null)
                scanlinePostProcess.isEffectActive = isMonoLine;

            ApplyMonoLineMaterial(isMonoLine);

            switch (ActiveStage)
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

                case "monoline": // 白黒スキャンライン線画（MEOW AIエージェント風アート）
                    ApplySettings(
                        new Color(0.9f, 0.9f, 0.9f),
                        new Color(0.95f, 0.95f, 0.95f),
                        1.4f,
                        null,
                        new Color(0.85f, 0.85f, 0.85f)
                    );
                    if (Camera.main != null)
                    {
                        Camera.main.clearFlags = CameraClearFlags.SolidColor;
                        Camera.main.backgroundColor = new Color(0.95f, 0.95f, 0.95f);
                    }
                    break;

                default:
                    ApplyResetScene();
                    break;
            }
            OnStageChanged?.Invoke(ActiveStage);
            if (ActiveStage != "default")
            {
                string displayStage = char.ToUpper(ActiveStage[0]) + ActiveStage.Substring(1);
                DynamicUIOverlay.ShowRareAlert("Stage", displayStage);
            }
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
