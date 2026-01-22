using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// 赤べこの首の物理演算 (Version 2.0)
    /// </summary>
    public class NeckPhysics : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform neckTransform;

        [Header("Physics Parameters")]
        public float springStrength = 100f;
        public float damping = 2f;
        public float mass = 0.5f;
        public float swipeMultiplier = 2.0f;

        [Header("Constraints")]
        public float maxAngleX = 45f;
        public float maxAngleY = 45f;
        public float maxAngleZ = 30f;

        [Header("Debug Status")]
        [SerializeField] private Vector3 currentRotation;
        [SerializeField] private Vector3 angularVelocity;

        private Vector3 targetRotation = Vector3.zero;
        private Quaternion initialRotation;
        private bool hasInitialized = false;

        private void Start()
        {
            if (neckTransform == null)
            {
                // 自動的に "Neck" という名前のオブジェクトを探す（階層を考慮）
                neckTransform = FindNeckRecursively(transform);
                
                if (neckTransform == null)
                {
                    Debug.LogError("[V2.0] Neck Transform is not assigned and could not be found in children! Please assign it in the Inspector.");
                    return;
                }
                Debug.Log($"[V2.0] Neck Transform automatically found: {neckTransform.name}");
            }

            initialRotation = neckTransform.localRotation;
            currentRotation = Vector3.zero;
            hasInitialized = true;
            Debug.Log($"[V2.0] NeckPhysics Initialized. Rotation: {initialRotation.eulerAngles}");

            // Animatorチェック
            var anim = GetComponentInParent<Animator>();
            if (anim != null && anim.enabled)
            {
                Debug.LogWarning("[V2.0] WARNING: Animator found! It might be blocking physics. Try disabling/removing it.");
            }
        }

        private void FixedUpdate()
        {
            if (!hasInitialized) return;

            // Spring physics
            Vector3 springForce = -springStrength * (currentRotation - targetRotation);
            Vector3 dampingForce = -damping * angularVelocity;
            Vector3 totalForce = springForce + dampingForce;
            Vector3 accel = totalForce / mass;

            angularVelocity += accel * Time.fixedDeltaTime;
            currentRotation += angularVelocity * Time.fixedDeltaTime;

            // Clamp
            currentRotation.x = Mathf.Clamp(currentRotation.x, -maxAngleX, maxAngleX);
            currentRotation.y = Mathf.Clamp(currentRotation.y, -maxAngleY, maxAngleY);
            currentRotation.z = Mathf.Clamp(currentRotation.z, -maxAngleZ, maxAngleZ);
        }

        private void LateUpdate()
        {
            if (!hasInitialized || neckTransform == null) return;

            // Animatorを上書きするためにLateUpdateで回転を適用
            Quaternion rot = Quaternion.Euler(currentRotation);
            neckTransform.localRotation = initialRotation * rot;

            // 回転が閾値を超えた場合にログを出す（インスペクターでの確認用）
            if (currentRotation.magnitude > 1.0f)
            {
                Debug.Log($"[V2.0] Neck Rotating: {currentRotation}");
            }
        }

        public void ApplySwipeForce(SwipeData data)
        {
            // 感度を 0.01f から 0.5f へ大幅に引き上げ
            Vector3 force = new Vector3(-data.direction.y, data.direction.x, 0) * data.speed * 0.5f;
            ApplyImpulse(force * swipeMultiplier);
        }

        public void ApplyImpulse(Vector3 force)
        {
            Debug.Log($"[V2.0] Impulse Received: {force}");
            angularVelocity += force;
        }

        [ContextMenu("Reset Physics Values")]
        public void ResetValues()
        {
            springStrength = 100f;
            damping = 2f;
            mass = 0.5f;
            swipeMultiplier = 2.0f;
            Debug.Log("[V2.0] Physics values reset to defaults.");
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
    }
}
