using UnityEngine;
using System.IO;

namespace Akabeko
{
    /// <summary>
    /// シェア機能を管理
    /// </summary>
    public class ShareManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string shareText = "赤べこで遊んでるよ！ #赤べこアプリ";
        [SerializeField] private string appUrl = "https://example.com/akabeko"; // アプリのURL

        private ScreenshotManager screenshotManager;

        private void Awake()
        {
            screenshotManager = FindFirstObjectByType<ScreenshotManager>();
        }

        /// <summary>
        /// シェア実行
        /// </summary>
        public void Share()
        {
            // スクリーンショットがあればそれを使用
            if (screenshotManager != null)
            {
                // まだ保存されていない、またはファイルが存在しない場合は新規撮影
                string existingPath = screenshotManager.GetLastScreenshotPath();
                
                if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
                {
                    ShareNative(shareText, existingPath);
                }
                else
                {
                    // 非同期で撮影してシェア
                    screenshotManager.CaptureScreenshotAsTextureAsync((texture) =>
                    {
                        string newPath = SaveTemporaryImage(texture);
                        ShareNative(shareText, newPath);
                        
                        // テクスチャのメモリ解放（不要なら、またはGC任せ）
                        // Destroy(texture); 
                    });
                }
            }
            else
            {
                // スクリーンショットマネージャーがない場合（通常ありえないが）
                ShareNative(shareText, null);
            }
        }

        /// <summary>
        /// ネイティブシェア
        /// </summary>
        private void ShareNative(string text, string imagePath)
        {
#if UNITY_ANDROID
            ShareAndroid(text, imagePath);
#elif UNITY_IOS
            ShareIOS(text, imagePath);
#else
            ShareWebGL(text, imagePath);
#endif
        }

        private void ShareAndroid(string text, string imagePath)
        {
            // Android用のシェア処理
            // NativeShareプラグインなどを使用する想定
            Debug.Log($"Sharing on Android: {text}");
            
            // 実装例（NativeShareプラグイン使用時）:
            // new NativeShare()
            //     .SetText(text)
            //     .SetUrl(appUrl)
            //     .AddFile(imagePath)
            //     .Share();
        }

        private void ShareIOS(string text, string imagePath)
        {
            // iOS用のシェア処理
            Debug.Log($"Sharing on iOS: {text}");
            
            // 実装例（NativeShareプラグイン使用時）:
            // new NativeShare()
            //     .SetText(text)
            //     .SetUrl(appUrl)
            //     .AddFile(imagePath)
            //     .Share();
        }

        private void ShareWebGL(string text, string imagePath)
        {
            // WebGL用（Twitter共有リンクなど）
            string tweetText = UnityEngine.Networking.UnityWebRequest.EscapeURL(text + " " + appUrl);
            string twitterUrl = $"https://twitter.com/intent/tweet?text={tweetText}";
            
            Application.OpenURL(twitterUrl);
            Debug.Log($"Opening Twitter share: {twitterUrl}");
        }

        /// <summary>
        /// 一時的に画像を保存
        /// </summary>
        private string SaveTemporaryImage(Texture2D texture)
        {
            byte[] bytes = texture.EncodeToPNG();
            string path = Path.Combine(Application.temporaryCachePath, "share_temp.png");
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
