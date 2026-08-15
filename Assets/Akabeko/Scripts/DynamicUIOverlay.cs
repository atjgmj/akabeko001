using UnityEngine;
using System.Collections.Generic;

namespace Akabeko
{
    /// <summary>
    /// Clean card-style UI + Admin Config Panel matching Wireframe Images 1 & 2
    /// </summary>
    public class DynamicUIOverlay : MonoBehaviour
    {
        private RareMotionSystem rareMotionSystem;
        private StageManager stageManager;
        private StageActionConfig actionConfig;
        private StageActionController stageActionController;
        private ScreenshotManager screenshotManager;
        private ShareManager shareManager;

        private static DynamicUIOverlay instance;
        public static DynamicUIOverlay Instance => instance;

        private int swipeCount = 0;
        private string notificationMessage = "";
        private float notificationTimer = 0f;

        // Modern Sleek Rare Alert Pill
        private string rareAlertCategory = "";
        private string rareAlertName = "";
        private float rareAlertTimer = 0f;
        private const float RARE_ALERT_DURATION = 3.5f;
        private Color rareAlertColor = new Color(0.85f, 0.85f, 0.95f);
        private GUIStyle rareAlertStyle;

        private string activeColor = "Normal";
        private string selectedViewStage = "Default"; // 画像2のように選択中のステージに応じたActionテーブル切り替え
        private bool showControls = false;

        private GUIStyle titleStyle;
        private GUIStyle countStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle headerBoxStyle;
        private GUIStyle itemBoxStyle;
        private GUIStyle activeItemBoxStyle;
        private GUIStyle percentStyle;
        private GUIStyle saveButtonStyle;
        private GUIStyle popupStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            instance = this;
            rareMotionSystem = FindFirstObjectByType<RareMotionSystem>();
            stageManager = FindFirstObjectByType<StageManager>();
            actionConfig = FindFirstObjectByType<StageActionConfig>();
            stageActionController = FindFirstObjectByType<StageActionController>();
            screenshotManager = FindFirstObjectByType<ScreenshotManager>();
            shareManager = FindFirstObjectByType<ShareManager>();
        }

        private void Start()
        {
            if (actionConfig == null) actionConfig = StageActionConfig.Instance ?? FindFirstObjectByType<StageActionConfig>();
            if (stageActionController == null) stageActionController = FindFirstObjectByType<StageActionController>();
        }

        private void OnEnable()
        {
            if (rareMotionSystem != null)
                rareMotionSystem.OnRareMotionTriggered += HandleRareMotionTriggered;
        }

        private void OnDisable()
        {
            if (rareMotionSystem != null)
                rareMotionSystem.OnRareMotionTriggered -= HandleRareMotionTriggered;
        }

        public void SetSwipeCount(int count) { swipeCount = count; }

        private void HandleRareMotionTriggered(RareMotionData motion)
        {
            ShowRareAlert("Motion", motion.motionName);
        }

        public static void ShowRareAlert(string category, string name)
        {
            if (instance != null)
            {
                instance.TriggerRareAlert(category, name);
            }
        }

        public void TriggerRareAlert(string category, string name)
        {
            if (string.IsNullOrEmpty(name) || name.Equals("Default", System.StringComparison.OrdinalIgnoreCase) || name.Equals("Normal", System.StringComparison.OrdinalIgnoreCase) || name.Equals("None", System.StringComparison.OrdinalIgnoreCase))
                return;

            rareAlertCategory = category;
            rareAlertName = name;
            rareAlertTimer = RARE_ALERT_DURATION;
            rareAlertColor = GetAccentColor(category, name);
        }

