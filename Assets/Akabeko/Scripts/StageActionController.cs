using UnityEngine;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// ステージ × アクション確率マトリクス（StageActionConfig）を参照して、
    /// 赤べこのアクション演出（Bobbing / FlyAway / Spin / Shake / ScalePulse）を統合制御するクラス。
    ///
    /// ▼ アクション発動タイミング
    ///   - Bobbing    : ステージ入場時に1回だけ判定（1.0=必ず発動）
    ///   - FlyAway    : 首振り1回ごとに確率判定
    ///   - Spin       : 首振り1回ごとに確率判定
    ///   - Shake      : 首振り1回ごとに確率判定
    ///   - ScalePulse : 首振り1回ごとに確率判定
    /// </summary>
    public class StageActionController : MonoBehaviour
    {
        private StageManager stageManager;
        private StageActionConfig actionConfig;
        private NeckPhysics neckPhysics;

        // --- Bobbing (浮遊) ---
        [Header("Bobbing Settings")]
        [SerializeField] private float bobSpeed = 1.0f;
        [SerializeField] private float bobRange = 0.15f;
        [SerializeField] private Vector3 rotBobSpeed = new Vector3(0.5f, 0.4f, 0.3f);
        [SerializeField] private Vector3 rotBobRange = new Vector3(4f, 4f, 4f);

        // --- Drift (漂い) - クリック入力 ---
        [Header("Drift Settings")]
        [SerializeField] private float clickImpulseForce = 1.8f;
        [SerializeField] private float clickTorqueForce = 35f;
        [SerializeField] private float returnSpringStrength = 1.8f;
        [SerializeField] private float driftDamping = 0.85f;
        [SerializeField] private float rotDamping = 0.90f;

        // --- Skybox Parallax (宇宙背景視差) ---
        [Header("Skybox Parallax Settings")]
        [SerializeField] private float skyboxRotationSpeed = 0.4f;
        [SerializeField] private float skyboxParallaxMultiplier = 12.0f;

        // --- FlyAway (飛んでいく) ---
        [Header("FlyAway Settings")]
        [SerializeField] private float flyAwayExitDuration = 0.7f;
        [SerializeField] private float flyAwayReturnDelay = 0.6f;
        [SerializeField] private float flyAwayExitMargin = 1.3f;
        [SerializeField] private float flyAwayTorque = 200f;

        // --- Spin (回転) ---
        [Header("Spin Settings")]
        [SerializeField] private float spinDuration = 1.2f;
        [SerializeField] private float spinTorque = 300f;

        // --- Shake (震え) ---
        [Header("Shake Settings")]
        [SerializeField] private float shakeDuration = 0.6f;
        [SerializeField] private float shakeIntensity = 0.08f;

        // --- ScalePulse (縮尺変化) ---
        [Header("ScalePulse Settings")]
        [SerializeField] private float scalePulseMultiplier = 1.3f;
        [SerializeField] private float scalePulseDuration = 0.5f;

        // 内部状態
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalScale;

        private Vector3 currentDriftPosition = Vector3.zero;
        private Vector3 driftVelocity = Vector3.zero;
        private Quaternion currentDriftRotation = Quaternion.identity;
        private Vector3 driftAngularVelocity = Vector3.zero;

        private float bobbingWeight = 0f; // 0=無効, 1=有効
        private Camera mainCam;
        private Vector3 initialCameraPosition;

        private Material spaceMaterial;
        private float originalSkyboxRotation = 0f;
        private float skyboxTimeRotation = 0f;
        private bool hasSavedSkyboxRotation = false;

        private bool isBobbing = false;
        private bool isFlyingAway = false;
        private bool isSpinning = false;
        private bool isShaking = false;
        private bool isScalePulsing = false;
        private bool physicsFrozen = false;

        private RareMotionSystem rareMotionSystem;

        // 首振り状態維持カウンター
        private string activeColorState = "Normal";
        private int colorRemainingBobs = 0;
        private int stageRemainingBobs = 0;

        // ---- Unity Lifecycle ----

        private void Awake()
        {
            stageManager = FindFirstObjectByType<StageManager>();
            rareMotionSystem = FindFirstObjectByType<RareMotionSystem>();
            neckPhysics = GetComponent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = GetComponentInParent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = FindFirstObjectByType<NeckPhysics>();

            mainCam = Camera.main;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialLocalScale = transform.localScale;

            if (mainCam != null)
                initialCameraPosition = mainCam.transform.position;
        }

        private void Start()
        {
            actionConfig = FindFirstObjectByType<StageActionConfig>();
            if (rareMotionSystem == null) rareMotionSystem = FindFirstObjectByType<RareMotionSystem>();

            if (stageManager != null)
            {
                stageManager.OnStageChanged -= HandleStageChanged;
                stageManager.OnStageChanged += HandleStageChanged;
            }
            if (neckPhysics != null)
            {
                neckPhysics.OnNeckShakeCounted -= HandleShakeTrigger;
                neckPhysics.OnNeckShakeCounted += HandleShakeTrigger;
            }

            if (stageManager != null)
                HandleStageChanged(stageManager.ActiveStage);
        }

        private void OnEnable()
        {
            if (stageManager != null)
                stageManager.OnStageChanged += HandleStageChanged;
            if (neckPhysics != null)
                neckPhysics.OnNeckShakeCounted += HandleShakeTrigger;
        }

        private void OnDisable()
        {
            if (stageManager != null)
                stageManager.OnStageChanged -= HandleStageChanged;
            if (neckPhysics != null)
                neckPhysics.OnNeckShakeCounted -= HandleShakeTrigger;

            if (hasSavedSkyboxRotation && spaceMaterial != null)
                spaceMaterial.SetFloat("_Rotation", originalSkyboxRotation);
        }

        // ---- イベントハンドラー ----

        private void HandleStageChanged(string stageName)
        {
            float bobbingProb = actionConfig != null
                ? actionConfig.GetProbability(stageName, AkabekoAction.Bobbing)
                : (stageName.Equals("space", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f);

            isBobbing = (Random.value < bobbingProb);

            if (!stageName.Equals("space", System.StringComparison.OrdinalIgnoreCase) && hasSavedSkyboxRotation && spaceMaterial != null)
            {
                spaceMaterial.SetFloat("_Rotation", originalSkyboxRotation);
                spaceMaterial = null;
            }

            if (stageName.Equals("space", System.StringComparison.OrdinalIgnoreCase))
            {
                if (spaceMaterial == null && RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
                {
                    spaceMaterial = RenderSettings.skybox;
                    originalSkyboxRotation = spaceMaterial.GetFloat("_Rotation");
                    hasSavedSkyboxRotation = true;
                }
            }

            Debug.Log($"[StageActionController] Stage changed to '{stageName}'. Bobbing={isBobbing}");
        }

        /// <summary>
        /// 首振りごとに呼ばれるメインのステートマシン処理
        /// </summary>
        private void HandleShakeTrigger()
        {
            if (actionConfig == null) actionConfig = StageActionConfig.Instance ?? FindFirstObjectByType<StageActionConfig>();

            // 1. カラー状態管理
            EvaluateColorState();

            // 2. ステージ状態管理
            EvaluateStageState();

            // 3. アクション発動管理
            EvaluateActionTrigger();
        }

        /// <summary>
        /// カラーの持続カウンター処理 & 確率ロール
        /// </summary>
        private void EvaluateColorState()
        {
            if (colorRemainingBobs > 0)
            {
                colorRemainingBobs--;
                if (colorRemainingBobs == 0 && activeColorState != "Normal")
                {
                    activeColorState = "Normal";
                    if (rareMotionSystem != null) rareMotionSystem.ResetColor();
                    Debug.Log("[StageActionController] Color duration expired. Reverted to Normal.");
                }
                return;
            }

            // Normal状態の場合、新規カラーチェンジの確率判定
            if (actionConfig == null) return;
            var colorEntries = actionConfig.GetAllColors();
            if (colorEntries == null || colorEntries.Count == 0) return;

            float totalWeight = 0f;
            foreach (var c in colorEntries) totalWeight += c.probability;
            if (totalWeight <= 0f) return;

            float roll = Random.value * totalWeight;
            float currentSum = 0f;
            string selectedColor = "Normal";

            foreach (var c in colorEntries)
            {
                currentSum += c.probability;
                if (roll <= currentSum)
                {
                    selectedColor = c.colorName;
                    break;
                }
            }

            if (!selectedColor.Equals("Normal", System.StringComparison.OrdinalIgnoreCase))
            {
                activeColorState = selectedColor;
                colorRemainingBobs = Random.Range(actionConfig.minColorBobs, actionConfig.maxColorBobs + 1);
                if (rareMotionSystem != null) rareMotionSystem.SetColorByName(selectedColor);
                Debug.Log($"[StageActionController] ★ Color transition to '{selectedColor}' for {colorRemainingBobs} bobs!");
            }
        }

        /// <summary>
        /// ステージの持続カウンター処理 & 確率ロール
        /// </summary>
        private void EvaluateStageState()
        {
            if (stageRemainingBobs > 0)
            {
                stageRemainingBobs--;
                if (stageRemainingBobs == 0)
                {
                    string currentActive = stageManager != null ? stageManager.ActiveStage : "default";
                    if (!currentActive.Equals("default", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (stageManager != null) stageManager.ResetScene();
                        Debug.Log("[StageActionController] Stage duration expired. Reverted to Default.");
                    }
                }
                return;
            }

            // Default状態の場合、新規ステージチェンジの確率判定
            if (actionConfig == null) return;
            var stageEntries = actionConfig.GetAllStageProbabilities();
            if (stageEntries == null || stageEntries.Count == 0) return;

            float totalWeight = 0f;
            foreach (var s in stageEntries) totalWeight += s.probability;
            if (totalWeight <= 0f) return;

            float roll = Random.value * totalWeight;
            float currentSum = 0f;
            string selectedStage = "Default";

            foreach (var s in stageEntries)
            {
                currentSum += s.probability;
                if (roll <= currentSum)
                {
                    selectedStage = s.stageName;
                    break;
                }
            }

            if (!selectedStage.Equals("Default", System.StringComparison.OrdinalIgnoreCase))
            {
                stageRemainingBobs = Random.Range(actionConfig.minStageBobs, actionConfig.maxStageBobs + 1);
                if (stageManager != null) stageManager.ChangeScene(selectedStage.ToLower());
                Debug.Log($"[StageActionController] ★ Stage transition to '{selectedStage}' for {stageRemainingBobs} bobs!");
            }
        }

        private bool isSuperNova = false;
        private bool isWormhole = false;
        private bool isClones = false;
        private bool isMatrixGlitch = false;
        private bool isDiscoParty = false;
        private bool isTornado = false;

        /// <summary>
        /// 現在のステージに応じたアクション判定（全アクションの重み付き抽選）
        /// </summary>
        private void EvaluateActionTrigger()
        {
            if (stageManager == null) return;
            string stage = stageManager.ActiveStage;

            // Bobbing（浮遊）の動的適用（Space等で Bobbing 確率 > 0.5f の場合に自動有効化）
            float bobbingProb = actionConfig != null ? actionConfig.GetProbability(stage, AkabekoAction.Bobbing) : 0f;
            isBobbing = (bobbingProb > 0.5f);

            if (actionConfig == null) return;

            // 各アクションの確率値を取得
            float pNone         = actionConfig.GetProbability(stage, AkabekoAction.None);
            float pFlyAway     = actionConfig.GetProbability(stage, AkabekoAction.FlyAway);
            float pSpin        = actionConfig.GetProbability(stage, AkabekoAction.Spin);
            float pShake       = actionConfig.GetProbability(stage, AkabekoAction.Shake);
            float pScalePulse  = actionConfig.GetProbability(stage, AkabekoAction.ScalePulse);
            float pSuperNova    = actionConfig.GetProbability(stage, AkabekoAction.SuperNova);
            float pWormhole     = actionConfig.GetProbability(stage, AkabekoAction.Wormhole);
            float pClones       = actionConfig.GetProbability(stage, AkabekoAction.Clones);
            float pMatrixGlitch = actionConfig.GetProbability(stage, AkabekoAction.MatrixGlitch);
            float pDiscoParty   = actionConfig.GetProbability(stage, AkabekoAction.DiscoParty);
            float pTornado      = actionConfig.GetProbability(stage, AkabekoAction.Tornado);

            float totalWeight = pNone + pFlyAway + pSpin + pShake + pScalePulse + pSuperNova + pWormhole + pClones + pMatrixGlitch + pDiscoParty + pTornado;
            if (totalWeight <= 0f) return;

            float roll = Random.value * totalWeight;

            if (roll < pNone) return; roll -= pNone;
            if (roll < pFlyAway) { ExecuteAction(AkabekoAction.FlyAway); return; } roll -= pFlyAway;
            if (roll < pSpin) { ExecuteAction(AkabekoAction.Spin); return; } roll -= pSpin;
            if (roll < pShake) { ExecuteAction(AkabekoAction.Shake); return; } roll -= pShake;
            if (roll < pScalePulse) { ExecuteAction(AkabekoAction.ScalePulse); return; } roll -= pScalePulse;
            if (roll < pSuperNova) { ExecuteAction(AkabekoAction.SuperNova); return; } roll -= pSuperNova;
            if (roll < pWormhole) { ExecuteAction(AkabekoAction.Wormhole); return; } roll -= pWormhole;
            if (roll < pClones) { ExecuteAction(AkabekoAction.Clones); return; } roll -= pClones;
            if (roll < pMatrixGlitch) { ExecuteAction(AkabekoAction.MatrixGlitch); return; } roll -= pMatrixGlitch;
            if (roll < pDiscoParty) { ExecuteAction(AkabekoAction.DiscoParty); return; } roll -= pDiscoParty;
            if (roll < pTornado) { ExecuteAction(AkabekoAction.Tornado); return; }
        }

        // ---- アドミン/テスト用パブリックメソッド ----

        public void ForceSetStage(string stageName, int durationBobs = 30)
        {
            stageRemainingBobs = durationBobs;
            if (stageManager != null)
            {
                if (stageName.Equals("Default", System.StringComparison.OrdinalIgnoreCase))
                    stageManager.ResetScene();
                else
                    stageManager.ChangeScene(stageName.ToLower());
            }
            Debug.Log($"[StageActionController] Force set stage: {stageName} ({durationBobs} bobs)");
        }

        public void ForceSetColor(string colorName, int durationBobs = 30)
        {
            activeColorState = colorName;
            colorRemainingBobs = durationBobs;
            if (rareMotionSystem != null) rareMotionSystem.SetColorByName(colorName);
            Debug.Log($"[StageActionController] Force set color: {colorName} ({durationBobs} bobs)");
        }

        public void ForceTriggerAction(AkabekoAction action)
        {
            ExecuteAction(action);
            Debug.Log($"[StageActionController] Force triggered action: {action}");
        }

        private void ExecuteAction(AkabekoAction action)
        {
            switch (action)
            {
                case AkabekoAction.FlyAway:      if (!isFlyingAway) { DynamicUIOverlay.ShowRareAlert("Action", "FlyAway"); StartCoroutine(FlyAwayCoroutine()); } break;
                case AkabekoAction.Spin:         if (!isSpinning) { DynamicUIOverlay.ShowRareAlert("Action", "Spin"); StartCoroutine(SpinCoroutine()); } break;
                case AkabekoAction.Shake:        if (!isShaking) { DynamicUIOverlay.ShowRareAlert("Action", "Shake"); StartCoroutine(ShakeCoroutine()); } break;
                case AkabekoAction.ScalePulse:   if (!isScalePulsing) { DynamicUIOverlay.ShowRareAlert("Action", "SquashBounce"); StartCoroutine(ScalePulseCoroutine()); } break;
                case AkabekoAction.SuperNova:    if (!isSuperNova) { DynamicUIOverlay.ShowRareAlert("Action", "SuperNova"); StartCoroutine(SuperNovaCoroutine()); } break;
                case AkabekoAction.Wormhole:     if (!isWormhole) { DynamicUIOverlay.ShowRareAlert("Action", "Wormhole"); StartCoroutine(WormholeCoroutine()); } break;
                case AkabekoAction.Clones:       if (!isClones) { DynamicUIOverlay.ShowRareAlert("Action", "Clones"); StartCoroutine(ClonesCoroutine()); } break;
                case AkabekoAction.MatrixGlitch: if (!isMatrixGlitch) { DynamicUIOverlay.ShowRareAlert("Action", "MatrixGlitch"); StartCoroutine(MatrixGlitchCoroutine()); } break;
                case AkabekoAction.DiscoParty:   if (!isDiscoParty) { DynamicUIOverlay.ShowRareAlert("Action", "DiscoParty"); StartCoroutine(DiscoPartyCoroutine()); } break;
                case AkabekoAction.Tornado:      if (!isTornado) { DynamicUIOverlay.ShowRareAlert("Action", "Tornado"); StartCoroutine(TornadoCoroutine()); } break;
            }
        }

        /// <summary>1. 超新星爆発（SuperNova）: 光の溜め＋カメラズーム＋衝撃波爆発</summary>
        private IEnumerator SuperNovaCoroutine()
        {
            isSuperNova = true;
            physicsFrozen = true;

            Vector3 startScale = initialLocalScale;
            Vector3 chargeScale = initialLocalScale * 1.6f;
            float elapsed = 0f;
            float chargeDuration = 0.8f;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / chargeDuration;
                float ease = t * t * t;
                transform.localScale = Vector3.Lerp(startScale, chargeScale, ease);
                transform.localRotation = initialLocalRotation * Quaternion.Euler(UnityEngine.Random.insideUnitSphere * 15f * t);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            if (mainCam != null) mainCam.transform.position += UnityEngine.Random.insideUnitSphere * 0.3f;
            yield return new WaitForSeconds(0.15f);

            elapsed = 0f;
            float popDuration = 0.5f;
            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                float elastic = Mathf.Sin(t * Mathf.PI * 2.5f) * Mathf.Exp(-3f * t);
                transform.localScale = Vector3.Lerp(startScale * 1.2f, startScale, t) + Vector3.one * elastic * 0.2f;
                yield return null;
            }

            transform.localScale = startScale;
            transform.localRotation = initialLocalRotation;
            physicsFrozen = false;
            isSuperNova = false;
        }

        /// <summary>2. ワームホール（Wormhole）: 渦に吸い込まれ、上空からポッピン復帰</summary>
        private IEnumerator WormholeCoroutine()
        {
            isWormhole = true;
            physicsFrozen = true;

            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            Vector3 startScale = transform.localScale;

            float elapsed = 0f;
            float suckedDuration = 0.7f;
            while (elapsed < suckedDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / suckedDuration;
                float ease = t * t;
                transform.localRotation = startRot * Quaternion.Euler(0f, t * 720f, 0f);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
                yield return null;
            }
            transform.localScale = Vector3.zero;

            yield return new WaitForSeconds(0.4f);

            Vector3 dropStartPos = startPos + new Vector3(0f, 4f, 0f);
            transform.localPosition = dropStartPos;
            transform.localScale = startScale;

            elapsed = 0f;
            float dropDuration = 0.5f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dropDuration;
                float bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 1.5f)) * (1f - t);
                transform.localPosition = Vector3.Lerp(dropStartPos, startPos, t) + new Vector3(0f, bounce * 0.6f, 0f);
                yield return null;
            }

            transform.localPosition = startPos;
            transform.localRotation = initialLocalRotation;
            physicsFrozen = false;
            isWormhole = false;
        }

        /// <summary>3. 分身の術（Clones）: 左右にホログラム分身出現＋シンクロ首振り</summary>
        private IEnumerator ClonesCoroutine()
        {
            isClones = true;

            GameObject cloneLeft = Instantiate(gameObject, transform.parent);
            GameObject cloneRight = Instantiate(gameObject, transform.parent);

            Destroy(cloneLeft.GetComponent<StageActionController>());
            Destroy(cloneRight.GetComponent<StageActionController>());

            Vector3 basePos = transform.localPosition;
            cloneLeft.transform.localPosition = basePos + new Vector3(-1.4f, 0f, 0f);
            cloneRight.transform.localPosition = basePos + new Vector3(1.4f, 0f, 0f);

            Renderer[] rLeft = cloneLeft.GetComponentsInChildren<Renderer>();
            Renderer[] rRight = cloneRight.GetComponentsInChildren<Renderer>();
            foreach (var r in rLeft) if (r.material.HasProperty("_Color")) r.material.color = new Color(0.3f, 0.8f, 1f, 0.7f);
            foreach (var r in rRight) if (r.material.HasProperty("_Color")) r.material.color = new Color(1f, 0.3f, 0.8f, 0.7f);

            float elapsed = 0f;
            float duration = 2.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float sway = Mathf.Sin(elapsed * 4f) * 0.1f;
                if (cloneLeft != null) cloneLeft.transform.localPosition = basePos + new Vector3(-1.4f, sway, 0f);
                if (cloneRight != null) cloneRight.transform.localPosition = basePos + new Vector3(1.4f, -sway, 0f);
                yield return null;
            }

            elapsed = 0f;
            float mergeDuration = 0.4f;
            while (elapsed < mergeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / mergeDuration;
                if (cloneLeft != null) cloneLeft.transform.localPosition = Vector3.Lerp(basePos + new Vector3(-1.4f, 0f, 0f), basePos, t);
                if (cloneRight != null) cloneRight.transform.localPosition = Vector3.Lerp(basePos + new Vector3(1.4f, 0f, 0f), basePos, t);
                yield return null;
            }

            Destroy(cloneLeft);
            Destroy(cloneRight);
            isClones = false;
        }

        /// <summary>4. サイバー・グリッチ（MatrixGlitch）: RGBブレ＆デジタルコマ送り</summary>
        private IEnumerator MatrixGlitchCoroutine()
        {
            isMatrixGlitch = true;
            Vector3 basePos = transform.localPosition;
            Vector3 baseScale = transform.localScale;

            float elapsed = 0f;
            float duration = 0.7f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * 0.12f;
                Vector3 scaleJitter = new Vector3(
                    1f + UnityEngine.Random.Range(-0.2f, 0.3f),
                    1f + UnityEngine.Random.Range(-0.2f, 0.3f),
                    1f + UnityEngine.Random.Range(-0.2f, 0.3f)
                );

                transform.localPosition = basePos + jitter;
                transform.localScale = Vector3.Scale(baseScale, scaleJitter);
                yield return new WaitForSeconds(0.04f);
            }

            transform.localPosition = basePos;
            transform.localScale = baseScale;
            isMatrixGlitch = false;
        }

        /// <summary>5. ディスコ・ミラーボールダンス（DiscoParty）: ノリノリフィーバーダンス</summary>
        private IEnumerator DiscoPartyCoroutine()
        {
            isDiscoParty = true;
            Vector3 basePos = transform.localPosition;
            Quaternion baseRot = transform.localRotation;

            float elapsed = 0f;
            float duration = 2.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed * 8f;
                float bounceY = Mathf.Abs(Mathf.Sin(t)) * 0.25f;
                float tiltZ = Mathf.Sin(t * 0.5f) * 18f;
                float yawY = Mathf.Cos(t * 0.25f) * 25f;

                transform.localPosition = basePos + new Vector3(Mathf.Sin(t * 0.5f) * 0.15f, bounceY, 0f);
                transform.localRotation = baseRot * Quaternion.Euler(0f, yawY, tiltZ);
                yield return null;
            }

            transform.localPosition = basePos;
            transform.localRotation = baseRot;
            isDiscoParty = false;
        }

        /// <summary>6. 超高速竜巻スピン（Tornado）: 嵐を纏う高速独楽回転</summary>
        private IEnumerator TornadoCoroutine()
        {
            isTornado = true;
            physicsFrozen = true;
            Vector3 basePos = transform.localPosition;
            Quaternion baseRot = transform.localRotation;

            float elapsed = 0f;
            float duration = 1.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float spinSpeed = Mathf.Sin(t * Mathf.PI) * 1600f;
                transform.localRotation = baseRot * Quaternion.Euler(0f, spinSpeed * elapsed, 0f);
                transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.3f, 0f);
                yield return null;
            }

            elapsed = 0f;
            float wobbleDuration = 0.6f;
            while (elapsed < wobbleDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / wobbleDuration;
                float wobble = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 12f;
                transform.localRotation = baseRot * Quaternion.Euler(wobble, 0f, wobble * 0.5f);
                yield return null;
            }

            transform.localPosition = basePos;
            transform.localRotation = baseRot;
            physicsFrozen = false;
            isTornado = false;
        }

        private ParticleSystem stardustParticleSystem;

        // ---- Update ----

        private void Update()
        {
            if (stageManager == null) return;
            bool isSpace = stageManager.ActiveStage.Equals("space", System.StringComparison.OrdinalIgnoreCase);

            // Bobbingウェイトの滑らかな変化
            float targetWeight = isBobbing ? 1f : 0f;
            bobbingWeight = Mathf.MoveTowards(bobbingWeight, targetWeight, Time.deltaTime * 2.0f);

            // 宇宙スターダスト粒子の動的生成＆ON/OFF制御
            UpdateStardustParticles(isSpace && bobbingWeight > 0.1f);

            // モノクロ線画ステージでのモーション連動（赤べこの動きでスキャンラインを横に波打たせる）
            if (stageManager.ActiveStage.Equals("monoline", System.StringComparison.OrdinalIgnoreCase))
            {
                float motionSpeed = driftVelocity.magnitude * 2.5f + (isSpinning || isTornado || isSuperNova ? 4.0f : 0.8f);

                Material monoMat = stageManager.GetMonoLineMaterial();
                if (monoMat != null)
                {
                    if (monoMat.HasProperty("_MotionAmount"))
                        monoMat.SetFloat("_MotionAmount", motionSpeed);
                    
                    float offsetY = (Time.time * 0.2f + Mathf.Sin(Time.time * 10f) * motionSpeed * 0.03f) % 1f;
                    monoMat.mainTextureOffset = new Vector2(0f, offsetY);
                }

                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    ScanlinePostProcess pp = mainCam.GetComponent<ScanlinePostProcess>();
                    if (pp != null && pp.isEffectActive)
                    {
                        pp.motionAmount = Mathf.Lerp(pp.motionAmount, motionSpeed, Time.deltaTime * 5f);
                    }
                }
            }

            // クリックで漂い入力（Bobbing有効時のみ）
            if (isBobbing && !isFlyingAway && Input.GetMouseButtonDown(0))
                DetectClick();

            // スカイボックス視差（宇宙ステージ & Bobbing有効時）
            if (isSpace && isBobbing && !physicsFrozen)
            {
                if (spaceMaterial == null && RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
                {
                    spaceMaterial = RenderSettings.skybox;
                    originalSkyboxRotation = spaceMaterial.GetFloat("_Rotation");
                    hasSavedSkyboxRotation = true;
                }

                if (spaceMaterial != null)
                {
                    skyboxTimeRotation += Time.deltaTime * skyboxRotationSpeed;
                    Vector3 camOffset = mainCam != null ? mainCam.transform.position - initialCameraPosition : Vector3.zero;
                    float parallaxOffset = (camOffset.x - camOffset.z * 0.5f) * skyboxParallaxMultiplier;
                    spaceMaterial.SetFloat("_Rotation", (originalSkyboxRotation + skyboxTimeRotation + parallaxOffset) % 360f);
                }
            }

            // 漂い物理演算（Bobbing有効 & フィジクスフリーズ中でない場合）
            if (bobbingWeight > 0.01f && !physicsFrozen)
            {
                UpdateDriftPhysics(isSpace);
            }
            else if (bobbingWeight <= 0.01f)
            {
                driftVelocity = Vector3.zero;
                driftAngularVelocity = Vector3.zero;
                currentDriftPosition = Vector3.Lerp(currentDriftPosition, Vector3.zero, Time.deltaTime * 5f);
                currentDriftRotation = Quaternion.Slerp(currentDriftRotation, Quaternion.identity, Time.deltaTime * 5f);
            }

            // 宇宙空間ならではの有機的な無重力3D浮遊（複素パーリンノイズ + スロータンブリング自転）
            Vector3 bobPos = Vector3.zero;
            Quaternion bobRot = Quaternion.identity;

            if (bobbingWeight > 0.01f)
            {
                float t = Time.time;
                // 水中/水面のような規則的単一サイン波ではなく、無重力空間のゆったりとした3D漂い
                float nX = (Mathf.PerlinNoise(t * 0.18f, 1.2f) - 0.5f) * 2.2f;
                float nY = (Mathf.Sin(t * 0.35f) + Mathf.Cos(t * 0.22f) * 0.6f) * 0.35f;
                float nZ = (Mathf.PerlinNoise(2.4f, t * 0.14f) - 0.5f) * 1.8f;

                bobPos = new Vector3(nX * bobRange, nY * bobRange * 1.4f, nZ * bobRange * 0.8f);

                // 宇宙空間でのゆっくりとした軸回転・自転・傾き（無重力タンブリング）
                float pitch = Mathf.Sin(t * 0.22f) * 8.0f + Mathf.Cos(t * 0.38f) * 4.0f;
                float yaw   = (t * 6.0f) % 360f; // 1分間に1周する非常にゆっくりとした宇宙自転
                float roll  = Mathf.Sin(t * 0.16f) * 10.0f;

                bobRot = Quaternion.Euler(pitch, yaw, roll);
            }

            // 最終座標と回転を滑らかに合成
            Vector3 targetPos = initialLocalPosition + Vector3.Lerp(Vector3.zero, bobPos + currentDriftPosition, bobbingWeight);
            Quaternion targetRot = Quaternion.Slerp(initialLocalRotation, initialLocalRotation * bobRot * currentDriftRotation, bobbingWeight);

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        private void UpdateDriftPhysics(bool isSpace)
        {
            // 宇宙空間ではバネ力を超ソフト（0.35f）にしてフワフワ感と無重力の慣性を表現
            float springStr = isSpace ? 0.35f : returnSpringStrength;
            float damp = isSpace ? 0.95f : (1f - driftDamping);

            // 位置の引き戻しバネ
            Vector3 returnForce = (Vector3.zero - currentDriftPosition) * springStr;
            driftVelocity += returnForce * Time.deltaTime;
            driftVelocity = Vector3.Lerp(driftVelocity, Vector3.zero, Time.deltaTime * (1f - damp));
            currentDriftPosition += driftVelocity * Time.deltaTime;

            // 回転の引き戻しトルク
            Quaternion deltaRot = Quaternion.identity * Quaternion.Inverse(currentDriftRotation);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude > 0.001f && Mathf.Abs(angle) > 0.01f)
                driftAngularVelocity += axis.normalized * angle * springStr * 0.3f * Time.deltaTime;

            driftAngularVelocity = Vector3.Lerp(driftAngularVelocity, Vector3.zero, Time.deltaTime * (1f - damp));
            currentDriftRotation = Quaternion.Euler(driftAngularVelocity * Time.deltaTime) * currentDriftRotation;
        }

        /// <summary>
        /// 宇宙空間の深みと速度感を演出する宇宙ダスト（Stardust）パーティクル
        /// </summary>
        private void UpdateStardustParticles(bool active)
        {
            if (active)
            {
                if (stardustParticleSystem == null)
                {
                    GameObject pGo = new GameObject("SpaceStardustParticles");
                    pGo.transform.SetParent(transform, false);

                    stardustParticleSystem = pGo.AddComponent<ParticleSystem>();
                    var main = stardustParticleSystem.main;
                    main.duration = 5f;
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                    main.startColor = new Color(0.85f, 0.80f, 1.0f, 0.65f);
                    main.maxParticles = 70;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var emission = stardustParticleSystem.emission;
                    emission.rateOverTime = 12;

                    var shape = stardustParticleSystem.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(5f, 5f, 5f);

                    var vel = stardustParticleSystem.velocityOverLifetime;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.z = new ParticleSystem.MinMaxCurve(-0.08f, -0.02f);

                    var renderer = stardustParticleSystem.GetComponent<ParticleSystemRenderer>();
                    Material pMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default"));
                    renderer.material = pMat;
                }

                if (!stardustParticleSystem.isPlaying) stardustParticleSystem.Play();
            }
            else
            {
                if (stardustParticleSystem != null && stardustParticleSystem.isPlaying)
                {
                    stardustParticleSystem.Stop();
                }
            }
        }

        // ---- アクション コルーチン ----

        /// <summary>カメラ外に飛んでいき、ふわっと帰ってくる</summary>
        private IEnumerator FlyAwayCoroutine()
        {
            isFlyingAway = true;
            physicsFrozen = true;

            if (mainCam == null) mainCam = Camera.main;

            // 8方向からランダムに飛び先を選択
            Vector2[] viewportEdges = {
                new Vector2(-0.5f, 0.5f), new Vector2(1.5f, 0.5f),
                new Vector2(0.5f, -0.5f), new Vector2(0.5f, 1.5f),
                new Vector2(-0.4f, -0.4f), new Vector2(1.4f, -0.4f),
                new Vector2(-0.4f, 1.4f), new Vector2(1.4f, 1.4f),
            };
            Vector2 vp = viewportEdges[Random.Range(0, viewportEdges.Length)];
            float dist = mainCam != null ? Vector3.Distance(mainCam.transform.position, transform.position) : 5f;

            Vector3 targetWorldPos = transform.position;
            if (mainCam != null)
            {
                Vector3 center = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, dist));
                Vector3 edge   = mainCam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, dist));
                targetWorldPos = center + (edge - center) * flyAwayExitMargin;
            }

            Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 targetDrift = currentDriftPosition + Quaternion.Inverse(parentRot) * (targetWorldPos - transform.position);
            Vector3 torqueDir = Random.onUnitSphere;

            float elapsed = 0f;
            Vector3 startDrift = currentDriftPosition;

            while (elapsed < flyAwayExitDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flyAwayExitDuration;
                currentDriftPosition = Vector3.Lerp(startDrift, targetDrift, t * t);
                currentDriftRotation = Quaternion.Euler(torqueDir * flyAwayTorque * Time.deltaTime) * currentDriftRotation;
                yield return null;
            }

            yield return new WaitForSeconds(flyAwayReturnDelay);

            physicsFrozen = false;
            driftVelocity = Vector3.zero;
            driftAngularVelocity = torqueDir * flyAwayTorque * 0.04f;
            isFlyingAway = false;
        }

        /// <summary>クルクルと1回転して戻る</summary>
        private IEnumerator SpinCoroutine()
        {
            isSpinning = true;
            Vector3 axis = Vector3.up;
            float elapsed = 0f;
            Quaternion startRot = currentDriftRotation;

            while (elapsed < spinDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / spinDuration;
                // 1回転 (360度) してから元に戻る
                float angle = Mathf.Sin(t * Mathf.PI) * spinTorque * Time.deltaTime;
                currentDriftRotation = Quaternion.Euler(axis * angle) * currentDriftRotation;
                yield return null;
            }
            isSpinning = false;
        }

        /// <summary>ブルブルと震える</summary>
        private IEnumerator ShakeCoroutine()
        {
            isShaking = true;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - (elapsed / shakeDuration);
                Vector3 offset = Random.insideUnitSphere * shakeIntensity * fade;
                transform.localPosition = initialLocalPosition + currentDriftPosition + offset;
                yield return null;
            }

            isShaking = false;
        }

        /// <summary>大きくなってから元のサイズに戻る</summary>
        private IEnumerator ScalePulseCoroutine()
        {
            isScalePulsing = true;
            float half = scalePulseDuration * 0.5f;
            float elapsed = 0f;

            // 大きくなる
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                transform.localScale = Vector3.Lerp(initialLocalScale, initialLocalScale * scalePulseMultiplier, t);
                yield return null;
            }
            // 元に戻る
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                transform.localScale = Vector3.Lerp(initialLocalScale * scalePulseMultiplier, initialLocalScale, t);
                yield return null;
            }
            transform.localScale = initialLocalScale;
            isScalePulsing = false;
        }

        // ---- クリック入力 ----

        private void DetectClick()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                {
                    Vector3 pushImpulse = (ray.direction * 0.8f + Random.onUnitSphere * 0.2f).normalized * clickImpulseForce;
                    driftVelocity += pushImpulse;
                    driftAngularVelocity += Random.onUnitSphere * clickTorqueForce;
                }
            }
        }
    }
}
