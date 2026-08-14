using UnityEngine;

namespace Akabeko
{
    /// <summary>
    /// Clean card-style UI: AKABEKO title + red line + zero-padded counter + hidden menu controls
    /// </summary>
    public class DynamicUIOverlay : MonoBehaviour
    {
        private RareMotionSystem rareMotionSystem;
        private StageManager stageManager;
        private StageActionConfig actionConfig;
        private ScreenshotManager screenshotManager;
        private ShareManager shareManager;

        private int swipeCount = 0;
        private string notificationMessage = "";
        private float notificationTimer = 0f;

        private string activeColor = "Normal";
        private string activeStage = "Default";
        private bool showControls = false;
        private bool showActionConfig = false; // ACTION行の確率テーブルを展開表示するトグル

        private GUIStyle titleStyle;
        private GUIStyle countStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle controlButtonStyle;
        private GUIStyle controlActiveStyle;
        private GUIStyle controlPanelStyle;
        private GUIStyle popupStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            rareMotionSystem = FindFirstObjectByType<RareMotionSystem>();
            stageManager = FindFirstObjectByType<StageManager>();
            actionConfig = FindFirstObjectByType<StageActionConfig>();
            screenshotManager = FindFirstObjectByType<ScreenshotManager>();
            shareManager = FindFirstObjectByType<ShareManager>();
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
            ShowNotification("* " + motion.motionName + " *");
        }

        public void ShowNotification(string msg)
        {
            notificationMessage = msg;
            notificationTimer = 3.0f;
        }