        private Color GetAccentColor(string category, string name)
        {
            string lower = name.ToLower();
            if (category.Equals("Stage", System.StringComparison.OrdinalIgnoreCase))
            {
                if (lower.Contains("space")) return new Color(0.68f, 0.38f, 0.98f);   // Purple
                if (lower.Contains("sea")) return new Color(0.15f, 0.75f, 0.95f);     // Aqua Cyan
                if (lower.Contains("volcano")) return new Color(1.00f, 0.40f, 0.10f); // Solar Orange
                if (lower.Contains("monoline")) return new Color(0.92f, 0.92f, 0.92f); // Silver
            }
            else if (category.Equals("Color", System.StringComparison.OrdinalIgnoreCase))
            {
                if (lower.Contains("gold")) return new Color(1.0f, 0.82f, 0.18f);     // Gold
                if (lower.Contains("silver")) return new Color(0.88f, 0.90f, 0.95f);   // Silver
                if (lower.Contains("rainbow")) return new Color(0.95f, 0.30f, 0.78f);  // Magenta
            }
            else if (category.Equals("Action", System.StringComparison.OrdinalIgnoreCase))
            {
                if (lower.Contains("supernova")) return new Color(1.0f, 0.70f, 0.15f);
                if (lower.Contains("wormhole")) return new Color(0.50f, 0.45f, 1.00f);
                if (lower.Contains("clone")) return new Color(0.20f, 0.85f, 0.50f);
                if (lower.Contains("matrix")) return new Color(0.18f, 0.95f, 0.40f);
                if (lower.Contains("disco")) return new Color(1.00f, 0.28f, 0.62f);
                if (lower.Contains("tornado")) return new Color(0.28f, 0.72f, 1.00f);
            }
            return new Color(0.82f, 0.10f, 0.10f); // Akabeko Red
        }

        public void ShowNotification(string msg)
        {
            notificationMessage = msg;
            notificationTimer = 2.5f;
        }

        private void Update()
        {
            if (notificationTimer > 0) notificationTimer -= Time.deltaTime;
            if (rareAlertTimer > 0) rareAlertTimer -= Time.deltaTime;
        }

