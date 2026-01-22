using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// カメラの固定位置・角度を設定
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Transform targetObject; // 赤べこのTransform
        [SerializeField] private bool useInitialTransform = true; // 開始時の位置・角度を完全に維持する
        [SerializeField] private Vector3 offset = new Vector3(2.5f, 2.0f, -2.5f);
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 0.5f, 0); // 注視点のオフセット（中央に寄せる用）

        [Header("Background Settings")]
        [SerializeField] private bool overrideBackground = false; // デフォルトでは上書きしない
        [SerializeField] private CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        [SerializeField] private Color backgroundColor = new Color(0.95f, 0.95f, 0.92f); // 柔らかなオフホワイト
        
        [Header("Distance & Shadow")]
        [SerializeField] private GameObject shadowPrefab; // 足元の影のプレハブ（オプション）
        [SerializeField] private float shadowOpacity = 0.2f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            SetupCamera();
        }

        /// <summary>
        /// カメラの初期設定
        /// </summary>
        private void SetupCamera()
        {
            if (targetObject == null)
            {
                // "Akabeko" という名前のオブジェクトを自動検索
                GameObject akabeko = GameObject.Find("Akabeko");
                if (akabeko != null)
                {
                    targetObject = akabeko.transform;
                    Debug.Log("[CameraController] Target object automatically assigned: Akabeko");
                }
            }

            if (targetObject == null)
            {
                Debug.LogWarning("[CameraController] Target object is missing! Camera will not move.");
                return;
            }

            if (useInitialTransform)
            {
                // エディタ上での位置関係をオフセットとして記録
                offset = transform.position - targetObject.position;
                Debug.Log($"[CameraController] Captured initial transform. Offset: {offset}");
            }
            else
            {
                // 手動設定のオフセットを適用
                transform.position = targetObject.position + offset;
                // カメラをターゲットに向ける
                transform.LookAt(targetObject.position + lookAtOffset);
            }

            // 背景設定の上書き
            if (overrideBackground && cam != null)
            {
                cam.clearFlags = clearFlags;
                cam.backgroundColor = backgroundColor;
                Debug.Log($"[CameraController] Background modified: {clearFlags}, {backgroundColor}");
            }
            
            Debug.Log($"Camera setup complete. Position: {transform.position}, FOV: {cam.fieldOfView}");
        }

        private void LateUpdate()
        {
            // ターゲットが動く可能性があるため、毎フレーム位置を追従（角度は維持）
            if (targetObject != null && useInitialTransform)
            {
                transform.position = targetObject.position + offset;
            }
        }

        /// <summary>
        /// エディタでの視覚化
        /// </summary>
        private void OnDrawGizmos()
        {
            if (targetObject == null) return;

            // カメラの視線を可視化
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetObject.position);
            Gizmos.DrawWireSphere(targetObject.position, 0.2f);
        }
    }
}
