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

        // ---- Unity Lifecycle ----

        private void Awake()
        {
            // OnEnableより前にAwakeで参照を取得する（OnEnableでイベント購読するため）
            stageManager = FindFirstObjectByType<StageManager>();
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
            // StageActionConfigはStageManager.Start()で追加されるため、全てのAwake完了後のStartで取得
            actionConfig = FindFirstObjectByType<StageActionConfig>();

            // OnEnable時に参照がまだなかった場合、改めて購読
            if (stageManager != null)
            {
                stageManager.OnStageChanged -= HandleStageChanged; // 二重購読防止
                stageManager.OnStageChanged += HandleStageChanged;
            }
            if (neckPhysics != null)
            {
                neckPhysics.OnNeckShakeCounted -= HandleShakeTrigger;
                neckPhysics.OnNeckShakeCounted += HandleShakeTrigger;
            }

            // 現在のステージで初期状態を設定（スペース入場時なdobbingを有効化する）
            if (stageManager != null)
                HandleStageChanged(stageManager.ActiveStage);
        }

        private void OnEnable()
        {
            // Awake後にOnEnableが呼ばれた場合は即購読，Start前はまだnullなのでガードする
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

            // スカイボックスの回転設定をリセット
            if (hasSavedSkyboxRotation && spaceMaterial != null)
                spaceMaterial.SetFloat("_Rotation", originalSkyboxRotation);
        }

        // ---- イベントハンドラー ----

        /// <summary>
        /// ステージが変わったときに呼ばれる → 入場時アクションを判定する
        /// </summary>
        private void HandleStageChanged(string stageName)
        {
            // Bobbingの判定（入場時1回だけ）
            float bobbingProb = actionConfig != null
                ? actionConfig.GetProbability(stageName, AkabekoAction.Bobbing)
                : (stageName == "space" ? 1f : 0f);

            isBobbing = (Random.value < bobbingProb);

            // スカイボックスのリセット（宇宙以外では視差を止める）
            if (stageName != "space" && hasSavedSkyboxRotation && spaceMaterial != null)
            {
                spaceMaterial.SetFloat("_Rotation", originalSkyboxRotation);
                spaceMaterial = null;
            }

            // スカイボックス視差は宇宙ステージのみ有効
            if (stageName == "space")
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
        /// 首振りごとに呼ばれる → 確率テーブルを元に各アクションを判定して発動
        /// </summary>
        private void HandleShakeTrigger()
        {
            if (stageManager == null) return;
            string stage = stageManager.ActiveStage;

            TryTriggerAction(stage, AkabekoAction.FlyAway, ref isFlyingAway, () => StartCoroutine(FlyAwayCoroutine()));
            TryTriggerAction(stage, AkabekoAction.Spin, ref isSpinning, () => StartCoroutine(SpinCoroutine()));
            TryTriggerAction(stage, AkabekoAction.Shake, ref isShaking, () => StartCoroutine(ShakeCoroutine()));
            TryTriggerAction(stage, AkabekoAction.ScalePulse, ref isScalePulsing, () => StartCoroutine(ScalePulseCoroutine()));
        }

        private void TryTriggerAction(string stage, AkabekoAction action, ref bool isRunning, System.Action trigger)
        {
            if (isRunning) return;
            float prob = actionConfig != null
                ? actionConfig.GetProbability(stage, action)
                : 0f;
            if (Random.value < prob)
            {
                Debug.Log($"[StageActionController] Action '{action}' triggered on stage '{stage}' (prob={prob:P0})");
                trigger?.Invoke();
            }
        }

        // ---- Update ----

        private void Update()
        {
            if (stageManager == null) return;
            bool isSpace = stageManager.ActiveStage == "space";

            // Bobbingウェイトの滑らかな変化
            float targetWeight = isBobbing ? 1f : 0f;
            bobbingWeight = Mathf.MoveTowards(bobbingWeight, targetWeight, Time.deltaTime * 2.0f);

            // クリックで漂い入力（Bobbing有効時のみ）
            if (isBobbing && !isFlyingAway && Input.GetMouseButtonDown(0))
                DetectClick();

            // スカイボックス視差（宇宙ステージ & Bobbing有効時）
            if (isSpace && isBobbing && spaceMaterial != null && !physicsFrozen)
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
                UpdateDriftPhysics();
            }
            else if (bobbingWeight <= 0.01f)
            {
                driftVelocity = Vector3.zero;
                driftAngularVelocity = Vector3.zero;
                currentDriftPosition = Vector3.Lerp(currentDriftPosition, Vector3.zero, Time.deltaTime * 5f);
                currentDriftRotation = Quaternion.Slerp(currentDriftRotation, Quaternion.identity, Time.deltaTime * 5f);
            }

            // ぷかぷか波の計算
            Vector3 bobPos = Vector3.zero;
            Quaternion bobRot = Quaternion.identity;
            if (bobbingWeight > 0.01f)
            {
                float t = Time.time;
                bobPos = new Vector3(
                    Mathf.Sin(t * bobSpeed * 0.7f) * bobRange * 0.5f,
                    Mathf.Sin(t * bobSpeed) * bobRange,
                    Mathf.Cos(t * bobSpeed * 0.5f) * bobRange * 0.3f
                );
                bobRot = Quaternion.Euler(
                    Mathf.Sin(t * rotBobSpeed.x) * rotBobRange.x,
                    Mathf.Sin(t * rotBobSpeed.y) * rotBobRange.y,
                    Mathf.Cos(t * rotBobSpeed.z) * rotBobRange.z
                );
            }

            // Shakeによる位置オフセット（別コルーチンで管理するためここでは無し）
            Vector3 targetPos = initialLocalPosition + Vector3.Lerp(Vector3.zero, bobPos + currentDriftPosition, bobbingWeight);
            Quaternion targetRot = Quaternion.Slerp(initialLocalRotation, initialLocalRotation * bobRot * currentDriftRotation, bobbingWeight);

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        private void UpdateDriftPhysics()
        {
            // 位置の引き戻しバネ
            Vector3 returnForce = (Vector3.zero - currentDriftPosition) * returnSpringStrength;
            driftVelocity += returnForce * Time.deltaTime;
            driftVelocity = Vector3.Lerp(driftVelocity, Vector3.zero, Time.deltaTime * (1f - driftDamping));
            currentDriftPosition += driftVelocity * Time.deltaTime;

            // 回転の引き戻しトルク
            Quaternion deltaRot = Quaternion.identity * Quaternion.Inverse(currentDriftRotation);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude > 0.001f && Mathf.Abs(angle) > 0.01f)
                driftAngularVelocity += axis.normalized * angle * returnSpringStrength * 0.4f * Time.deltaTime;

            driftAngularVelocity = Vector3.Lerp(driftAngularVelocity, Vector3.zero, Time.deltaTime * (1f - rotDamping));
            currentDriftRotation = Quaternion.Euler(driftAngularVelocity * Time.deltaTime) * currentDriftRotation;
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