        private void Update()
        {
            if (notificationTimer > 0) notificationTimer -= Time.deltaTime;
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

            Color red      = new Color(0.82f, 0.10f, 0.10f, 1f);
            Color darkGray = new Color(0.20f, 0.20f, 0.20f, 1f);
            Color panelBg  = new Color(0.15f, 0.15f, 0.15f, 0.90f);
            Color activeBg = new Color(0.82f, 0.10f, 0.10f, 0.95f);
            Color btnBg    = new Color(0.25f, 0.25f, 0.25f, 0.90f);

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

            controlPanelStyle = new GUIStyle(GUI.skin.box);
            controlPanelStyle.normal.background = MakeRounded(200, 80, 12f, panelBg, new Color(0.4f, 0.4f, 0.4f, 0.5f), 1f);
            controlPanelStyle.border = new RectOffset(12, 12, 12, 12);

            controlButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            controlButtonStyle.normal.background = MakeRounded(100, 36, 8f, btnBg, new Color(0.5f, 0.5f, 0.5f, 0.4f), 1f);
            controlButtonStyle.hover.background  = MakeRounded(100, 36, 8f, new Color(0.35f, 0.35f, 0.35f, 0.95f), new Color(0.6f, 0.6f, 0.6f, 0.6f), 1f);
            controlButtonStyle.border = new RectOffset(8, 8, 8, 8);
            controlButtonStyle.margin = new RectOffset(3, 3, 3, 3);
            controlButtonStyle.normal.textColor = Color.white;

            controlActiveStyle = new GUIStyle(controlButtonStyle);
            controlActiveStyle.normal.background = MakeRounded(100, 36, 8f, activeBg, new Color(1f, 0.6f, 0.6f, 0.7f), 1.5f);
            controlActiveStyle.normal.textColor  = Color.white;

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
            labelStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float scale = Mathf.Clamp(sw / 1280f, 0.6f, 1.4f);

            titleStyle.fontSize   = Mathf.RoundToInt(44 * scale);
            countStyle.fontSize   = Mathf.RoundToInt(36 * scale);
            menuButtonStyle.fontSize = Mathf.RoundToInt(22 * scale);
            labelStyle.fontSize   = Mathf.RoundToInt(12 * scale);
            controlButtonStyle.fontSize = Mathf.RoundToInt(13 * scale);
            controlActiveStyle.fontSize = Mathf.RoundToInt(13 * scale);
            popupStyle.fontSize   = Mathf.RoundToInt(16 * scale);

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

            // --- 7. Control panel (hidden by default) ---
            if (!showControls) return;

            float panelW = 500f * scale;
            float panelH = 142f * scale;  // 3行分 (COLOR + STAGE + ACTION)
            float panelX = (sw - panelW) * 0.5f;
            float panelY = sh - panelH - 16f * scale;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", controlPanelStyle);

            GUILayout.BeginArea(new Rect(panelX + 10f * scale, panelY + 8f * scale,
                                         panelW - 20f * scale, panelH - 16f * scale));

            float rowH = 32f * scale;

            // Color row
            GUILayout.BeginHorizontal();
            GUILayout.Label("COLOR", labelStyle, GUILayout.Width(50f * scale), GUILayout.Height(rowH));
            if (GUILayout.Button("Normal",  activeColor == "Normal"  ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) TriggerColor("Normal");
            if (GUILayout.Button("Gold",    activeColor == "Gold"    ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) TriggerColor("Gold");
            if (GUILayout.Button("Silver",  activeColor == "Silver"  ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) TriggerColor("Silver");
            if (GUILayout.Button("Rainbow", activeColor == "Rainbow" ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) TriggerColor("Rainbow");
            GUILayout.EndHorizontal();

            // Stage row
            GUILayout.BeginHorizontal();
            GUILayout.Label("STAGE", labelStyle, GUILayout.Width(50f * scale), GUILayout.Height(rowH));
            if (GUILayout.Button("Default", activeStage == "Default"  ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) { stageManager?.ResetScene(); activeStage = "Default"; ShowNotification("Default"); }
            if (GUILayout.Button("Space",   activeStage == "Space"    ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) { stageManager?.ChangeScene("space"); activeStage = "Space"; ShowNotification("Space"); }
            if (GUILayout.Button("Sea",     activeStage == "Sea"      ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) { stageManager?.ChangeScene("sea"); activeStage = "Sea"; ShowNotification("Sea"); }
            if (GUILayout.Button("Volcano", activeStage == "Volcano"  ? controlActiveStyle : controlButtonStyle, GUILayout.Height(rowH))) { stageManager?.ChangeScene("volcano"); activeStage = "Volcano"; ShowNotification("Volcano"); }
            GUILayout.EndHorizontal();

            // Action row (確率テーブル)
            GUILayout.BeginHorizontal();
            GUILayout.Label("ACTION", labelStyle, GUILayout.Width(50f * scale), GUILayout.Height(rowH));
            if (GUILayout.Button(showActionConfig ? "▲ Close" : "▼ Config", controlButtonStyle, GUILayout.Height(rowH)))
                showActionConfig = !showActionConfig;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // --- Action Config サブパネル ---
            if (showActionConfig && actionConfig != null)
            {
                DrawActionConfigPanel(sw, sh, scale, panelX, panelY);
            }

        } // end OnGUI

        /// <summary>
        /// アクション確率テーブルを表示・編集するサブパネル
        /// ステージ列 × アクション行のマトリクスをスライダーで表示する
        /// </summary>
        private void DrawActionConfigPanel(float sw, float sh, float scale, float mainPanelX, float mainPanelY)
        {
            var entries = actionConfig.GetAllEntries();
            int cols = entries.Count + 1; // ラベル列 + ステージ数
            AkabekoAction[] actions = (AkabekoAction[])System.Enum.GetValues(typeof(AkabekoAction));
            int rows = actions.Length + 1; // ヘッダー行 + アクション数

            float cellW = 90f * scale;
            float labelW = 80f * scale;
            float cellH = 28f * scale;
            float subW = labelW + cellW * entries.Count + 20f * scale;
            float subH = cellH * rows + 20f * scale;

            // メインパネルの上に表示
            float subX = (sw - subW) * 0.5f;
            float subY = mainPanelY - subH - 8f * scale;

            GUI.Box(new Rect(subX, subY, subW, subH), "", controlPanelStyle);

            float cx = subX + 10f * scale;
            float cy = subY + 8f * scale;

            // ヘッダー行 (ステージ名)
            GUI.Label(new Rect(cx, cy, labelW, cellH), "", labelStyle);
            for (int i = 0; i < entries.Count; i++)
            {
                GUI.Label(new Rect(cx + labelW + cellW * i, cy, cellW, cellH),
                          entries[i].stageName.ToUpper(), labelStyle);
            }
            cy += cellH;

            // アクション行
            foreach (AkabekoAction action in actions)
            {
                GUI.Label(new Rect(cx, cy, labelW, cellH), action.ToString(), labelStyle);
                for (int i = 0; i < entries.Count; i++)
                {
                    float current = entries[i].GetProbability(action);
                    float newVal = GUI.HorizontalSlider(
                        new Rect(cx + labelW + cellW * i, cy + 4f * scale, cellW - 8f * scale, cellH - 8f * scale),
                        current, 0f, 1f);
                    if (!Mathf.Approximately(newVal, current))
                        entries[i].SetProbability(action, newVal);

                    // 確率値ラベル
                    GUI.Label(new Rect(cx + labelW + cellW * i, cy, cellW - 4f * scale, cellH),
                              (newVal * 100f).ToString("F0") + "%", labelStyle);
                }
                cy += cellH;
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
    }
}
