using UnityEngine;
using System;

namespace Akabeko
{
    /// <summary>
    /// スワイプ入力を検出し、パターンを分析するクラス
    /// </summary>
    public class SwipeDetector : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float minSwipeDistance = 20f;
        [SerializeField] private float maxSwipeTime = 1f;
        [SerializeField] private LayerMask neckLayer;

        // スワイプ情報
        private Vector2 startPos;
        private Vector2 endPos;
        private float startTime;
        private float endTime;
        private bool isSwiping = false;

        // イベント
        public event Action<SwipeData> OnSwipeDetected;
        public event Action<Vector3> OnNeckTapped;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            DetectInput();
        }

        private void DetectInput()
        {
            // タッチ入力（モバイル）
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                HandleInput(touch.position, touch.phase == TouchPhase.Began, touch.phase == TouchPhase.Ended);
            }
            // マウス入力（PC/WebGL）
            else
            {
                HandleInput(Input.mousePosition, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
            }
        }

        private void HandleInput(Vector2 position, bool isDown, bool isUp)
        {
            if (isDown)
            {
                StartSwipe(position);
            }
            else if (isUp && isSwiping)
            {
                EndSwipe(position);
            }
        }

        private void StartSwipe(Vector2 position)
        {
            // Raycastで首の部位をチェック
            Ray ray = mainCamera.ScreenPointToRay(position);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, neckLayer))
            {
                startPos = position;
                startTime = Time.time;
                isSwiping = true;

                // 首がタップされたことを通知
                OnNeckTapped?.Invoke(hit.point);
            }
        }

        private void EndSwipe(Vector2 position)
        {
            endPos = position;
            endTime = Time.time;

            float distance = Vector2.Distance(startPos, endPos);
            float duration = endTime - startTime;

            // スワイプとして認識
            if (distance >= minSwipeDistance && duration <= maxSwipeTime)
            {
                SwipeData swipeData = AnalyzeSwipe();
                OnSwipeDetected?.Invoke(swipeData);
            }

            isSwiping = false;
        }

        private SwipeData AnalyzeSwipe()
        {
            Vector2 direction = (endPos - startPos).normalized;
            float distance = Vector2.Distance(startPos, endPos);
            float duration = endTime - startTime;
            float speed = distance / duration;

            SwipeData data = new SwipeData
            {
                direction = direction,
                distance = distance,
                duration = duration,
                speed = speed,
                startPosition = startPos,
                endPosition = endPos
            };

            return data;
        }

        /// <summary>
        /// 特定部位（耳、鼻など）がタップされたかを判定
        /// </summary>
        public string GetTappedPart(Vector3 hitPoint, Transform neckTransform)
        {
            // ローカル座標に変換
            Vector3 localPoint = neckTransform.InverseTransformPoint(hitPoint);

            // 簡易的な部位判定（実際のモデルに合わせて調整が必要）
            if (localPoint.y > 0.8f)
            {
                if (Mathf.Abs(localPoint.x) > 0.3f)
                    return "ear"; // 耳
                else
                    return "head"; // 頭
            }
            else if (localPoint.y > 0.5f && localPoint.z > 0.2f)
            {
                return "nose"; // 鼻
            }

            return "neck"; // その他の首部分
        }
    }

    /// <summary>
    /// スワイプデータ構造
    /// </summary>
    [Serializable]
    public struct SwipeData
    {
        public Vector2 direction;
        public float distance;
        public float duration;
        public float speed;
        public Vector2 startPosition;
        public Vector2 endPosition;

        public SwipePattern GetPattern()
        {
            // パターン判定ロジック（将来拡張）
            if (speed > 1000f)
                return SwipePattern.Rapid;
            else if (duration > 0.5f)
                return SwipePattern.Hold;
            else
                return SwipePattern.Normal;
        }
    }

    public enum SwipePattern
    {
        Normal,
        Rapid,      // 高速スワイプ
        Hold,       // 長押し
        Circle      // 円形（将来実装）
    }
}
