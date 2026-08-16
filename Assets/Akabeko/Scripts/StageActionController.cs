using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Akabeko
{
    /// <summary>
    /// ステージ × アクション確率マトリクス（StageActionConfig）を参照して、
    /// 赤べこのアクション演出（Bobbing / FlyAway / Spin / Shake / ScalePulse / SuperNova / Wormhole / Clones / MatrixGlitch / DiscoParty / Tornado）
    /// を排他制御・復帰保証付きで統合制御するクラス。
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

        // 内部状態・初期値キャッシュ
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
        private bool isSuperNova = false;
        private bool isWormhole = false;
        private bool isClones = false;
        private bool isMatrixGlitch = false;
        private bool isDiscoParty = false;
        private bool isTornado = false;

        // 排他制御フラグ & クールダウン
        private bool isActionExecuting = false;
        private bool physicsFrozen = false;
        private float actionCooldownTimer = 0f;
        private const float ACTION_COOLDOWN = 2.5f;

        private List<GameObject> activeSpawnedObjects = new List<GameObject>();
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

            StopAllActionsAndRestore();

            if (hasSavedSkyboxRotation && spaceMaterial != null)
                spaceMaterial.SetFloat("_Rotation", originalSkyboxRotation);
        }

        // ---- イベントハンドラー ----

        private void HandleStageChanged(string stageName)
        {
            StopAllActionsAndRestore();

            float bobbingProb = actionConfig != null
                ? actionConfig.GetProbability(stageName, AkabekoAction.Bobbing)
                : (stageName.Equals("space", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f);

            isBobbing = (bobbingProb > 0.5f);

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

            // 3. アクション発動管理（排他制御中またはクールダウン中は判定をスキップ）
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

        /// <summary>
        /// 現在のステージに応じたアクション判定（排他制御・クールダウン保証）
        /// </summary>
        private void EvaluateActionTrigger()
        {
            if (stageManager == null) return;
            string stage = stageManager.ActiveStage;

            // Bobbing（浮遊）の動的適用
            float bobbingProb = actionConfig != null ? actionConfig.GetProbability(stage, AkabekoAction.Bobbing) : 0f;
            isBobbing = (bobbingProb > 0.5f);

            // アクション実行中またはクールダウン中であれば新規発動はスキップ
            if (isActionExecuting || actionCooldownTimer > 0f) return;
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

        // ---- パブリック制御メソッド ----

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
            StopAllActionsAndRestore();
            ExecuteAction(action);
            Debug.Log($"[StageActionController] Force triggered action: {action}");
        }

        /// <summary>
        /// 実行中の全アクションを強制終了し、トランスフォーム・カメラ・オブジェクトを完全復元する
        /// </summary>
        public void StopAllActionsAndRestore()
        {
            StopAllCoroutines();

            isActionExecuting = false;
            physicsFrozen = false;
            isSuperNova = false;
            isWormhole = false;
            isClones = false;
            isMatrixGlitch = false;
            isDiscoParty = false;
            isTornado = false;
            isFlyingAway = false;
            isSpinning = false;
            isShaking = false;
            isScalePulsing = false;

            transform.localScale = initialLocalScale;
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;

            currentDriftPosition = Vector3.zero;
            currentDriftRotation = Quaternion.identity;
            driftVelocity = Vector3.zero;
            driftAngularVelocity = Vector3.zero;

            if (mainCam == null) mainCam = Camera.main;
            if (mainCam != null) mainCam.transform.position = initialCameraPosition;

            foreach (var obj in activeSpawnedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            activeSpawnedObjects.Clear();

            actionCooldownTimer = 1.0f;
        }

        private void ExecuteAction(AkabekoAction action)
        {
            if (isActionExecuting) return;

            switch (action)
            {
                case AkabekoAction.FlyAway:      DynamicUIOverlay.ShowRareAlert("Action", "FlyAway"); StartCoroutine(FlyAwayCoroutine()); break;
                case AkabekoAction.Spin:         DynamicUIOverlay.ShowRareAlert("Action", "Spin"); StartCoroutine(SpinCoroutine()); break;
                case AkabekoAction.Shake:        DynamicUIOverlay.ShowRareAlert("Action", "Shake"); StartCoroutine(ShakeCoroutine()); break;
                case AkabekoAction.ScalePulse:   DynamicUIOverlay.ShowRareAlert("Action", "SquashBounce"); StartCoroutine(ScalePulseCoroutine()); break;
                case AkabekoAction.SuperNova:    DynamicUIOverlay.ShowRareAlert("Action", "SuperNova"); StartCoroutine(SuperNovaCoroutine()); break;
                case AkabekoAction.Wormhole:     DynamicUIOverlay.ShowRareAlert("Action", "Wormhole"); StartCoroutine(WormholeCoroutine()); break;
                case AkabekoAction.Clones:       DynamicUIOverlay.ShowRareAlert("Action", "Clones"); StartCoroutine(ClonesCoroutine()); break;
                case AkabekoAction.MatrixGlitch: DynamicUIOverlay.ShowRareAlert("Action", "MatrixGlitch"); StartCoroutine(MatrixGlitchCoroutine()); break;
                case AkabekoAction.DiscoParty:   DynamicUIOverlay.ShowRareAlert("Action", "DiscoParty"); StartCoroutine(DiscoPartyCoroutine()); break;
                case AkabekoAction.Tornado:      DynamicUIOverlay.ShowRareAlert("Action", "Tornado"); StartCoroutine(TornadoCoroutine()); break;
            }
        }

        // ================= アクション コルーチン群（try-finally復帰保証） =================

        /// <summary>1. 超新星爆発（SuperNova）: 光の溜め＋カメラズーム＋衝撃波爆発</summary>
        private IEnumerator SuperNovaCoroutine()
        {
            isActionExecuting = true;
            isSuperNova = true;
            physicsFrozen = true;

            Vector3 startScale = initialLocalScale;
            Vector3 chargeScale = initialLocalScale * 1.6f;

            try
            {
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
                if (mainCam != null) mainCam.transform.position = initialCameraPosition + UnityEngine.Random.insideUnitSphere * 0.3f;
                yield return new WaitForSeconds(0.15f);

                elapsed = 0f;
                float popDuration = 0.5f;
                while (elapsed < popDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / popDuration;
                    float elastic = Mathf.Sin(t * Mathf.PI * 2.5f) * Mathf.Exp(-3f * t);
                    transform.localScale = Vector3.Lerp(startScale * 1.2f, startScale, t) + Vector3.one * elastic * 0.2f;
                    if (mainCam != null) mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, initialCameraPosition, t);
                    yield return null;
                }
            }
            finally
            {
                transform.localScale = initialLocalScale;
                transform.localRotation = initialLocalRotation;
                if (mainCam != null) mainCam.transform.position = initialCameraPosition;
                physicsFrozen = false;
                isSuperNova = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>2. ワームホール（Wormhole）: 渦に吸い込まれ、上空からポッピン復帰</summary>
        private IEnumerator WormholeCoroutine()
        {
            isActionExecuting = true;
            isWormhole = true;
            physicsFrozen = true;

            Vector3 startPos = initialLocalPosition;
            Quaternion startRot = initialLocalRotation;
            Vector3 startScale = initialLocalScale;

            try
            {
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
            }
            finally
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
                transform.localScale = initialLocalScale;
                physicsFrozen = false;
                isWormhole = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>3. 分身の術（Clones）: 左右にホログラム分身出現＋シンクロ首振り</summary>
        private IEnumerator ClonesCoroutine()
        {
            isActionExecuting = true;
            isClones = true;

            GameObject cloneLeft = null;
            GameObject cloneRight = null;

            try
            {
                cloneLeft = Instantiate(gameObject, transform.parent);
                cloneRight = Instantiate(gameObject, transform.parent);
                activeSpawnedObjects.Add(cloneLeft);
                activeSpawnedObjects.Add(cloneRight);

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
            }
            finally
            {
                if (cloneLeft != null) { activeSpawnedObjects.Remove(cloneLeft); Destroy(cloneLeft); }
                if (cloneRight != null) { activeSpawnedObjects.Remove(cloneRight); Destroy(cloneRight); }
                isClones = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>4. サイバー・グリッチ（MatrixGlitch）: RGBブレ＆デジタルコマ送り</summary>
        private IEnumerator MatrixGlitchCoroutine()
        {
            isActionExecuting = true;
            isMatrixGlitch = true;
            Vector3 basePos = transform.localPosition;
            Vector3 baseScale = initialLocalScale;

            try
            {
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
            }
            finally
            {
                transform.localPosition = initialLocalPosition;
                transform.localScale = initialLocalScale;
                isMatrixGlitch = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>5. ディスコ・ミラーボールダンス（DiscoParty）: ノリノリフィーバーダンス</summary>
        private IEnumerator DiscoPartyCoroutine()
        {
            isActionExecuting = true;
            isDiscoParty = true;
            Vector3 basePos = transform.localPosition;
            Quaternion baseRot = initialLocalRotation;

            try
            {
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
            }
            finally
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
                isDiscoParty = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>6. 超高速竜巻スピン（Tornado）: 嵐を纏う高速独楽回転</summary>
        private IEnumerator TornadoCoroutine()
        {
            isActionExecuting = true;
            isTornado = true;
            physicsFrozen = true;
            Vector3 basePos = transform.localPosition;
            Quaternion baseRot = initialLocalRotation;

            try
            {
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
            }
            finally
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
                physicsFrozen = false;
                isTornado = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>7. カメラ外に飛んでいき、ふわっと帰ってくる（FlyAway）</summary>
        private IEnumerator FlyAwayCoroutine()
        {
            isActionExecuting = true;
            isFlyingAway = true;
            physicsFrozen = true;

            if (mainCam == null) mainCam = Camera.main;

            try
            {
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
                    transform.localPosition = initialLocalPosition + currentDriftPosition;
                    transform.localRotation = initialLocalRotation * currentDriftRotation;
                    yield return null;
                }

                yield return new WaitForSeconds(flyAwayReturnDelay);

                elapsed = 0f;
                float returnDuration = 0.8f;
                Vector3 returnStartDrift = currentDriftPosition;
                Quaternion returnStartRot = currentDriftRotation;

                while (elapsed < returnDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / returnDuration;
                    float ease = 1f - Mathf.Pow(1f - t, 3f);
                    currentDriftPosition = Vector3.Lerp(returnStartDrift, Vector3.zero, ease);
                    currentDriftRotation = Quaternion.Slerp(returnStartRot, Quaternion.identity, ease);
                    transform.localPosition = initialLocalPosition + currentDriftPosition;
                    transform.localRotation = initialLocalRotation * currentDriftRotation;
                    yield return null;
                }
            }
            finally
            {
                currentDriftPosition = Vector3.zero;
                currentDriftRotation = Quaternion.identity;
                driftVelocity = Vector3.zero;
                driftAngularVelocity = Vector3.zero;
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
                physicsFrozen = false;
                isFlyingAway = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>8. クルクルと1回転して戻る（Spin）</summary>
        private IEnumerator SpinCoroutine()
        {
            isActionExecuting = true;
            isSpinning = true;

            try
            {
                Vector3 axis = Vector3.up;
                float elapsed = 0f;
                Quaternion startRot = initialLocalRotation;

                while (elapsed < spinDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / spinDuration;
                    float currentAngle = (1f - Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f * 360f;
                    transform.localRotation = startRot * Quaternion.Euler(axis * currentAngle);
                    yield return null;
                }
            }
            finally
            {
                transform.localRotation = initialLocalRotation;
                currentDriftRotation = Quaternion.identity;
                isSpinning = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>9. ブルブルと震える（Shake）</summary>
        private IEnumerator ShakeCoroutine()
        {
            isActionExecuting = true;
            isShaking = true;
            Vector3 basePos = transform.localPosition;

            try
            {
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    elapsed += Time.deltaTime;
                    float fade = 1f - (elapsed / shakeDuration);
                    Vector3 offset = Random.insideUnitSphere * shakeIntensity * fade;
                    transform.localPosition = basePos + offset;
                    yield return null;
                }
            }
            finally
            {
                transform.localPosition = initialLocalPosition;
                isShaking = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        /// <summary>10. 大きくなってから元のサイズに戻る（SquashBounce / ScalePulse）</summary>
        private IEnumerator ScalePulseCoroutine()
        {
            isActionExecuting = true;
            isScalePulsing = true;
            Vector3 baseScale = initialLocalScale;

            try
            {
                float half = scalePulseDuration * 0.5f;
                float elapsed = 0f;

                while (elapsed < half)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / half;
                    transform.localScale = Vector3.Lerp(baseScale, baseScale * scalePulseMultiplier, t);
                    yield return null;
                }

                elapsed = 0f;
                while (elapsed < half)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / half;
                    transform.localScale = Vector3.Lerp(baseScale * scalePulseMultiplier, baseScale, t);
                    yield return null;
                }
            }
            finally
            {
                transform.localScale = initialLocalScale;
                isScalePulsing = false;
                isActionExecuting = false;
                actionCooldownTimer = ACTION_COOLDOWN;
            }
        }

        private ParticleSystem stardustParticleSystem;

        // ---- Update ----

        private void Update()
        {
            if (actionCooldownTimer > 0f) actionCooldownTimer -= Time.deltaTime;
            if (stageManager == null) return;

            bool isSpace = stageManager.ActiveStage.Equals("space", System.StringComparison.OrdinalIgnoreCase);

            // Bobbingウェイトの滑らかな変化
            float targetWeight = isBobbing ? 1f : 0f;
            bobbingWeight = Mathf.MoveTowards(bobbingWeight, targetWeight, Time.deltaTime * 2.0f);

            // 宇宙スターダスト粒子の動的生成＆ON/OFF制御
            UpdateStardustParticles(isSpace && bobbingWeight > 0.1f);

            // モノクロ線画ステージでのモーション連動
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

            // クリックで漂い入力（Bobbing有効 & アクション中でない場合）
            if (isBobbing && !isActionExecuting && !isFlyingAway && Input.GetMouseButtonDown(0))
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

            // 漂い物理演算（Bobbing有効 & アクション実行中でない場合）
            if (bobbingWeight > 0.01f && !isActionExecuting && !physicsFrozen)
            {
                UpdateDriftPhysics(isSpace);
            }
            else if (bobbingWeight <= 0.01f && !isActionExecuting)
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
                float nX = (Mathf.PerlinNoise(t * 0.18f, 1.2f) - 0.5f) * 2.2f;
                float nY = (Mathf.Sin(t * 0.35f) + Mathf.Cos(t * 0.22f) * 0.6f) * 0.35f;
                float nZ = (Mathf.PerlinNoise(2.4f, t * 0.14f) - 0.5f) * 1.8f;

                bobPos = new Vector3(nX * bobRange, nY * bobRange * 1.4f, nZ * bobRange * 0.8f);

                float pitch = Mathf.Sin(t * 0.22f) * 8.0f + Mathf.Cos(t * 0.38f) * 4.0f;
                float yaw   = (t * 6.0f) % 360f;
                float roll  = Mathf.Sin(t * 0.16f) * 10.0f;

                bobRot = Quaternion.Euler(pitch, yaw, roll);
            }

            // アクションが実行中でない場合のみ、Update() がトランスフォームを合成
            if (!isActionExecuting && !physicsFrozen)
            {
                Vector3 targetPos = initialLocalPosition + Vector3.Lerp(Vector3.zero, bobPos + currentDriftPosition, bobbingWeight);
                Quaternion targetRot = Quaternion.Slerp(initialLocalRotation, initialLocalRotation * bobRot * currentDriftRotation, bobbingWeight);

                transform.localPosition = targetPos;
                transform.localRotation = targetRot;
            }
        }

        private void UpdateDriftPhysics(bool isSpace)
        {
            float springStr = isSpace ? 0.35f : returnSpringStrength;
            float damp = isSpace ? 0.95f : (1f - driftDamping);

            Vector3 returnForce = (Vector3.zero - currentDriftPosition) * springStr;
            driftVelocity += returnForce * Time.deltaTime;
            driftVelocity = Vector3.Lerp(driftVelocity, Vector3.zero, Time.deltaTime * (1f - damp));
            currentDriftPosition += driftVelocity * Time.deltaTime;

            Quaternion deltaRot = Quaternion.identity * Quaternion.Inverse(currentDriftRotation);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude > 0.001f && Mathf.Abs(angle) > 0.01f)
                driftAngularVelocity += axis.normalized * angle * springStr * 0.3f * Time.deltaTime;

            driftAngularVelocity = Vector3.Lerp(driftAngularVelocity, Vector3.zero, Time.deltaTime * (1f - damp));
            currentDriftRotation = Quaternion.Euler(driftAngularVelocity * Time.deltaTime) * currentDriftRotation;
        }

        private void UpdateStardustParticles(bool enable)
        {
            if (enable)
            {
                if (stardustParticleSystem == null)
                {
                    GameObject go = new GameObject("SpaceStardustFX");
                    go.transform.SetParent(transform, false);
                    activeSpawnedObjects.Add(go);

                    stardustParticleSystem = go.AddComponent<ParticleSystem>();
                    var main = stardustParticleSystem.main;
                    main.maxParticles = 60;
                    main.startLifetime = 3.5f;
                    main.startSpeed = 0.15f;
                    main.startSize = 0.04f;
                    main.startColor = new Color(0.7f, 0.9f, 1f, 0.6f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var emission = stardustParticleSystem.emission;
                    emission.rateOverTime = 12f;

                    var shape = stardustParticleSystem.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 2.0f;
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
