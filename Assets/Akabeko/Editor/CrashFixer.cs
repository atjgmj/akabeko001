using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Akabeko.Editor
{
    /// <summary>
    /// クラッシュ対策：グラフィックスAPIを安定版（DirectX 11）に強制設定するスクリプト
    /// </summary>
    [InitializeOnLoad]
    public class CrashFixer
    {
        static CrashFixer()
        {
            var target = BuildTarget.StandaloneWindows64;
            
            // 現在の設定を取得
            var currentAPIs = PlayerSettings.GetGraphicsAPIs(target);

            // 既にDX11が先頭なら何もしない
            if (currentAPIs.Length > 0 && currentAPIs[0] == GraphicsDeviceType.Direct3D11)
            {
                return;
            }

            // 強制的にDX11のみに設定
            Debug.Log("【CrashFixer】安定化のため、Graphics API を Direct3D11 に変更します...");
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
            Debug.Log("【CrashFixer】変更完了: Direct3D11 が設定されました。");
        }
    }
}