        private Texture2D MakeTex(int w, int h, Color c)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = c;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        private Texture2D MakeRounded(int w, int h, float radius, Color fill, Color border, float bw = 1.5f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dist = 0f;
                    bool corner = false;
                    if (x < radius && y < radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)); corner = true; }
                    else if (x >= w - radius && y < radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(w - radius, radius)); corner = true; }
                    else if (x < radius && y >= h - radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, h - radius)); corner = true; }
                    else if (x >= w - radius && y >= h - radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(w - radius, h - radius)); corner = true; }

                    if (corner && dist > radius) cols[y * w + x] = Color.clear;
                    else if (corner && dist > radius - bw) cols[y * w + x] = border;
                    else if (!corner && (x < bw || x >= w - bw || y < bw || y >= h - bw)) cols[y * w + x] = border;
                    else cols[y * w + x] = fill;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            Color red        = new Color(0.82f, 0.10f, 0.10f, 1f);
            Color tealBg     = new Color(0.10f, 0.35f, 0.48f, 1.0f); // 画像通りのTeal色
            Color darkGray   = new Color(0.20f, 0.20f, 0.20f, 1f);
            Color highlight  = new Color(0.85f, 0.15f, 0.15f, 1f); // 赤枠ハイライト

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            titleStyle.normal.textColor = red;

            countStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            countStyle.normal.textColor = red;

            menuButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            menuButtonStyle.normal.background  = MakeTex(2, 2, Color.clear);
            menuButtonStyle.hover.background   = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.08f));
            menuButtonStyle.active.background  = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.15f));
            menuButtonStyle.normal.textColor   = darkGray;

            headerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            headerBoxStyle.normal.background = MakeTex(2, 2, tealBg);
            headerBoxStyle.normal.textColor = Color.white;

            itemBoxStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            itemBoxStyle.normal.background = MakeTex(2, 2, tealBg);
            itemBoxStyle.hover.background  = MakeTex(2, 2, new Color(0.15f, 0.45f, 0.60f));
            itemBoxStyle.normal.textColor = Color.white;

            activeItemBoxStyle = new GUIStyle(itemBoxStyle);
            activeItemBoxStyle.normal.background = MakeRounded(100, 36, 4f, tealBg, highlight, 2.5f);
            activeItemBoxStyle.normal.textColor = Color.white;

            percentStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
            };
            percentStyle.normal.textColor = Color.black;

            saveButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            saveButtonStyle.normal.background = MakeTex(2, 2, tealBg);
            saveButtonStyle.hover.background  = MakeTex(2, 2, new Color(0.15f, 0.45f, 0.60f));
            saveButtonStyle.normal.textColor = Color.white;

            popupStyle = new GUIStyle(GUI.skin.box)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            popupStyle.normal.background = MakeRounded(200, 44, 10f, new Color(0.12f, 0.12f, 0.12f, 0.90f), red, 1.5f);
            popupStyle.normal.textColor  = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            labelStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);

            rareAlertStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            rareAlertStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float scale = Mathf.Clamp(sw / 1280f, 0.6f, 1.4f);

            titleStyle.fontSize         = Mathf.RoundToInt(44 * scale);
            countStyle.fontSize         = Mathf.RoundToInt(36 * scale);
            menuButtonStyle.fontSize    = Mathf.RoundToInt(22 * scale);
            headerBoxStyle.fontSize     = Mathf.RoundToInt(18 * scale);
            itemBoxStyle.fontSize       = Mathf.RoundToInt(16 * scale);
            activeItemBoxStyle.fontSize = Mathf.RoundToInt(16 * scale);
            percentStyle.fontSize       = Mathf.RoundToInt(14 * scale);
            saveButtonStyle.fontSize    = Mathf.RoundToInt(18 * scale);
            popupStyle.fontSize         = Mathf.RoundToInt(16 * scale);
            rareAlertStyle.fontSize     = Mathf.RoundToInt(13 * scale);

            // --- 0. Top Rare Alert Pill (Modern Game HUD Style e.g. Stage : Space) ---
            if (rareAlertTimer > 0f)
            {
                float elapsed = RARE_ALERT_DURATION - rareAlertTimer;
                float inProgress = Mathf.Clamp01(elapsed / 0.35f);
                float easeIn = 1f - Mathf.Pow(1f - inProgress, 3f);
                float alpha = Mathf.Clamp01(rareAlertTimer / 0.5f);

                float targetY = 12f * scale;
                float startY = -40f * scale;
                float curAlertY = Mathf.Lerp(startY, targetY, easeIn);

                string alertText = $"✦ {rareAlertCategory} : {rareAlertName}";
                float alertW = Mathf.Max(200f * scale, alertText.Length * 11.5f * scale + 24f * scale);
                float alertH = 28f * scale;
                float alertX = (sw - alertW) * 0.5f;

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                Texture2D pillTex = MakeRounded(220, 28, 14f, new Color(0.06f, 0.08f, 0.12f, 0.90f), rareAlertColor, 1.2f);
                GUI.DrawTexture(new Rect(alertX, curAlertY, alertW, alertH), pillTex);

                GUI.Label(new Rect(alertX, curAlertY, alertW, alertH), alertText, rareAlertStyle);

                GUI.color = prevColor;
            }

            // --- 1. Title: AKABEKO ---
            float headerTop = 24f * scale;
            float titleH    = 56f * scale;
            GUI.Label(new Rect(0, headerTop, sw, titleH), "AKABEKO", titleStyle);

            // --- 2. Red divider line ---
            float lineW = 180f * scale;
            float lineH = 2.5f * scale;
            float lineY = headerTop + titleH + 4f * scale;
            GUI.DrawTexture(new Rect((sw - lineW) * 0.5f, lineY, lineW, lineH),
                            MakeTex(2, 2, new Color(0.82f, 0.10f, 0.10f)));

            // --- 3. Zero-padded counter ---
            float countH = 48f * scale;
            float countY = lineY + lineH + 4f * scale;
            GUI.Label(new Rect(0, countY, sw, countH), swipeCount.ToString("D3"), countStyle);

            // --- 4. Notification popup ---
            if (notificationTimer > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(notificationTimer));
                float popW = 300f * scale, popH = 44f * scale;
                GUI.Box(new Rect((sw - popW) * 0.5f, countY + countH + 8f, popW, popH),
                        notificationMessage, popupStyle);
                GUI.color = Color.white;
            }

            // --- 5. Camera icon top-left ---
            float iconSz = 38f * scale;
            if (GUI.Button(new Rect(14f * scale, 14f * scale, iconSz, iconSz), "[S]", menuButtonStyle))
            {
                if (screenshotManager != null) screenshotManager.TakeScreenshot();
                ShowNotification("Screenshot saved");
            }

            // --- 6. Menu toggle top-right ---
            float btnSz = 38f * scale;
            if (GUI.Button(new Rect(sw - btnSz - 14f * scale, 14f * scale, btnSz, btnSz),
                           showControls ? "X" : "=", menuButtonStyle))
                showControls = !showControls;

            // --- 7. Admin Config Overlay (Matching Images 1 & 2) ---
            if (!showControls) return;

            DrawAdminConfigUI(sw, sh, scale);
        }

        private Vector2 actionScrollPos = Vector2.zero;

        /// <summary>
        /// ユーザーから提示された設計画像（1枚目・2枚目）を完全再現するアドミンパネルUI
        /// </summary>
        private void DrawAdminConfigUI(float sw, float sh, float scale)
        {
            if (actionConfig == null) actionConfig = StageActionConfig.Instance ?? FindFirstObjectByType<StageActionConfig>();
            if (actionConfig == null) return;

            // 背景カード領域
            float panelW = Mathf.Min(1150f * scale, sw - 40f);
            float panelH = 520f * scale;
            float panelX = (sw - panelW) * 0.5f;
            float panelY = (sh - panelH) * 0.5f + 30f * scale;

            // 全体背景（ホワイト/ライトグレーカード風）
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", GUI.skin.window);

            float curY = panelY + 20f * scale;
            float startX = panelX + 25f * scale;
            float headerW = 120f * scale;
            float itemW   = 120f * scale;
            float itemH   = 40f * scale;
            float gapX    = 20f * scale;
            float rowGapY = 90f * scale;

            // ================= 1. Color Row =================
            GUI.Box(new Rect(startX, curY, headerW, itemH), "Color", headerBoxStyle);

            var colors = actionConfig.GetAllColors();
            for (int i = 0; i < colors.Count; i++)
            {
                var colEntry = colors[i];
                float x = startX + headerW + gapX + i * (itemW + gapX);
                bool isActive = activeColor.Equals(colEntry.colorName, System.StringComparison.OrdinalIgnoreCase);

                if (GUI.Button(new Rect(x, curY, itemW, itemH), colEntry.colorName, isActive ? activeItemBoxStyle : itemBoxStyle))
                {
                    activeColor = colEntry.colorName;
                    if (stageActionController != null) stageActionController.ForceSetColor(colEntry.colorName, 30);
                    else if (rareMotionSystem != null) rareMotionSystem.SetColorByName(colEntry.colorName);
                    ShowNotification($"Test Color: {colEntry.colorName}");
                }

                float pct = colEntry.probability * 100f;
                Rect sliderRect = new Rect(x, curY + itemH + 6f * scale, itemW - 40f * scale, 22f * scale);
                float newPct = GUI.HorizontalSlider(sliderRect, pct, 0f, 100f);
                if (!Mathf.Approximately(newPct, pct))
                {
                    actionConfig.SetColorProbability(colEntry.colorName, newPct / 100f);
                }

                Rect labelRect = new Rect(x + itemW - 36f * scale, curY + itemH + 4f * scale, 40f * scale, 24f * scale);
                GUI.Label(labelRect, FormatPercent(newPct), percentStyle);
            }

            curY += rowGapY;

            // ================= 2. Stage Row =================
            GUI.Box(new Rect(startX, curY, headerW, itemH), "Stage", headerBoxStyle);

            var stageProbs = actionConfig.GetAllStageProbabilities();
            for (int i = 0; i < stageProbs.Count; i++)
            {
                var stgEntry = stageProbs[i];
                float x = startX + headerW + gapX + i * (itemW + gapX);
                bool isSelectedStage = selectedViewStage.Equals(stgEntry.stageName, System.StringComparison.OrdinalIgnoreCase);

                if (GUI.Button(new Rect(x, curY, itemW, itemH), stgEntry.stageName, isSelectedStage ? activeItemBoxStyle : itemBoxStyle))
                {
                    selectedViewStage = stgEntry.stageName;
                    if (stageActionController != null) stageActionController.ForceSetStage(stgEntry.stageName, 30);
                    else if (stageManager != null) stageManager.ChangeScene(stgEntry.stageName.ToLower());
                    ShowNotification($"Selected Stage: {stgEntry.stageName}");
                }

                float pct = stgEntry.probability * 100f;
                Rect sliderRect = new Rect(x, curY + itemH + 6f * scale, itemW - 40f * scale, 22f * scale);
                float newPct = GUI.HorizontalSlider(sliderRect, pct, 0f, 100f);
                if (!Mathf.Approximately(newPct, pct))
                {
                    actionConfig.SetStageProbability(stgEntry.stageName, newPct / 100f);
                }

                Rect labelRect = new Rect(x + itemW - 36f * scale, curY + itemH + 4f * scale, 40f * scale, 24f * scale);
                GUI.Label(labelRect, FormatPercent(newPct), percentStyle);
            }

            curY += rowGapY;

            // ================= 3. Action Row (All 12 Actions with ScrollView) =================
            GUI.Box(new Rect(startX, curY, headerW, itemH), "Action", headerBoxStyle);

            AkabekoAction[] actions = (AkabekoAction[])System.Enum.GetValues(typeof(AkabekoAction));
            List<AkabekoAction> activeActionList = new List<AkabekoAction>();
            foreach (var a in actions)
            {
                if (a != AkabekoAction.Sound) activeActionList.Add(a);
            }

            float scrollAreaW = panelW - headerW - gapX - 45f * scale;
            float totalActionContentW = activeActionList.Count * (itemW + gapX);

            Rect viewRect = new Rect(startX + headerW + gapX, curY, scrollAreaW, itemH + 42f * scale);
            Rect contentRect = new Rect(0, 0, totalActionContentW, itemH + 28f * scale);

            actionScrollPos = GUI.BeginScrollView(viewRect, actionScrollPos, contentRect, true, false);

            for (int i = 0; i < activeActionList.Count; i++)
            {
                AkabekoAction act = activeActionList[i];
                string actDisplayName = act switch
                {
                    AkabekoAction.None         => "None",
                    AkabekoAction.Bobbing      => "Bubble",
                    AkabekoAction.FlyAway     => "flyaway",
                    AkabekoAction.Spin        => "Spin",
                    AkabekoAction.Shake       => "Shake",
                    AkabekoAction.ScalePulse  => "SquashBounce",
                    AkabekoAction.SuperNova    => "SuperNova",
                    AkabekoAction.Wormhole     => "Wormhole",
                    AkabekoAction.Clones       => "Clones",
                    AkabekoAction.MatrixGlitch => "MatrixGlitch",
                    AkabekoAction.DiscoParty   => "DiscoParty",
                    AkabekoAction.Tornado      => "Tornado",
                    _                         => act.ToString()
                };

                float x = i * (itemW + gapX);

                float currentProb = actionConfig.GetProbability(selectedViewStage, act);
                bool isHighlighted = (currentProb > 0.04f && act != AkabekoAction.None);

                if (GUI.Button(new Rect(x, 0, itemW, itemH), actDisplayName, isHighlighted ? activeItemBoxStyle : itemBoxStyle))
                {
                    if (stageActionController != null) stageActionController.ForceTriggerAction(act);
                    ShowNotification($"Test Action: {actDisplayName}");
                }

                float pct = currentProb * 100f;
                Rect sliderRect = new Rect(x, itemH + 6f * scale, itemW - 40f * scale, 22f * scale);
                float newPct = GUI.HorizontalSlider(sliderRect, pct, 0f, 100f);
                if (!Mathf.Approximately(newPct, pct))
                {
                    actionConfig.SetProbability(selectedViewStage, act, newPct / 100f);
                }

                Rect labelRect = new Rect(x + itemW - 36f * scale, itemH + 4f * scale, 40f * scale, 24f * scale);
                GUI.Label(labelRect, FormatPercent(newPct), percentStyle);
            }

            GUI.EndScrollView();

            curY += rowGapY + 25f * scale;

            // ================= 4. Save & Reset Bottom Bar =================
            float saveW = 160f * scale;
            float saveH = 44f * scale;
            float saveX = (sw - saveW) * 0.5f;

            if (GUI.Button(new Rect(saveX, curY, saveW, saveH), "Save", saveButtonStyle))
            {
                actionConfig.SaveConfig();
                ShowNotification("★ Config Saved! ★");
            }

            float resetW = 120f * scale;
            float resetX = panelX + panelW - resetW - 25f * scale;
            if (GUI.Button(new Rect(resetX, curY + 6f * scale, resetW, 32f * scale), "Reset Defaults", itemBoxStyle))
            {
                actionConfig.ResetToDefaults();
                ShowNotification("Config Reset");
            }
        }

        private void TriggerColor(string id)
        {
            activeColor = id;
            if (id == "Normal")
            {
                rareMotionSystem?.SendMessage("ResetColor", SendMessageOptions.DontRequireReceiver);
                ShowNotification("Normal");
                return;
            }
            string matName = id == "Gold" ? "Mat_Gold" : id == "Silver" ? "Mat_Silver" : "Mat_Rainbow";
            if (rareMotionSystem != null)
            {
                var data = new RareMotionData
                {
                    motionId     = "ui_" + matName.ToLower(),
                    motionName   = id,
                    type         = MotionType.COLOR_CHANGE,
                    materialName = matName
                };
                rareMotionSystem.SendMessage("ChangeColor", data, SendMessageOptions.DontRequireReceiver);
            }
            ShowNotification(id);
        }

        private string FormatPercent(float pct)
        {
            if (pct <= 0.0001f) return "0%";
            if (pct < 1f) return $"{pct:F1}%";
            return $"{Mathf.RoundToInt(pct)}%";
        }
    }
}

