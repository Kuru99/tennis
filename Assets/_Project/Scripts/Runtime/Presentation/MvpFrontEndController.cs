using System.Collections.Generic;
using PrideCourt.Cards;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Networking;
using UnityEngine;

namespace PrideCourt.Presentation
{
    public sealed class MvpFrontEndController : MonoBehaviour
    {
        public const string OpeningArtworkResourcePath = "Stadium/FantasyStadiumMural";

        private const float OpeningDurationSeconds = 8.2f;

        [SerializeField] private TennisMatchController match;
        [SerializeField] private TennisAthleteController player;
        [SerializeField] private TennisAthleteController cpu;
        [SerializeField] private LanBattleSessionController lanSession;

        private readonly Dictionary<CardId, int> deckCounts = new Dictionary<CardId, int>();
        private readonly PrideCourtFrontEndFlow frontEndFlow = new PrideCourtFrontEndFlow();
        private AthleteIdentity selectedIdentity;
        private Texture2D characterAtlas;
        private Texture2D openingArtwork;
        private Vector2 scroll;
        private float openingStartedAt;
        private string manualLanAddress = "192.168.";
        private string manualOnlineCode = string.Empty;

        public FrontEndScreen CurrentScreen => frontEndFlow.Screen;
        public MultiplayerEntry PendingMultiplayerEntry => frontEndFlow.PendingMultiplayerEntry;

        public void Configure(TennisMatchController configuredMatch, TennisAthleteController configuredPlayer, TennisAthleteController configuredCpu)
        {
            match = configuredMatch;
            player = configuredPlayer;
            cpu = configuredCpu;
            EnsureLanSession();
        }

        private void Awake()
        {
            EnsureLanSession();
            selectedIdentity = (AthleteIdentity)Mathf.Clamp(PlayerPrefs.GetInt("PrideCourt.PlayerIdentity", 0), 0, 1);
            characterAtlas = Resources.Load<Texture2D>("CharacterCardAtlas");
            openingArtwork = Resources.Load<Texture2D>(OpeningArtworkResourcePath);
            openingStartedAt = Time.unscaledTime;
            LoadDeck(selectedIdentity);
        }

        private void Update()
        {
            if (match == null) return;
            if (!match.HasStarted)
            {
                if (frontEndFlow.Screen == FrontEndScreen.Opening &&
                    (Time.unscaledTime - openingStartedAt >= OpeningDurationSeconds || OpeningSkipPressed()))
                {
                    ApplyFrontEndAction(FrontEndAction.FinishOpening);
                    return;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    ApplyFrontEndAction(FrontEndAction.Back);
                    return;
                }

                HandleFrontEndKeyboard();
                return;
            }

            if (match.Phase == MatchPhase.MatchOver && UnityEngine.Input.GetKeyDown(KeyCode.Return)) StartMatch();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) ReturnToSetup();
        }

        private void OnGUI()
        {
            if (HudSettingsController.IsAnyOpen) return;
            if (match == null || player == null || cpu == null) return;
            if (!match.HasStarted)
            {
                switch (frontEndFlow.Screen)
                {
                    case FrontEndScreen.Opening:
                        DrawOpeningMovie();
                        break;
                    case FrontEndScreen.Title:
                        DrawTitle();
                        break;
                    case FrontEndScreen.ModeSelect:
                        DrawModeSelect();
                        break;
                    case FrontEndScreen.MultiplayerSelect:
                        DrawMultiplayerSelect();
                        break;
                    case FrontEndScreen.LocalNetworkLobby:
                        DrawLocalNetworkLobby();
                        break;
                    case FrontEndScreen.OnlineNetworkLobby:
                        DrawOnlineNetworkLobby();
                        break;
                    default:
                        DrawSetup();
                        break;
                }
                return;
            }

            if (match.Phase == MatchPhase.MatchOver) DrawMatchOver();
        }

        public bool ApplyFrontEndAction(FrontEndAction action)
        {
            if (action == FrontEndAction.Back && lanSession != null &&
                (frontEndFlow.Screen == FrontEndScreen.LocalNetworkLobby ||
                 frontEndFlow.Screen == FrontEndScreen.OnlineNetworkLobby) &&
                (lanSession.IsBusy || lanSession.IsConnected))
            {
                lanSession.Disconnect();
            }
            bool changed = frontEndFlow.Apply(action);
            if (changed && frontEndFlow.Screen == FrontEndScreen.Opening)
            {
                openingStartedAt = Time.unscaledTime;
            }
            if (changed && frontEndFlow.Screen == FrontEndScreen.LocalNetworkLobby)
            {
                lanSession?.RefreshHosts();
            }
            return changed;
        }

        public void ReturnToTitleFromSettings()
        {
            if (lanSession != null && (lanSession.IsConnected || lanSession.IsBusy))
                lanSession.Disconnect();
            else
                match?.ReturnToSetup();
            ApplyFrontEndAction(FrontEndAction.ReturnToTitle);
        }

        private void EnsureLanSession()
        {
            if (lanSession == null) lanSession = GetComponent<LanBattleSessionController>();
            if (lanSession == null) lanSession = gameObject.AddComponent<LanBattleSessionController>();
            if (match != null && player != null && cpu != null)
            {
                TennisBallController configuredBall = FindAnyObjectByType<TennisBallController>();
                if (configuredBall != null) lanSession.Configure(match, player, cpu, configuredBall);
            }
        }

