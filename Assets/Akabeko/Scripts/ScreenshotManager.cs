using UnityEngine;
using System;
using System.IO;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// スクリーンショット機能を管理
    /// </summary>
    public class ScreenshotManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string screenshotPrefix = "Akabeko_";
        [SerializeField] private int superSize = 1; // 解像度倍率（1 = 通常、2 = 2倍）

        private UIManager uiManager;
        private string lastScreenshotPath;

        private void Awake()
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }

        /// <summary>
        /// スクリーンショットを撮影
        /// </summary>
        public void TakeScreenshot()
        {
            StartCoroutine(TakeScreenshotCoroutine());
        }

        private IEnumerator TakeScreenshotCoroutine()
        {
            // 広告を一時非表示
            if (uiManager != null)
            {
                uiManager.SetAdBannerVisible(false);
            }

            // 1フレーム待機（UIが更新されるまで）
            yield return new WaitForEndOfFrame();

            // ファイル名生成
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"{screenshotPrefix}{timestamp}.png";

            // 保存先パス
#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, "..", "Screenshots", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
#elif UNITY_ANDROID || UNITY_IOS
            string path = Path.Combine(Application.persistentDataPath, filename);
#else
            string path = Path.Combine(Application.persistentDataPath, filename);
#endif

            // スクリーンショット撮影
            ScreenCapture.CaptureScreenshot(path, superSize);
            lastScreenshotPath = path;

            Debug.Log($"Screenshot saved: {path}");

            // 広告を再表示
            yield return new WaitForSeconds(0.1f);
            if (uiManager != null)
            {
                uiManager.SetAdBannerVisible(true);
            }

            // フィードバック（将来実装）
            // ShowScreenshotFeedback();
        }

        /// <summary>
        /// 最後に撮影したスクリーンショットのパスを取得
        /// </summary>
        public string GetLastScreenshotPath()
        {
            return lastScreenshotPath;
        }

        /// <summary>
        /// スクリーンショットを非同期でTexture2Dとして取得
        /// </summary>
        /// <param name="callback">取得したテクスチャを受け取るコールバック</param>
        public void CaptureScreenshotAsTextureAsync(Action<Texture2D> callback)
        {
            StartCoroutine(CaptureScreenshotAsTextureCoroutine(callback));
        }

        private IEnumerator CaptureScreenshotAsTextureCoroutine(Action<Texture2D> callback)
        {
            yield return new WaitForEndOfFrame();

            int width = Screen.width;
            int height = Screen.height;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            
            // 画面のピクセルを読み取る
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            callback?.Invoke(screenshot);
        }
    }
}
