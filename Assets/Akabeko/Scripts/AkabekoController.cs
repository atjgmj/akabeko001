using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// 赤べこ全体の動作を統合制御するメインコントローラー (Version 2.0)
    /// </summary>
    [RequireComponent(typeof(SwipeDetector))]
    [RequireComponent(typeof(NeckPhysics))]
    public class AkabekoController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform neckTransform;
        [SerializeField] private Renderer akabekoRenderer;

        private SwipeDetector swipeDetector;
        private NeckPhysics neckPhysics;
        private RareMotionSystem rareMotionSystem;
        private DataManager dataManager;
        private DynamicUIOverlay uiOverlay;

        private int swipeCount = 0;

        private void Awake()
        {
            swipeDetector = GetComponent<SwipeDetector>();
            
            // 確実にNeckPhysicsを見つける
            neckPhysics = GetComponent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = GetComponentInChildren<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = GetComponentInParent<NeckPhysics>();
            if (neckPhysics == null) neckPhysics = FindFirstObjectByType<NeckPhysics>();

            rareMotionSystem = GetComponent<RareMotionSystem>();
            dataManager = FindFirstObjectByType<DataManager>();

            // UIの自動アタッチ
            uiOverlay = FindFirstObjectByType<DynamicUIOverlay>();
            if (uiOverlay == null)
            {
                uiOverlay = gameObject.AddComponent<DynamicUIOverlay>();
            }

            // StageActionController (ステージアクション統合制御) の自動アタッチ
            if (GetComponent<StageActionController>() == null)
            {
                gameObject.AddComponent<StageActionController>();
            }

            // neckTransformの自動補完
            if (neckTransform == null)
            {
                neckTransform = FindNeckRecursively(transform);
            }

            Debug.Log($"[AkabekoController V2.0] Initialized. NeckPhysics: {neckPhysics != null}, NeckTransform: {neckTransform != null}");
        }

        private Transform FindNeckRecursively(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name == "Neck" || child.name == "neck") return child;
                Transform found = FindNeckRecursively(child);
                if (found != null) return found;
            }
            return null;
        }

        private void OnEnable()
        {
            if (swipeDetector != null)
            {
                swipeDetector.OnSwipeDetected += HandleSwipe;
                swipeDetector.OnNeckTapped += HandleNeckTap;
            }

            if (neckPhysics != null)
            {
                neckPhysics.OnNeckShakeCounted += HandleNeckShake;
            }
        }

        private void OnDisable()
        {
            if (swipeDetector != null)
            {
                swipeDetector.OnSwipeDetected -= HandleSwipe;
                swipeDetector.OnNeckTapped -= HandleNeckTap;
            }

            if (neckPhysics != null)
            {
                neckPhysics.OnNeckShakeCounted -= HandleNeckShake;
            }
        }

        private void Start()
        {
            if (dataManager != null)
            {
                swipeCount = dataManager.GetSwipeCount();
            }
            if (uiOverlay != null)
            {
                uiOverlay.SetSwipeCount(swipeCount);
            }
        }

        private void HandleSwipe(SwipeData swipeData)
        {
            // スワイプ入力時そのものではなく、実際の物理的な首振り動作（HandleNeckShake）でカウントします。
            if (neckPhysics != null)
            {
                neckPhysics.ApplySwipeForce(swipeData);
            }

            if (rareMotionSystem != null)
            {
                rareMotionSystem.CheckRareMotion(swipeData, swipeCount);
            }

            Debug.Log($"[V2.0] Swipe Detected! Speed: {swipeData.speed:F0}");
        }

        private void HandleNeckShake()
        {
            swipeCount++;
            if (dataManager != null)
            {
                dataManager.IncrementSwipeCount();
            }
            if (uiOverlay != null)
            {
                uiOverlay.SetSwipeCount(swipeCount);
            }
            Debug.Log($"[AkabekoController] Physical Neck Shake counted. Total count: {swipeCount}");
        }

        private void HandleNeckTap(Vector3 hitPoint)
        {
            if (neckTransform == null) 
            {
                Debug.LogError("[V2.0] Neck Transform is missing in Inspector!");
                return;
            }

            string part = swipeDetector.GetTappedPart(hitPoint, neckTransform);
            Debug.Log($"[V2.0] Tap Detected at: {part}");

            if (neckPhysics != null)
            {
                // タップ時の動きをより自然にするため、上方向（Y）と横方向（X）に力を与える
                Vector3 impulse = new Vector3(300f, 300f, 0f); 
                Debug.Log($"[V2.0] Applying Tap Impulse to {neckPhysics.gameObject.name}: {impulse}");
                neckPhysics.ApplyImpulse(impulse);
            }
        }

        [ContextMenu("Debug Push All Axes")]
        public void DebugPushAll()
        {
            if (neckPhysics != null)
            {
                neckPhysics.ApplyImpulse(new Vector3(10000f, 10000f, 10000f));
                Debug.Log("[V2.0] Manual Push Applied");
            }
        }

        public void ChangeMaterial(Material newMaterial)
        {
            if (akabekoRenderer != null) akabekoRenderer.material = newMaterial;
        }

        public void ResetMaterial()
        {
            // Implementation pending
        }
    }
}