        private void HandleFrontEndKeyboard()
        {
            if (frontEndFlow.Screen == FrontEndScreen.Title && UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                ApplyFrontEndAction(FrontEndAction.OpenModeSelect);
                return;
            }

            if (frontEndFlow.Screen == FrontEndScreen.Setup &&
                UnityEngine.Input.GetKeyDown(KeyCode.Return) && DeckSize == 16)
            {
                StartMatch();
                return;
            }

            if (!UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) && !UnityEngine.Input.GetKeyDown(KeyCode.Keypad1) &&
                !UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) && !UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
            {
                return;
            }

            bool first = UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1);
            if (frontEndFlow.Screen == FrontEndScreen.ModeSelect)
            {
                ApplyFrontEndAction(first ? FrontEndAction.SelectSolo : FrontEndAction.SelectMultiplayer);
            }
            else if (frontEndFlow.Screen == FrontEndScreen.MultiplayerSelect)
            {
                ApplyFrontEndAction(first ? FrontEndAction.SelectLocal : FrontEndAction.SelectNetwork);
            }
        }

        private static bool OpeningSkipPressed()
        {
            return UnityEngine.Input.anyKeyDown || UnityEngine.Input.GetMouseButtonDown(0) ||
                   UnityEngine.Input.touchCount > 0;
        }

        private void DrawOpeningMovie()
        {
            float elapsed = Time.unscaledTime - openingStartedAt;
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.15f));
            float zoom = Mathf.Clamp01(elapsed / OpeningDurationSeconds) * 38f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, reveal);
            DrawOpeningArtwork(new Rect(-zoom, -zoom * 0.55f, full.width + zoom * 2f, full.height + zoom * 1.1f));
            GUI.color = previous;
            PrideCourtUiTheme.DrawSolid(full, new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g,
                PrideCourtUiTheme.Ink.b, 0.28f));

            Rect safe = GetGuiSafeArea();
            float scale = UiScale();
            if (elapsed >= 2.15f && characterAtlas != null)
            {
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 2.15f) / 0.85f));
                float portraitWidth = Mathf.Min(300f * scale, safe.width * 0.27f);
                float portraitHeight = Mathf.Min(500f * scale, safe.height * 0.66f);
                float y = safe.y + (safe.height - portraitHeight) * 0.52f;
                Rect lux = new Rect(Mathf.Lerp(-portraitWidth, safe.x + safe.width * 0.055f, enter), y,
                    portraitWidth, portraitHeight);
                Rect bastion = new Rect(Mathf.Lerp(Screen.width, safe.xMax - safe.width * 0.055f - portraitWidth, enter), y,
                    portraitWidth, portraitHeight);
                GUI.Box(Grow(lux, 4f * scale), GUIContent.none,
                    PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Cyan, scale));
                GUI.Box(Grow(bastion, 4f * scale), GUIContent.none,
                    PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Yellow, scale));
                GUI.DrawTextureWithTexCoords(lux, characterAtlas, new Rect(0f, 0f, 0.25f, 1f), true);
                GUI.DrawTextureWithTexCoords(bastion, characterAtlas, new Rect(0.5f, 0f, 0.25f, 1f), true);
            }

            if (elapsed < 5.35f)
            {
                string line = elapsed < 2.9f ? "世界を決めるのは、力か。" : "速さか。譲れない誇りか。";
                GUI.Label(new Rect(safe.x, safe.y + safe.height * 0.13f, safe.width, 62f * scale), line,
                    PrideCourtUiTheme.Heading(scale * 0.82f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));
            }
            else
            {
                DrawLogo(new Rect(safe.x + safe.width * 0.2f, safe.y + safe.height * 0.2f,
                    safe.width * 0.6f, safe.height * 0.38f), scale * 0.86f);
            }

            float pulse = 0.58f + Mathf.Sin(Time.unscaledTime * 4f) * 0.18f;
            GUI.Label(new Rect(safe.x, safe.yMax - 46f * scale, safe.width, 32f * scale),
                "PRESS ANY BUTTON  /  TAP TO SKIP",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter,
                    new Color(PrideCourtUiTheme.Paper.r, PrideCourtUiTheme.Paper.g, PrideCourtUiTheme.Paper.b, pulse)));
        }

        private void DrawTitle()
        {
            DrawFrontEndBackdrop(0.56f);
            Rect safe = GetGuiSafeArea();
            float scale = UiScale();
            DrawLogo(new Rect(safe.x + safe.width * 0.15f, safe.y + safe.height * 0.13f,
                safe.width * 0.7f, safe.height * 0.38f), scale);

            float buttonWidth = Mathf.Min(500f * scale, safe.width * 0.62f);
            Rect start = new Rect(safe.center.x - buttonWidth * 0.5f, safe.y + safe.height * 0.61f,
                buttonWidth, 68f * scale);
            if (GUI.Button(start, "START  /  モード選択",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 22)))
            {
                ApplyFrontEndAction(FrontEndAction.OpenModeSelect);
            }

            Rect replay = new Rect(start.x + buttonWidth * 0.16f, start.yMax + 18f * scale,
                buttonWidth * 0.68f, 48f * scale);
            if (GUI.Button(replay, "OPENING  /  オープニング再生",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, 14)))
            {
                ApplyFrontEndAction(FrontEndAction.ReplayOpening);
            }

            GUI.Label(new Rect(safe.x, safe.yMax - 42f * scale, safe.width, 28f * scale),
                "ENTER: SELECT   •   TOUCH: TAP",
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
        }

        private void DrawModeSelect()
        {
            DrawFrontEndBackdrop(0.7f);
            Rect panel = FrontEndPanel(840f, 570f, out float scale);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Cyan, scale, "GAME MODE / モード選択");
            DrawMenuTitle(panel, scale, "誇りを懸ける舞台を選べ");

            Rect solo = new Rect(panel.x + 54f * scale, panel.y + 126f * scale,
                panel.width - 108f * scale, 94f * scale);
            if (GUI.Button(solo, "SOLO PLAY   /   ソロプレイ",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, 22)))
            {
                ApplyFrontEndAction(FrontEndAction.SelectSolo);
            }
            GUI.Label(new Rect(solo.x + 20f * scale, solo.yMax + 2f * scale, solo.width - 40f * scale, 30f * scale),
                "CPUを相手にキャラクターと16枚のデッキで挑む",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));

            Rect multi = new Rect(solo.x, solo.yMax + 70f * scale, solo.width, 94f * scale);
            if (GUI.Button(multi, "MULTI PLAY   /   マルチプレイ",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 22)))
            {
                ApplyFrontEndAction(FrontEndAction.SelectMultiplayer);
            }
            GUI.Label(new Rect(multi.x + 20f * scale, multi.yMax + 2f * scale, multi.width - 40f * scale, 30f * scale),
                "同じLANのPC・スマホ対戦 / ネット対戦は実装前",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
            DrawBackButton(panel, scale);
        }

        private void DrawMultiplayerSelect()
        {
            DrawFrontEndBackdrop(0.72f);
            Rect panel = FrontEndPanel(840f, 570f, out float scale);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Magenta, scale, "MULTI PLAY / マルチプレイ");
            DrawMenuTitle(panel, scale, "対戦する場所を選べ");

            Rect local = new Rect(panel.x + 54f * scale, panel.y + 126f * scale,
                panel.width - 108f * scale, 94f * scale);
            if (GUI.Button(local, "LOCAL BATTLE   /   ローカル対戦",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Yellow, scale, 21)))
            {
                ApplyFrontEndAction(FrontEndAction.SelectLocal);
            }
            GUI.Label(new Rect(local.x, local.yMax + 2f * scale, local.width, 30f * scale),
                "同じWi-Fi / LANのPC・スマホでクロスプレイ",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));

            Rect network = new Rect(local.x, local.yMax + 70f * scale, local.width, 94f * scale);
            GUI.enabled = PrideCourtFrontEndFlow.NetworkBattleAvailable;
            if (GUI.Button(network, "NETWORK BATTLE   /   ネット対戦   ※実装前",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, scale, 21)))
            {
                ApplyFrontEndAction(FrontEndAction.SelectNetwork);
            }
            GUI.enabled = true;
            GUI.Label(new Rect(network.x, network.yMax + 2f * scale, network.width, 30f * scale),
                "※実装前：現在は選択できません",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
            DrawBackButton(panel, scale);
        }

        private void DrawOnlineNetworkLobby()
        {
            DrawFrontEndBackdrop(0.76f);
            Rect panel = FrontEndPanel(980f, 650f, out float scale);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Violet, scale,
                "ONLINE LINK / ネット通信対戦");
            GUI.Label(new Rect(panel.x + 32f * scale, panel.y + 46f * scale, panel.width - 64f * scale, 42f * scale),
                "片方がコートを開き、表示された6桁のコードを相手へ伝えます",
                PrideCourtUiTheme.Label(scale, 15, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));

            float identityY = panel.y + 94f * scale;
            GUI.Label(new Rect(panel.x + 42f * scale, identityY, 220f * scale, 42f * scale), "使用キャラクター",
                PrideCourtUiTheme.Heading(scale * 0.44f, TextAnchor.MiddleLeft, PrideCourtUiTheme.Cyan));
            if (GUI.Button(new Rect(panel.x + 268f * scale, identityY, 154f * scale, 42f * scale), "ルクス",
                    PrideCourtUiTheme.Button(selectedIdentity == AthleteIdentity.Lux
                        ? PrideCourtUiTheme.Tone.Cyan : PrideCourtUiTheme.Tone.Neutral, scale, 15)))
                SelectIdentity(AthleteIdentity.Lux);
            if (GUI.Button(new Rect(panel.x + 434f * scale, identityY, 176f * scale, 42f * scale), "バスティオン",
                    PrideCourtUiTheme.Button(selectedIdentity == AthleteIdentity.Bastion
                        ? PrideCourtUiTheme.Tone.Yellow : PrideCourtUiTheme.Tone.Neutral, scale, 15)))
                SelectIdentity(AthleteIdentity.Bastion);
            GUI.Label(new Rect(panel.x + 626f * scale, identityY, 300f * scale, 42f * scale),
                $"保存デッキ  {DeckSize}/16", PrideCourtUiTheme.Label(scale, 14, TextAnchor.MiddleLeft,
                    DeckSize == 16 ? PrideCourtUiTheme.Cyan : PrideCourtUiTheme.Magenta));

            Rect hostPanel = new Rect(panel.x + 38f * scale, panel.y + 158f * scale, 410f * scale, 330f * scale);
            PrideCourtUiTheme.DrawPanel(hostPanel, PrideCourtUiTheme.Tone.Cyan, scale, "HOST / コートを開く");
            GUI.Label(new Rect(hostPanel.x + 24f * scale, hostPanel.y + 46f * scale,
                    hostPanel.width - 48f * scale, 58f * scale),
                "この端末が試合を管理します。\nコードは相手が参加するまで有効です。",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper, true));
            string roomCode = lanSession == null ? string.Empty : lanSession.OnlineRoomCode;
            GUI.Label(new Rect(hostPanel.x + 30f * scale, hostPanel.y + 120f * scale,
                    hostPanel.width - 60f * scale, 62f * scale),
                string.IsNullOrEmpty(roomCode) ? "—— —— ——" : roomCode,
                PrideCourtUiTheme.Heading(scale * 0.9f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Yellow));
            GUI.enabled = lanSession != null && lanSession.IsOnlineConfigured && !lanSession.IsBusy &&
                          !lanSession.IsConnected && DeckSize == 16;
            if (GUI.Button(new Rect(hostPanel.x + 58f * scale, hostPanel.y + 202f * scale,
                        hostPanel.width - 116f * scale, 62f * scale), "コートを開く",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 18)))
                lanSession.StartOnlineHosting(selectedIdentity, BuildDeck());
            GUI.enabled = true;

            Rect joinPanel = new Rect(panel.x + 468f * scale, panel.y + 158f * scale, 474f * scale, 330f * scale);
            PrideCourtUiTheme.DrawPanel(joinPanel, PrideCourtUiTheme.Tone.Violet, scale, "JOIN / コートに参加");
            GUI.Label(new Rect(joinPanel.x + 24f * scale, joinPanel.y + 48f * scale,
                    joinPanel.width - 48f * scale, 54f * scale),
                "相手から受け取ったコートコードを入力",
                PrideCourtUiTheme.Label(scale, 14, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));
            Rect codeField = new Rect(joinPanel.x + 56f * scale, joinPanel.y + 124f * scale,
                joinPanel.width - 112f * scale, 58f * scale);
            PrideCourtUiTheme.DrawSolid(codeField, PrideCourtUiTheme.Paper);
            GUIStyle codeStyle = PrideCourtUiTheme.Heading(scale * 0.72f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Ink);
            codeStyle.padding = new RectOffset(10, 10, 5, 5);
            manualOnlineCode = EosRoomCode.Normalize(GUI.TextField(codeField, manualOnlineCode,
                EosRoomCode.Length, codeStyle));
            GUI.enabled = lanSession != null && lanSession.IsOnlineConfigured && !lanSession.IsBusy &&
                          !lanSession.IsConnected && DeckSize == 16 && EosRoomCode.IsValid(manualOnlineCode);
            if (GUI.Button(new Rect(joinPanel.x + 92f * scale, joinPanel.y + 212f * scale,
                        joinPanel.width - 184f * scale, 58f * scale), "コードで参加",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Yellow, scale, 17)))
                lanSession.JoinOnline(manualOnlineCode, selectedIdentity, BuildDeck());
            GUI.enabled = true;

            string status = lanSession == null ? "通信機能を初期化できません" :
                (lanSession.IsBusy || lanSession.IsConnected || lanSession.Role == LanSessionRole.Error
                    ? lanSession.Status : lanSession.OnlineConfigurationStatus);
            Color statusColor = lanSession != null && lanSession.Role == LanSessionRole.Error
                ? PrideCourtUiTheme.Magenta
                : lanSession != null && lanSession.IsOnlineConfigured ? PrideCourtUiTheme.Cyan : PrideCourtUiTheme.Yellow;
            GUI.Label(new Rect(panel.x + 36f * scale, panel.y + 508f * scale,
                    panel.width - 72f * scale, 58f * scale), status,
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, statusColor, true));

            if (lanSession != null && (lanSession.IsBusy || lanSession.IsConnected))
            {
                if (GUI.Button(new Rect(panel.center.x - 110f * scale, panel.yMax - 66f * scale,
                            220f * scale, 42f * scale), "通信をキャンセル",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 14)))
                    lanSession.Disconnect();
            }
            else
            {
                DrawBackButton(panel, scale);
#if UNITY_EDITOR
                if (lanSession != null && !lanSession.IsOnlineConfigured &&
                    GUI.Button(new Rect(panel.xMax - 284f * scale, panel.yMax - 72f * scale,
                            246f * scale, 44f * scale), "EOS設定を開く",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, scale, 15)))
                {
                    if (!UnityEditor.EditorApplication.ExecuteMenuItem("EOS Plugin/EOS Configuration"))
                        Debug.LogWarning("EOS Configuration window could not be opened.");
                }
