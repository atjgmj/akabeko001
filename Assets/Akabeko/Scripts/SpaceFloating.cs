using UnityEngine;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// 宇宙（Space）ステージにおいて、赤べこをぷかぷかと浮遊させ、
    /// クリック（タップ）するとその方向へ漂いながらゆっくり戻る挙動を提供するクラス。
    /// さらに、カメラの動きに連動して背景のスカイボックスを微小回転させ、
    /// 擬似的な3D視差効果（パララックス）を表現します。
    /// 首振り回数に応じてランダムに「カメラ外に飛んでいき、ふわっと帰ってくる」演出を発生させます。
    /// </summary>
    public class SpaceFloating : MonoBehaviour
    {
        private StageManager stageManager;
        private NeckPhysics neckPhysics;
        
        [Header("Floating (Bobbing) Settings")]
        [SerializeField] private float bobSpeed = 1.0f;
        [SerializeField] private float bobRange = 0.15f;
        [SerializeField] private Vector3 rotBobSpeed = new Vector3(0.5f, 0.4f, 0.3f);
        [SerializeField] private Vector3 rotBobRange = new Vector3(4f, 4f, 4f);
        
        [Header("Drifting Settings")]
        [SerializeField] private float clickImpulseForce = 1.8f;
        [SerializeField] private float clickTorqueForce = 35f;
        [SerializeField] private float returnSpringStrength = 1.8f;
        [SerializeField] private float driftDamping = 0.85f;
        [SerializeField] private float rotDamping = 0.90f;

        [Header("Space Skybox Parallax Settings")]
        [SerializeField] private float skyboxRotationSpeed = 0.4f;
        [SerializeField] private float skyboxParallaxMultiplier = 12.0f;

        [Header("Fly-Away Event Settings")]
        [Tooltip("首振り1回ごとに発動する確率 (0〜1)。例: 0.05 = 5%")]
        [SerializeField] private float flyAwayProbabilityPerShake = 0.05f;
        [SerializeField] private float flyAwayExitDuration = 0.7f;  // 画面外へ飛んでいく時間(秒)
        [SerializeField] private float flyAwayReturnDelay = 0.6f;   // 画面外での余韻(秒)
        [SerializeField] private float flyAwayExitMargin = 1.3f;    // カメラ端より外に出るマージン (1.0=ギリギリ端, 1.3=少し外)
        [SerializeField] private float flyAwayTorque = 200f;        // 飛ぶ際の回転トルク

        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        
        private Vector3 currentDriftPosition = Vector3.zero;
        private Vector3 driftVelocity = Vector3.zero;
        
        private Quaternion currentDriftRotation = Quaternion.identity;
        private Vector3 driftAngularVelocity = Vector3.zero;

        private float spaceActiveWeight = 0f;
        private Camera mainCam;
        private Vector3 initialCameraPosition;

        private Material spaceMaterial;
        private float originalRotation = 0f;
        private float skyboxTimeRotation = 0f;
        private bool hasSavedOriginalRotation = false;

        private bool isFlyingAway = false;
        private bool physicsFrozen = false;

        private void Start()
        {
            stageManager = FindFirstObjectByType<StageManager>();
            neckPhysics = GetComponent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = GetComponentInParent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = FindFirstObjectByType<NeckPhysics>();

            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            mainCam = Camera.main;
            
            if (mainCam != null)
            {
                initialCameraPosition = mainCam.transform.position;
            }
        }

        private void OnEnable()
        {
            if (neckPhysics != null)
                neckPhysics.OnNeckShakeCounted += HandleShakeForFlyAway;
        }

        private void OnDisable()
        {
            if (neckPhysics != null)
                neckPhysics.OnNeckShakeCounted -= HandleShakeForFlyAway;

            // シーン終了時などに、マテリアルの回転設定を初期状態に復元
            if (hasSavedOriginalRotation && spaceMaterial != null)
            {
                spaceMaterial.SetFloat("_Rotation", originalRotation);
            }
        }

        /// <summary>
        /// 首振りイベントを受け取り、確率でフライアウト演出を発動する
        /// </summary>
        private void HandleShakeForFlyAway()
        {
            if (stageManager == null || stageManager.ActiveStage != "space") return;
            if (isFlyingAway) return;

            if (Random.value < flyAwayProbabilityPerShake)
            {
                StartCoroutine(FlyAwayCoroutine());
            }
        }

        private void Update()
        {
            if (stageManager == null) return;

            bool isSpace = stageManager.ActiveStage == "space";
            
            spaceActiveWeight = Mathf.MoveTowards(spaceActiveWeight, isSpace ? 1f : 0f, Time.deltaTime * 2.0f);

            if (isSpace && !isFlyingAway)
            {
                // クリックまたはタップ判定
                if (Input.GetMouseButtonDown(0))
                {
                    DetectClick();
                }

                // スカイボックスのマテリアル参照と初期値取得
                if (spaceMaterial == null && RenderSettings.skybox != null)
                {
                    if (RenderSettings.skybox.HasProperty("_Rotation"))
                    {
                        spaceMaterial = RenderSettings.skybox;
                        originalRotation = spaceMaterial.GetFloat("_Rotation");
                        hasSavedOriginalRotation = true;
                    }
                }
            }

            // 宇宙モード時の漂い物理演算（フライアウト中は物理をフリーズ）
            if (spaceActiveWeight > 0.01f && !physicsFrozen)
            {
                // 1. 位置の引き戻しバネ
                Vector3 returnForce = (Vector3.zero - currentDriftPosition) * returnSpringStrength;
                driftVelocity += returnForce * Time.deltaTime;
                driftVelocity = Vector3.Lerp(driftVelocity, Vector3.zero, Time.deltaTime * (1f - driftDamping));
                currentDriftPosition += driftVelocity * Time.deltaTime;

                // 2. 回転の引き戻しトルク
                Quaternion targetRelativeRot = Quaternion.identity;
                Quaternion deltaRot = targetRelativeRot * Quaternion.Inverse(currentDriftRotation);
                deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                
                if (axis.sqrMagnitude > 0.001f && Mathf.Abs(angle) > 0.01f)
                {
                    Vector3 torque = axis.normalized * angle * returnSpringStrength * 0.4f;
                    driftAngularVelocity += torque * Time.deltaTime;
                }
                driftAngularVelocity = Vector3.Lerp(driftAngularVelocity, Vector3.zero, Time.deltaTime * (1f - rotDamping));
                
                Quaternion frameRot = Quaternion.Euler(driftAngularVelocity * Time.deltaTime);
                currentDriftRotation = frameRot * currentDriftRotation;

                // 3. 背景のパララックス & 自転処理
                if (spaceMaterial != null)
                {
                    skyboxTimeRotation += Time.deltaTime * skyboxRotationSpeed;
                    
                    Vector3 camOffset = Vector3.zero;
                    if (mainCam != null)
                    {
                        camOffset = mainCam.transform.position - initialCameraPosition;
                    }

                    float parallaxOffset = (camOffset.x - camOffset.z * 0.5f) * skyboxParallaxMultiplier;
                    float targetRotVal = (originalRotation + skyboxTimeRotation + parallaxOffset) % 360f;
                    spaceMaterial.SetFloat("_Rotation", targetRotVal);
                }
            }
            else if (spaceActiveWeight <= 0.01f)
            {
                driftVelocity = Vector3.zero;
                driftAngularVelocity = Vector3.zero;
                currentDriftPosition = Vector3.Lerp(currentDriftPosition, Vector3.zero, Time.deltaTime * 5f);
                currentDriftRotation = Quaternion.Slerp(currentDriftRotation, Quaternion.identity, Time.deltaTime * 5f);

                if (hasSavedOriginalRotation && spaceMaterial != null)
                {
                    spaceMaterial.SetFloat("_Rotation", originalRotation);
                    spaceMaterial = null;
                }
            }

            // ぷかぷか揺れる波の計算
            Vector3 bobPosition = Vector3.zero;
            Quaternion bobRotation = Quaternion.identity;
            
            if (spaceActiveWeight > 0.01f)
            {
                float t = Time.time;
                bobPosition = new Vector3(
                    Mathf.Sin(t * bobSpeed * 0.7f) * bobRange * 0.5f,
                    Mathf.Sin(t * bobSpeed) * bobRange,
                    Mathf.Cos(t * bobSpeed * 0.5f) * bobRange * 0.3f
                );

                bobRotation = Quaternion.Euler(
                    Mathf.Sin(t * rotBobSpeed.x) * rotBobRange.x,
                    Mathf.Sin(t * rotBobSpeed.y) * rotBobRange.y,
                    Mathf.Cos(t * rotBobSpeed.z) * rotBobRange.z
                );
            }

            // 最終座標と回転をブレンドして適用
            Vector3 targetPos = initialLocalPosition + Vector3.Lerp(Vector3.zero, bobPosition + currentDriftPosition, spaceActiveWeight);
            Quaternion targetRot = Quaternion.Slerp(initialLocalRotation, initialLocalRotation * bobRotation * currentDriftRotation, spaceActiveWeight);

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        /// <summary>
        /// カメラ外に飛んでいき、ふわっと帰ってくるコルーチン
        /// カメラのビューポートを計算し、確実に画面外に出る距離を動く
        /// </summary>
        private IEnumerator FlyAwayCoroutine()
        {
            isFlyingAway = true;
            physicsFrozen = true;

            if (mainCam == null) mainCam = Camera.main;

            // ---- 飛んでいく目標座標の計算 ----
            // ランダムに画面端の方向を選ぶ (左/右/上/下 + 対角線)
            Vector2[] viewportEdges = {
                new Vector2(-0.5f, 0.5f),  // 左
                new Vector2( 1.5f, 0.5f),  // 右
                new Vector2( 0.5f,-0.5f),  // 下
                new Vector2( 0.5f, 1.5f),  // 上
                new Vector2(-0.4f,-0.4f),  // 左下
                new Vector2( 1.4f,-0.4f),  // 右下
                new Vector2(-0.4f, 1.4f),  // 左上
                new Vector2( 1.4f, 1.4f),  // 右上
            };
            Vector2 targetViewport = viewportEdges[Random.Range(0, viewportEdges.Length)];

            // オブジェクトのカメラからの距離を取得
            float distToObj = mainCam != null
                ? Vector3.Distance(mainCam.transform.position, transform.position)
                : 5f;

            // ビューポート座標 → ワールド座標に変換して飛び先を決定
            Vector3 targetWorldPos = transform.position; // フォールバック
            if (mainCam != null)
            {
                Vector3 viewportPoint = new Vector3(targetViewport.x, targetViewport.y, distToObj);
                // マージン分だけさらに外側に伸ばす
                Vector3 screenCenter = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distToObj));
                Vector3 screenEdge = mainCam.ViewportToWorldPoint(viewportPoint);
                targetWorldPos = screenCenter + (screenEdge - screenCenter) * flyAwayExitMargin;
            }

            // ワールド座標をローカルオフセット（currentDriftPosition用）に変換
            Vector3 currentWorldPos = transform.position;
            Vector3 worldDelta = targetWorldPos - currentWorldPos;
            // 親のワールド回転の逆をかけてローカル方向に変換
            Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 targetDriftPos = currentDriftPosition + Quaternion.Inverse(parentRot) * worldDelta;

            // 回転軸はランダム
            Vector3 torqueDir = Random.onUnitSphere;

            Debug.Log($"[SpaceFloating] Fly-away! Target viewport: {targetViewport}, World: {targetWorldPos}");

            // ---- フェーズ1: ヒュッと飛んでいく ----
            float elapsed = 0f;
            Vector3 startDriftPos = currentDriftPosition;
            Quaternion startDriftRot = currentDriftRotation;

            while (elapsed < flyAwayExitDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flyAwayExitDuration;
                // イーズイン（最初ゆっくり、だんだん加速）
                float eased = t * t;

                currentDriftPosition = Vector3.Lerp(startDriftPos, targetDriftPos, eased);
                // クルクル回転
                currentDriftRotation = Quaternion.Euler(torqueDir * flyAwayTorque * Time.deltaTime) * currentDriftRotation;

                yield return null;
            }

            currentDriftPosition = targetDriftPos;

            // ---- フェーズ2: 画面外での余韻 ----
            yield return new WaitForSeconds(flyAwayReturnDelay);

            // ---- フェーズ3: ふわっと帰ってくる ----
            // 物理バネを再開して自然にゆっくり中央に戻す
            physicsFrozen = false;
            driftVelocity = Vector3.zero;
            // 少し回転の慣性を残してクルクルしながら帰る
            driftAngularVelocity = torqueDir * (flyAwayTorque * 0.04f);

            isFlyingAway = false;

            Debug.Log("[SpaceFloating] Return phase started.");
        }

        private void DetectClick()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                {
                    Vector3 clickDir = ray.direction;
                    
                    Vector3 pushImpulse = (clickDir * 0.8f + Random.onUnitSphere * 0.2f).normalized * clickImpulseForce;
                    Vector3 torqueImpulse = Random.onUnitSphere * clickTorqueForce;

                    driftVelocity += pushImpulse;
                    driftAngularVelocity += torqueImpulse;
                    
                    Debug.Log($"[SpaceFloating] Target Clicked. Applying drift impulse: {pushImpulse}");
                }
            }
        }
    }
}
