using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// 線画（Scanline Halftone）エフェクトをカメラ全域に適用するポストプロセスコンポーネント
    /// 赤べこの動き（MotionShift）に合わせて線の歪みや揺らぎを動的変更する
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class ScanlinePostProcess : MonoBehaviour
    {
        [Header("Settings")]
        public bool isEffectActive = false;
        [Range(0f, 5f)] public float motionAmount = 0.0f;

        [SerializeField] private Shader scanlineShader;
        private Material scanlineMaterial;

        private void OnEnable()
        {
            if (scanlineShader == null)
            {
                scanlineShader = Shader.Find("Akabeko/ScanlineHalftone");
            }
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (scanlineMaterial == null && scanlineShader != null)
            {
                scanlineMaterial = new Material(scanlineShader);
                scanlineMaterial.hideFlags = HideFlags.DontSave;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (isEffectActive)
            {
                EnsureMaterial();
                if (scanlineMaterial != null)
                {
                    scanlineMaterial.SetFloat("_MotionShift", motionAmount);
                    Graphics.Blit(source, destination, scanlineMaterial);
                    return;
                }
            }

            Graphics.Blit(source, destination);
        }

        private void OnDisable()
        {
            if (scanlineMaterial != null)
            {
                DestroyImmediate(scanlineMaterial);
            }
        }
    }
}