#endif
            }
        }

        private void DrawLocalNetworkLobby()
        {
            DrawFrontEndBackdrop(0.76f);
            Rect panel = FrontEndPanel(980f, 650f, out float scale);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Yellow, scale, "LOCAL LINK / ローカル通信対戦");
            GUI.Label(new Rect(panel.x + 32f * scale, panel.y + 48f * scale, panel.width - 64f * scale, 38f * scale),
                "同じLANに接続し、片方がコートを開き、もう片方が参加します",
                PrideCourtUiTheme.Label(scale, 15, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));

            float identityY = panel.y + 94f * scale;
            GUI.Label(new Rect(panel.x + 42f * scale, identityY, 220f * scale, 42f * scale), "使用キャラクター",
                PrideCourtUiTheme.Heading(scale * 0.44f, TextAnchor.MiddleLeft, PrideCourtUiTheme.Cyan));
            if (GUI.Button(new Rect(panel.x + 268f * scale, identityY, 154f * scale, 42f * scale), "ルクス",
                    PrideCourtUiTheme.Button(selectedIdentity == AthleteIdentity.Lux ? PrideCourtUiTheme.Tone.Cyan : PrideCourtUiTheme.Tone.Neutral, scale, 15)))
                SelectIdentity(AthleteIdentity.Lux);
            if (GUI.Button(new Rect(panel.x + 434f * scale, identityY, 176f * scale, 42f * scale), "バスティオン",
                    PrideCourtUiTheme.Button(selectedIdentity == AthleteIdentity.Bastion ? PrideCourtUiTheme.Tone.Yellow : PrideCourtUiTheme.Tone.Neutral, scale, 15)))
                SelectIdentity(AthleteIdentity.Bastion);
            GUI.Label(new Rect(panel.x + 626f * scale, identityY, 300f * scale, 42f * scale),
                $"保存デッキ  {DeckSize}/16", PrideCourtUiTheme.Label(scale, 14, TextAnchor.MiddleLeft,
                    DeckSize == 16 ? PrideCourtUiTheme.Cyan : PrideCourtUiTheme.Magenta));

            Rect hostPanel = new Rect(panel.x + 38f * scale, panel.y + 158f * scale, 410f * scale, 330f * scale);
            PrideCourtUiTheme.DrawPanel(hostPanel, PrideCourtUiTheme.Tone.Cyan, scale, "HOST / コートを開く");
            GUI.Label(new Rect(hostPanel.x + 22f * scale, hostPanel.y + 42f * scale, hostPanel.width - 44f * scale, 54f * scale),
                "この端末を試合のホストにします。\n相手には下のIPv4アドレスも伝えられます。",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper, true));
            string addressText = lanSession == null || lanSession.LocalAddresses.Count == 0
                ? "IPv4: 取得できません"
                : "IPv4: " + string.Join(" / ", lanSession.LocalAddresses);
            GUI.Label(new Rect(hostPanel.x + 18f * scale, hostPanel.y + 112f * scale, hostPanel.width - 36f * scale, 58f * scale),
                addressText, PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Yellow, true));
            GUI.enabled = lanSession != null && !lanSession.IsBusy && !lanSession.IsConnected && DeckSize == 16;
            if (GUI.Button(new Rect(hostPanel.x + 58f * scale, hostPanel.y + 194f * scale, hostPanel.width - 116f * scale, 62f * scale),
                    "コートを開く", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 18)))
                lanSession.StartHosting(selectedIdentity, BuildDeck());
            GUI.enabled = true;

            Rect joinPanel = new Rect(panel.x + 468f * scale, panel.y + 158f * scale, 474f * scale, 330f * scale);
            PrideCourtUiTheme.DrawPanel(joinPanel, PrideCourtUiTheme.Tone.Violet, scale, "JOIN / コートに参加");
            GUI.enabled = lanSession != null && !lanSession.IsBusy && !lanSession.IsConnected;
            if (GUI.Button(new Rect(joinPanel.x + 22f * scale, joinPanel.y + 42f * scale, 138f * scale, 40f * scale),
                    "LANを再検索", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, 13)))
                lanSession.RefreshHosts();
            GUI.enabled = true;

            float hostY = joinPanel.y + 92f * scale;
            IReadOnlyList<LanDiscoveredHost> discoveredHosts = lanSession?.Hosts;
            if (discoveredHosts != null && discoveredHosts.Count > 0)
            {
                int visibleCount = Mathf.Min(3, discoveredHosts.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    LanDiscoveredHost host = discoveredHosts[i];
                    GUI.enabled = !lanSession.IsBusy && !lanSession.IsConnected && DeckSize == 16;
                    if (GUI.Button(new Rect(joinPanel.x + 20f * scale, hostY + i * 46f * scale,
                                joinPanel.width - 40f * scale, 39f * scale), host.RoomName + "  /  " + host.Address,
                            PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, scale, 12)))
                        lanSession.Join(host, selectedIdentity, BuildDeck());
                    GUI.enabled = true;
                }
            }
            else
            {
                GUI.Label(new Rect(joinPanel.x + 20f * scale, hostY, joinPanel.width - 40f * scale, 42f * scale),
                    "見つからない場合はIPv4を直接入力", PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
            }

            Rect addressField = new Rect(joinPanel.x + 22f * scale, joinPanel.yMax - 78f * scale,
                250f * scale, 42f * scale);
            PrideCourtUiTheme.DrawSolid(addressField, PrideCourtUiTheme.Paper);
            GUIStyle addressStyle = PrideCourtUiTheme.Label(scale, 15, TextAnchor.MiddleCenter, PrideCourtUiTheme.Ink);
            addressStyle.padding = new RectOffset(8, 8, 4, 4);
            manualLanAddress = GUI.TextField(addressField, manualLanAddress, 15, addressStyle);
            GUI.enabled = lanSession != null && !lanSession.IsBusy && !lanSession.IsConnected && DeckSize == 16;
            if (GUI.Button(new Rect(joinPanel.x + 284f * scale, joinPanel.yMax - 78f * scale, 168f * scale, 42f * scale),
                    "直接参加", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Yellow, scale, 14)))
                lanSession.JoinAddress(manualLanAddress, LanBattleProtocol.GamePort, selectedIdentity, BuildDeck());
            GUI.enabled = true;

            string sessionStatus = lanSession == null ? "通信機能を初期化できません" : lanSession.Status;
            Color statusColor = lanSession != null && lanSession.Role == LanSessionRole.Error
                ? PrideCourtUiTheme.Magenta : PrideCourtUiTheme.Cyan;
            GUI.Label(new Rect(panel.x + 36f * scale, panel.y + 510f * scale, panel.width - 72f * scale, 48f * scale),
                sessionStatus, PrideCourtUiTheme.Label(scale, 14, TextAnchor.MiddleCenter, statusColor, true));

            if (lanSession != null && (lanSession.IsBusy || lanSession.IsConnected))
            {
                if (GUI.Button(new Rect(panel.center.x - 110f * scale, panel.yMax - 66f * scale, 220f * scale, 42f * scale),
                        "通信をキャンセル", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 14)))
                    lanSession.Disconnect();
            }
            else
            {
                DrawBackButton(panel, scale);
            }
        }

        private void DrawFrontEndBackdrop(float darkness)
        {
            DrawOpeningArtwork(new Rect(0f, 0f, Screen.width, Screen.height));
            PrideCourtUiTheme.DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g, PrideCourtUiTheme.Ink.b, darkness));
            PrideCourtUiTheme.DrawSolid(new Rect(0f, 0f, Screen.width, 6f), PrideCourtUiTheme.Cyan);
            PrideCourtUiTheme.DrawSolid(new Rect(0f, Screen.height - 6f, Screen.width, 6f), PrideCourtUiTheme.Magenta);
        }

        private void DrawOpeningArtwork(Rect rect)
        {
            if (openingArtwork != null)
            {
                GUI.DrawTexture(rect, openingArtwork, ScaleMode.ScaleAndCrop);
            }
            else
            {
                PrideCourtUiTheme.DrawSolid(rect, PrideCourtUiTheme.Ink);
            }
        }

        private static void DrawLogo(Rect rect, float scale)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height * 0.44f), "PRIDE",
                PrideCourtUiTheme.Heading(scale * 2.05f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Cyan));
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.31f, rect.width, rect.height * 0.44f), "COURT",
                PrideCourtUiTheme.Heading(scale * 2.05f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Magenta));
            PrideCourtUiTheme.DrawTag(new Rect(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.78f,
                rect.width * 0.56f, 28f * scale), "種族の誇りが、世界を決める", PrideCourtUiTheme.Tone.Violet, scale);
        }

        private static void DrawMenuTitle(Rect panel, float scale, string title)
        {
            GUI.Label(new Rect(panel.x + 30f * scale, panel.y + 50f * scale,
                    panel.width - 60f * scale, 58f * scale), title,
                PrideCourtUiTheme.Heading(scale * 0.78f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));
        }

        private void DrawBackButton(Rect panel, float scale)
        {
            if (GUI.Button(new Rect(panel.x + 38f * scale, panel.yMax - 72f * scale,
                    180f * scale, 44f * scale), "◀  戻る",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 15)))
            {
                ApplyFrontEndAction(FrontEndAction.Back);
            }
        }

        private static Rect FrontEndPanel(float referenceWidth, float referenceHeight, out float scale)
        {
            scale = UiScale();
            Rect safe = GetGuiSafeArea();
            float width = Mathf.Min(referenceWidth * scale, safe.width - 24f);
            float height = Mathf.Min(referenceHeight * scale, safe.height - 24f);
            return new Rect(safe.x + (safe.width - width) * 0.5f,
                safe.y + (safe.height - height) * 0.5f, width, height);
        }

        private static Rect GetGuiSafeArea()
        {
            Rect safe = Screen.safeArea;
            safe.y = Screen.height - safe.yMax;
            return safe;
        }

        private static float UiScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.35f);
        }

        private static Rect Grow(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private void DrawSetup()
        {
            float uiScale = UiScale();
            Rect safe = GetGuiSafeArea();
            float panelWidth = Mathf.Min(1080f * uiScale, safe.width - 24f);
            float panelHeight = Mathf.Min(660f * uiScale, safe.height - 24f);
            Rect panel = new Rect(safe.x + (safe.width - panelWidth) * 0.5f, safe.y + (safe.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            PrideCourtUiTheme.DrawPanel(panel, PrideCourtUiTheme.Tone.Cyan, uiScale);

            GUI.Label(new Rect(panel.x + 34f * uiScale, panel.y + 13f * uiScale, panel.width - 68f * uiScale, 62f * uiScale),
                "プライド・コート", PrideCourtUiTheme.Heading(uiScale * 1.05f, TextAnchor.MiddleLeft, PrideCourtUiTheme.Cyan));
            PrideCourtUiTheme.DrawTag(new Rect(panel.x + 38f * uiScale, panel.y + 70f * uiScale, 298f * uiScale, 25f * uiScale),
                "種族の誇りが、世界を決める", PrideCourtUiTheme.Tone.Magenta, uiScale);

            Rect characterRect = new Rect(panel.x + 32f * uiScale, panel.y + 108f * uiScale, 330f * uiScale, 420f * uiScale);
            DrawCharacterChoice(characterRect, uiScale);
            Rect deckRect = new Rect(panel.x + 386f * uiScale, panel.y + 108f * uiScale, panel.width - 418f * uiScale, 420f * uiScale);
            DrawDeckEditor(deckRect, uiScale);

            string startLabel = DeckSize == 16 ? "コートへ挑む" : $"デッキ {DeckSize}/16";
            GUI.enabled = DeckSize == 16;
            if (GUI.Button(new Rect(panel.x + panel.width - 330f * uiScale, panel.y + panel.height - 86f * uiScale, 292f * uiScale, 54f * uiScale),
                    startLabel, PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, uiScale, 21))) StartMatch();
            GUI.enabled = true;
            GUI.Label(new Rect(panel.x + 38f * uiScale, panel.y + panel.height - 72f * uiScale, 560f * uiScale, 42f * uiScale),
                "全16枚  /  同名カードは3枚まで  /  キャラクターカードは2枚まで",
                PrideCourtUiTheme.Label(uiScale, 13, TextAnchor.MiddleLeft, PrideCourtUiTheme.Muted));
        }

        private void DrawCharacterChoice(Rect rect, float scale)
        {
            PrideCourtUiTheme.DrawPanel(rect, PrideCourtUiTheme.Tone.Magenta, scale, "挑戦者 / 01");
            Rect portrait = new Rect(rect.x + 18f * scale, rect.y + 34f * scale, rect.width - 36f * scale, 235f * scale);
            GUI.Box(new Rect(portrait.x - 3f * scale, portrait.y - 3f * scale, portrait.width + 6f * scale, portrait.height + 6f * scale),
                GUIContent.none, PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Magenta, scale));
            if (characterAtlas != null)
            {
                Rect uv = selectedIdentity == AthleteIdentity.Lux ? new Rect(0f, 0f, 0.25f, 1f) : new Rect(0.5f, 0f, 0.25f, 1f);
                GUI.DrawTextureWithTexCoords(portrait, characterAtlas, uv, true);
            }
            else GUI.Box(portrait, selectedIdentity == AthleteIdentity.Lux ? "ルクス" : "バスティオン",
                PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Cyan, scale));

            if (GUI.Button(new Rect(rect.x + 18f * scale, rect.yMax - 128f * scale, 130f * scale, 42f * scale),
                    "◀  ルクス", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, 14))) SelectIdentity(AthleteIdentity.Lux);
            if (GUI.Button(new Rect(rect.xMax - 148f * scale, rect.yMax - 128f * scale, 130f * scale, 42f * scale),
                    "バスティオン  ▶", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Yellow, scale, 13))) SelectIdentity(AthleteIdentity.Bastion);
            string trait = selectedIdentity == AthleteIdentity.Lux
                ? "俊敏なキツネ  •  幻影の絆\n素早い加速 / 正確な立て直し"
                : "剛力のロボット  •  重力駆動\n広い守備範囲 / 重いプレッシャー";
            GUI.Label(new Rect(rect.x + 18f * scale, rect.yMax - 76f * scale, rect.width - 36f * scale, 58f * scale), trait,
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper, true));
        }

        private void DrawDeckEditor(Rect rect, float scale)
        {
            PrideCourtUiTheme.DrawPanel(rect, PrideCourtUiTheme.Tone.Violet, scale, "デッキ編成 / " + DeckSize + " / 16");
            Rect viewport = new Rect(rect.x + 12f * scale, rect.y + 30f * scale, rect.width - 24f * scale, rect.height - 42f * scale);
            Rect content = new Rect(0f, 0f, viewport.width - 18f, 10f * 61f * scale);
            scroll = GUI.BeginScrollView(viewport, scroll, content);
            int row = 0;
            foreach (CardId card in CardCatalog.All)
            {
                if (!CardCatalog.IsAllowedFor(card, selectedIdentity)) continue;
                CardDefinition definition = CardCatalog.Get(card);
                float y = row * 58f * scale;
                Color accent = definition.IsCharacterCard ? PrideCourtUiTheme.Cyan : CategoryColor(definition.Category);
                GUI.Box(new Rect(2f, y + 2f, content.width - 4f, 50f * scale), GUIContent.none,
                    PrideCourtUiTheme.CardPanel(accent, scale));
                PrideCourtUiTheme.DrawSolid(new Rect(8f * scale, y + 8f * scale, 4f * scale, 38f * scale), accent);
                GUI.Label(new Rect(18f * scale, y + 5f * scale, content.width - 184f * scale, 23f * scale), definition.DisplayName,
                    PrideCourtUiTheme.Heading(scale * 0.58f, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
                GUI.Label(new Rect(18f * scale, y + 27f * scale, content.width - 184f * scale, 18f * scale), Describe(card),
                    PrideCourtUiTheme.Label(scale, 11, TextAnchor.MiddleLeft, PrideCourtUiTheme.Muted));
                int count = deckCounts.TryGetValue(card, out int value) ? value : 0;
                GUI.enabled = count > 0;
                if (GUI.Button(new Rect(content.width - 154f * scale, y + 9f * scale, 38f * scale, 34f * scale), "−",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale))) ChangeCount(card, -1);
                GUI.enabled = count < (definition.IsCharacterCard ? 2 : 3) && DeckSize < 16;
                if (GUI.Button(new Rect(content.width - 48f * scale, y + 9f * scale, 38f * scale, 34f * scale), "+",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale))) ChangeCount(card, 1);
                GUI.enabled = true;
                GUI.Label(new Rect(content.width - 108f * scale, y + 10f * scale, 54f * scale, 32f * scale), count.ToString(),
                    PrideCourtUiTheme.Heading(scale * 0.63f, TextAnchor.MiddleCenter, accent));
                row++;
            }
            GUI.EndScrollView();
        }

        private void DrawMatchOver()
        {
            bool playerWon = match.Score.TryGetWinner(out CourtSide winner) && winner == match.LocalPlayerSide;
            Rect safe = GetGuiSafeArea();
            Rect panel = new Rect(safe.center.x - 250f, safe.center.y - 130f, 500f, 260f);
            PrideCourtUiTheme.Tone resultTone = playerWon ? PrideCourtUiTheme.Tone.Cyan : PrideCourtUiTheme.Tone.Yellow;
            PrideCourtUiTheme.DrawPanel(panel, resultTone, 1f, "試合終了");
            GUI.Label(new Rect(panel.x + 24f, panel.y + 28f, panel.width - 48f, 62f), playerWon ? "誇りを示した" : "誇りは折れない",
                PrideCourtUiTheme.Heading(1.16f, TextAnchor.MiddleCenter, PrideCourtUiTheme.ToneColor(resultTone)));
            string scoreText = match.LocalPlayerSide == CourtSide.Near
                ? $"{match.Score.NearPoints}  —  {match.Score.FarPoints}"
                : $"{match.Score.FarPoints}  —  {match.Score.NearPoints}";
            GUI.Label(new Rect(panel.x + 24f, panel.y + 92f, panel.width - 48f, 42f), scoreText,
                PrideCourtUiTheme.Heading(1.05f, TextAnchor.MiddleCenter));
            if (lanSession != null && lanSession.IsConnected)
            {
                GUI.enabled = lanSession.IsHost;
                if (GUI.Button(new Rect(panel.x + 42f, panel.y + 160f, 190f, 52f),
                        lanSession.IsHost ? "再戦" : "ホストの再戦待ち",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, 1f, 14))) lanSession.StartRematch();
                GUI.enabled = true;
                if (GUI.Button(new Rect(panel.x + 268f, panel.y + 160f, 190f, 52f), "通信対戦を終了",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, 1f, 14))) ReturnToSetup();
            }
            else
            {
                if (GUI.Button(new Rect(panel.x + 42f, panel.y + 160f, 190f, 52f), "再戦",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, 1f, 20))) StartMatch();
                if (GUI.Button(new Rect(panel.x + 268f, panel.y + 160f, 190f, 52f), "デッキ・キャラクター",
                        PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, 1f, 14))) ReturnToSetup();
            }
        }

        private void ReturnToSetup()
        {
            if (lanSession != null && (lanSession.IsConnected || lanSession.IsBusy))
            {
                bool wasOnline = lanSession.IsOnlineSession;
                lanSession.Disconnect();
                ApplyFrontEndAction(wasOnline ? FrontEndAction.ReturnToOnlineLobby : FrontEndAction.ReturnToLocalLobby);
                return;
            }
            match.ReturnToSetup();
            ApplyFrontEndAction(FrontEndAction.ReturnToSetup);
        }

        private void StartMatch()
        {
            if (DeckSize != 16) return;
            lanSession?.PrepareSolo();
            AthleteIdentity opponent = selectedIdentity == AthleteIdentity.Lux ? AthleteIdentity.Bastion : AthleteIdentity.Lux;
            player.SetIdentity(selectedIdentity);
            cpu.SetIdentity(opponent);
            player.GetComponent<CardLoadoutController>().RebuildDeck(BuildDeck());
            cpu.GetComponent<CardLoadoutController>().RebuildDeck(CardCatalog.BuildPreset(opponent));
            SaveDeck();
            match.StartNewMatch();
        }

        private void SelectIdentity(AthleteIdentity identity)
        {
            if (selectedIdentity == identity) return;
            selectedIdentity = identity;
            PlayerPrefs.SetInt("PrideCourt.PlayerIdentity", (int)identity);
            LoadDeck(identity);
        }

        private void LoadDeck(AthleteIdentity identity)
        {
            deckCounts.Clear();
            foreach (CardId card in CardCatalog.BuildPreset(identity))
            {
                deckCounts.TryGetValue(card, out int count);
                deckCounts[card] = count + 1;
            }
            foreach (CardId card in CardCatalog.All)
            {
                if (!CardCatalog.IsAllowedFor(card, identity)) continue;
                string key = DeckKey(identity, card);
                if (PlayerPrefs.HasKey(key)) deckCounts[card] = PlayerPrefs.GetInt(key);
            }
            if (DeckSize != 16)
            {
                deckCounts.Clear();
                foreach (CardId card in CardCatalog.BuildPreset(identity))
                {
                    deckCounts.TryGetValue(card, out int count);
                    deckCounts[card] = count + 1;
                }
            }
        }

        private void SaveDeck()
        {
            PlayerPrefs.SetInt("PrideCourt.PlayerIdentity", (int)selectedIdentity);
            foreach (CardId card in CardCatalog.All)
                if (CardCatalog.IsAllowedFor(card, selectedIdentity)) PlayerPrefs.SetInt(DeckKey(selectedIdentity, card), deckCounts.TryGetValue(card, out int count) ? count : 0);
            PlayerPrefs.Save();
        }

        private List<CardId> BuildDeck()
        {
            List<CardId> cards = new List<CardId>(16);
            foreach (CardId card in CardCatalog.All)
                if (CardCatalog.IsAllowedFor(card, selectedIdentity) && deckCounts.TryGetValue(card, out int count))
                    for (int i = 0; i < count; i++) cards.Add(card);
            return cards;
        }

        private void ChangeCount(CardId card, int delta)
        {
            deckCounts.TryGetValue(card, out int count);
            int max = CardCatalog.Get(card).IsCharacterCard ? 2 : 3;
            deckCounts[card] = Mathf.Clamp(count + delta, 0, max);
        }

        private int DeckSize
        {
            get { int total = 0; foreach (int count in deckCounts.Values) total += count; return total; }
        }

        private static string DeckKey(AthleteIdentity identity, CardId card) => $"PrideCourt.Deck.{identity}.{card}";
        private static Color CategoryColor(CardCategory category) => category switch
        {
            CardCategory.PersonalBuff => PrideCourtUiTheme.Cyan,
            CardCategory.Instant => PrideCourtUiTheme.Magenta,
            CardCategory.NextShot => PrideCourtUiTheme.Violet,
            _ => PrideCourtUiTheme.Yellow
        };

        private static string Describe(CardId card) => card switch
        {
            CardId.AccelStep => "加速力アップ • 5秒", CardId.EcoRun => "ダッシュ消費半減 • 6秒",
            CardId.RecoveryPulse => "スタミナを35回復", CardId.SpinBoost => "次のショットのスピン強化",
            CardId.GaugeCharge => "スペシャルゲージ +15", CardId.GripCourt => "コート上の加速力アップ",
            CardId.SlipCourt => "コート上の加速力ダウン", CardId.HighBounce => "このラリーのバウンド上昇",
            CardId.FlashStep => "ルクスの加速力 +30%", CardId.TailFeint => "ルクスの次のBショットがフェイント",
            CardId.RailBoost => "バスティオンの移動速度 +20%", _ => "バスティオンの強打消費半減"
        };

    }
}
