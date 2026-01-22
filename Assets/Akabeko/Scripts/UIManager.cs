using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Akabeko
{
    /// <summary>
    /// UI全体を管理するクラス
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button screenshotButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private GameObject adBanner;
        [SerializeField] private GameObject rareMotionPanel;
        [SerializeField] private TextMeshProUGUI rareMotionText;
        [SerializeField] private ParticleSystem rareEffectParticle;

        [Header("Settings")]
        [SerializeField] private float rareEffectDuration = 3f;

        private ScreenshotManager screenshotManager;
        private ShareManager shareManager;

        private void Awake()
        {
            screenshotManager = FindFirstObjectByType<ScreenshotManager>();
            shareManager = FindFirstObjectByType<ShareManager>();
        }

        private void Start()
        {
            SetupButtons();
            HideRareMotionPanel();
        }

        private void SetupButtons()
        {
            if (screenshotButton != null)
            {
                screenshotButton.onClick.AddListener(OnScreenshotButtonClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.AddListener(OnShareButtonClicked);
            }
        }

        private void OnScreenshotButtonClicked()
        {
            if (screenshotManager != null)
            {
                screenshotManager.TakeScreenshot();
            }
        }

        private void OnShareButtonClicked()
        {
            if (shareManager != null)
            {
                shareManager.Share();
            }
        }

        /// <summary>
        /// レアモーション演出を表示
        /// </summary>
        public void ShowRareMotionEffect(RareMotionData motion)
        {
            StartCoroutine(RareMotionEffectCoroutine(motion));
        }

        private IEnumerator RareMotionEffectCoroutine(RareMotionData motion)
        {
            // パネル表示
            if (rareMotionPanel != null)
            {
                rareMotionPanel.SetActive(true);
            }

            // テキスト設定
            if (rareMotionText != null)
            {
                string rarityStars = GetRarityStars(motion.rarity);
                rareMotionText.text = $"{rarityStars}\n{motion.motionName}";
            }

            // パーティクル再生
            if (rareEffectParticle != null)
            {
                rareEffectParticle.Play();
            }

            // シェアボタンをハイライト
            HighlightShareButton(true);

            // 効果音再生（将来実装）
            // PlayRareSound(motion.rarity);

            // 一定時間待機
            yield return new WaitForSeconds(rareEffectDuration);

            // 演出終了
            HideRareMotionPanel();
            HighlightShareButton(false);
        }

        private void HideRareMotionPanel()
        {
            if (rareMotionPanel != null)
            {
                rareMotionPanel.SetActive(false);
            }
        }

        private void HighlightShareButton(bool highlight)
        {
            if (shareButton != null)
            {
                // ボタンのスケールやカラーを変更してハイライト
                shareButton.transform.localScale = highlight ? Vector3.one * 1.2f : Vector3.one;
            }
        }

        private string GetRarityStars(RarityLevel rarity)
        {
            switch (rarity)
            {
                case RarityLevel.COMMON: return "★";
                case RarityLevel.RARE: return "★★";
                case RarityLevel.SUPER_RARE: return "★★★";
                case RarityLevel.ULTRA_RARE: return "★★★★";
                default: return "★";
            }
        }

        /// <summary>
        /// 広告バナーの表示/非表示
        /// </summary>
        public void SetAdBannerVisible(bool visible)
        {
            if (adBanner != null)
            {
                adBanner.SetActive(visible);
            }
        }
    }
}
