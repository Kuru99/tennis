using PrideCourt.AI;
using PrideCourt.Domain;
using PrideCourt.Input;
using PrideCourt.Networking;
using PrideCourt.Presentation;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    [DefaultExecutionOrder(10000)]
    public sealed class HudSettingsController : MonoBehaviour
    {
        private bool open;
        private TennisMatchController match;
        private LanBattleSessionController lanSession;
        private MvpFrontEndController frontEnd;

        public bool IsOpen => open;
        public static bool IsAnyOpen { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            AudioListener.volume = PrideCourtSettingsPreferences.MasterVolume;
        }

        private void Update()
        {
            ResolveReferences();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F10)) TryToggle();
            if (!open && UnityEngine.Input.GetKeyDown(KeyCode.F8) && !IsMultiplayer())
                CpuDifficultyPreferences.Cycle();
        }

        private void OnDisable()
        {
            if (open) SetOpen(false);
            IsAnyOpen = false;
        }

        private void OnGUI()
        {
            ResolveReferences();
            Rect safe = GetGuiSafeArea();
            float uiScale = UiScale();
            bool canOpen = CanOpenNow();
            Rect settingsButton = new Rect(safe.xMax - 132f * uiScale, safe.y + 12f * uiScale,
                120f * uiScale, 42f * uiScale);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = open || canOpen;
            if (GUI.Button(settingsButton, open ? "再開" : "設定",
                    PrideCourtUiTheme.Button(open ? PrideCourtUiTheme.Tone.Magenta : PrideCourtUiTheme.Tone.Cyan,
                        uiScale, 15)))
                TryToggle();
            GUI.enabled = previousEnabled;

            if (!open)
            {
                if (!canOpen && IsMultiplayer() && match != null && match.HasStarted)
                {
                    GUI.Label(new Rect(settingsButton.x - 166f * uiScale, settingsButton.y + 7f * uiScale,
                            158f * uiScale, 28f * uiScale),
                        "インプレイ中は設定不可",
                        PrideCourtUiTheme.Label(uiScale, 11, TextAnchor.MiddleRight, PrideCourtUiTheme.Muted));
                }
                return;
            }

            PrideCourtUiTheme.DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g, PrideCourtUiTheme.Ink.b, 0.72f));
            DrawSettingsPanel(safe, uiScale);
        }

        private void DrawSettingsPanel(Rect safe, float uiScale)
        {
            float panelWidth = Mathf.Min(620f * uiScale, safe.width - 24f * uiScale);
            float panelHeight = Mathf.Min(640f * uiScale, safe.height - 24f * uiScale);
            Rect panel = new Rect(safe.center.x - panelWidth * 0.5f, safe.center.y - panelHeight * 0.5f,
                panelWidth, panelHeight);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Cyan, uiScale, "SETTINGS / PAUSE");

            float x = panel.x + 30f * uiScale;
            float width = panel.width - 60f * uiScale;
            float y = panel.y + 46f * uiScale;
            GUI.Label(new Rect(x, y, width, 28f * uiScale),
                IsMultiplayer() ? "通信対戦を安全な待機状態で停止中" : match != null && match.HasStarted ? "試合を一時停止中" : "ゲーム設定",
                PrideCourtUiTheme.Label(uiScale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Cyan));

            y += 40f * uiScale;
            DrawVolumeRow(x, y, width, uiScale);
            y += 62f * uiScale;
            DrawQualityRow(x, y, width, uiScale);
            y += 68f * uiScale;
            DrawDisplayRows(x, y, width, uiScale);
            y += 166f * uiScale;
            DrawStickRow(x, y, width, uiScale);
            y += 58f * uiScale;
            DrawDifficultyRow(x, y, width, uiScale);

            float bottomY = panel.yMax - 106f * uiScale;
            if (match != null && match.HasStarted)
            {
                string returnLabel = IsMultiplayer() ? "通信を終了してタイトルへ" : "タイトルへ戻る";
                if (GUI.Button(new Rect(x, bottomY, width, 42f * uiScale), returnLabel,
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, uiScale, 14)))
                {
                    SetOpen(false);
                    frontEnd?.ReturnToTitleFromSettings();
                    return;
                }
                bottomY += 50f * uiScale;
            }

            if (GUI.Button(new Rect(x, bottomY, width, 42f * uiScale),
                    match != null && match.HasStarted ? "試合を再開" : "閉じる",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, uiScale, 15)))
                SetOpen(false);
        }

        private static void DrawVolumeRow(float x, float y, float width, float uiScale)
        {
            GUI.Label(new Rect(x, y, 116f * uiScale, 30f * uiScale), "音量",
                PrideCourtUiTheme.Label(uiScale, 14, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
            float volume = GUI.HorizontalSlider(new Rect(x + 118f * uiScale, y + 8f * uiScale,
                    width - 196f * uiScale, 22f * uiScale),
                PrideCourtSettingsPreferences.MasterVolume, 0f, 1f);
            GUI.Label(new Rect(x + width - 70f * uiScale, y, 70f * uiScale, 30f * uiScale),
                Mathf.RoundToInt(volume * 100f) + "%",
                PrideCourtUiTheme.Label(uiScale, 13, TextAnchor.MiddleRight, PrideCourtUiTheme.Cyan));
            if (!Mathf.Approximately(volume, PrideCourtSettingsPreferences.MasterVolume))
                PrideCourtSettingsPreferences.MasterVolume = volume;
        }

        private static void DrawQualityRow(float x, float y, float width, float uiScale)
        {
            GUI.Label(new Rect(x, y, 116f * uiScale, 36f * uiScale), "画質",
                PrideCourtUiTheme.Label(uiScale, 14, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
            float buttonX = x + 118f * uiScale;
            float gap = 8f * uiScale;
            float buttonWidth = (width - 118f * uiScale - gap * 2f) / 3f;
            DrawQualityButton(buttonX, y, buttonWidth, uiScale, PrideCourtQualityPreset.Performance, "軽量");
            DrawQualityButton(buttonX + buttonWidth + gap, y, buttonWidth, uiScale,
                PrideCourtQualityPreset.Balanced, "標準");
            DrawQualityButton(buttonX + (buttonWidth + gap) * 2f, y, buttonWidth, uiScale,
                PrideCourtQualityPreset.Quality, "高画質");
        }

        private static void DrawQualityButton(float x, float y, float width, float uiScale,
            PrideCourtQualityPreset preset, string label)
        {
            PrideCourtUiTheme.Tone tone = PrideCourtSettingsPreferences.QualityPreset == preset
                ? PrideCourtUiTheme.Tone.Magenta
                : PrideCourtUiTheme.Tone.Violet;
            if (GUI.Button(new Rect(x, y, width, 38f * uiScale), label,
                    PrideCourtUiTheme.Button(tone, uiScale, 13)))
                PrideCourtSettingsPreferences.QualityPreset = preset;
        }

        private static void DrawDisplayRows(float x, float y, float width, float uiScale)
        {
            GUIStyle toggleStyle = PrideCourtUiTheme.Toggle(uiScale);
            bool visible = GUI.Toggle(new Rect(x, y, width * 0.5f, 28f * uiScale),
                HudPreferences.Visible, "試合表示", toggleStyle);
            bool resources = GUI.Toggle(new Rect(x + width * 0.5f, y, width * 0.5f, 28f * uiScale),
                HudPreferences.ResourcesEnabled, "スタミナ・SP", toggleStyle);
            bool cards = GUI.Toggle(new Rect(x, y + 34f * uiScale, width * 0.5f, 28f * uiScale),
                HudPreferences.CardsEnabled, "カード表示", toggleStyle);
            if (visible != HudPreferences.Visible) HudPreferences.Visible = visible;
            if (resources != HudPreferences.ResourcesEnabled) HudPreferences.ResourcesEnabled = resources;
            if (cards != HudPreferences.CardsEnabled) HudPreferences.CardsEnabled = cards;

            GUI.Label(new Rect(x, y + 76f * uiScale, 116f * uiScale, 28f * uiScale), "表示サイズ",
                PrideCourtUiTheme.Label(uiScale, 14, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
            float scale = GUI.HorizontalSlider(new Rect(x + 118f * uiScale, y + 84f * uiScale,
                    width - 196f * uiScale, 20f * uiScale), HudPreferences.Scale, 0.7f, 1.3f);
            GUI.Label(new Rect(x + width - 70f * uiScale, y + 76f * uiScale, 70f * uiScale, 28f * uiScale),
                Mathf.RoundToInt(scale * 100f) + "%",
                PrideCourtUiTheme.Label(uiScale, 13, TextAnchor.MiddleRight, PrideCourtUiTheme.Cyan));
            if (!Mathf.Approximately(scale, HudPreferences.Scale)) HudPreferences.Scale = scale;
        }

        private static void DrawStickRow(float x, float y, float width, float uiScale)
        {
            GUI.Label(new Rect(x, y, 132f * uiScale, 36f * uiScale), "スマホスティック",
                PrideCourtUiTheme.Label(uiScale, 14, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
            float buttonX = x + 136f * uiScale;
            float gap = 8f * uiScale;
            float buttonWidth = (width - 136f * uiScale - gap * 2f) / 3f;
            DrawStickSizeButton(buttonX, y, buttonWidth, uiScale, VirtualStickSize.Small, "小");
            DrawStickSizeButton(buttonX + buttonWidth + gap, y, buttonWidth, uiScale, VirtualStickSize.Medium, "中");
            DrawStickSizeButton(buttonX + (buttonWidth + gap) * 2f, y, buttonWidth, uiScale,
                VirtualStickSize.Large, "大");
        }

        private void DrawDifficultyRow(float x, float y, float width, float uiScale)
        {
            bool multiplayer = IsMultiplayer();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !multiplayer;
            GUI.Label(new Rect(x, y, width - 104f * uiScale, 36f * uiScale),
                multiplayer ? "相手の強さ / 通信対戦では変更不可" : "相手の強さ / " + DifficultyName(CpuDifficultyPreferences.Current),
                PrideCourtUiTheme.Label(uiScale, 13, TextAnchor.MiddleLeft,
                    multiplayer ? PrideCourtUiTheme.Muted : PrideCourtUiTheme.Paper));
            if (GUI.Button(new Rect(x + width - 96f * uiScale, y, 96f * uiScale, 36f * uiScale), "変更",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, uiScale, 13)))
                CpuDifficultyPreferences.Cycle();
            GUI.enabled = previousEnabled;
        }

        private static void DrawStickSizeButton(float x, float y, float width, float uiScale,
            VirtualStickSize size, string label)
        {
            PrideCourtUiTheme.Tone tone = VirtualStickPreferences.Current == size
                ? PrideCourtUiTheme.Tone.Magenta
                : PrideCourtUiTheme.Tone.Violet;
            if (GUI.Button(new Rect(x, y, width, 36f * uiScale), label,
                    PrideCourtUiTheme.Button(tone, uiScale, 14)))
                VirtualStickPreferences.Current = size;
        }

        private void TryToggle()
        {
            if (open)
            {
                SetOpen(false);
                return;
            }
            if (CanOpenNow()) SetOpen(true);
        }

        private void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            IsAnyOpen = value;
            if (match != null && match.HasStarted)
            {
                if (lanSession != null) lanSession.SetSettingsMenuOpen(value);
                else match.SetSettingsPaused(value);
            }
        }

        private bool CanOpenNow()
        {
            if (match == null) return true;
            bool multiplayer = IsMultiplayer();
            if (match.IsGameplayPaused && !open && !multiplayer) return false;
            return SettingsAvailabilityPolicy.CanOpen(multiplayer, match.HasStarted,
                match.Phase, match.ServeRestrictionsActive);
        }

        private bool IsMultiplayer()
        {
            return lanSession != null && lanSession.IsConnected;
        }

        private void ResolveReferences()
        {
            if (match == null) match = GetComponent<TennisMatchController>();
            if (match == null) match = FindAnyObjectByType<TennisMatchController>();
            if (lanSession == null) lanSession = GetComponent<LanBattleSessionController>();
            if (lanSession == null) lanSession = FindAnyObjectByType<LanBattleSessionController>();
            if (frontEnd == null) frontEnd = GetComponent<MvpFrontEndController>();
            if (frontEnd == null) frontEnd = FindAnyObjectByType<MvpFrontEndController>();
        }

        private static Rect GetGuiSafeArea()
        {
            Rect safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }

        private static float UiScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.3f);
        }

        private static string DifficultyName(CpuDifficulty difficulty)
        {
            return difficulty switch
            {
                CpuDifficulty.Easy => "やさしい",
                CpuDifficulty.Hard => "むずかしい",
                _ => "ふつう"
            };
        }
    }
}
