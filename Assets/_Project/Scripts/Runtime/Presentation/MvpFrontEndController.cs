using System.Collections.Generic;
using PrideCourt.Cards;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Networking;
using UnityEngine;

namespace PrideCourt.Presentation
{
    // Hallmark · pre-emit critique: P5 H5 E4 S5 R5 V5
    // Hallmark · single-screen redesign · playful Sport · pop-punk versus poster
    public sealed class MvpFrontEndController : MonoBehaviour
    {
        public const string OpeningArtworkResourcePath = "Stadium/FantasyStadiumMural";

        private const float OpeningDurationSeconds = 8.2f;
        private const float OpeningPortraitStartSeconds = 1.35f;
        private const float OpeningPortraitExitSeconds = 4.72f;
        private const float OpeningLogoStartSeconds = 5.08f;

        [SerializeField] private TennisMatchController match;
        [SerializeField] private TennisAthleteController player;
        [SerializeField] private TennisAthleteController cpu;
        [SerializeField] private LanBattleSessionController lanSession;

        private readonly Dictionary<CardId, int> deckCounts = new Dictionary<CardId, int>();
        private readonly PrideCourtFrontEndFlow frontEndFlow = new PrideCourtFrontEndFlow();
        private readonly PrideCourtSetupFlow setupFlow = new PrideCourtSetupFlow();
        private readonly Dictionary<CardId, Texture2D> setupCardArt = new Dictionary<CardId, Texture2D>();
        private AthleteIdentity selectedIdentity;
        private CardId focusedCard;
        private bool hasFocusedCard;
        private Texture2D characterAtlas;
        private readonly Dictionary<AthleteIdentity, Texture2D> characterPortraits =
            new Dictionary<AthleteIdentity, Texture2D>();
        private Texture2D openingArtwork;
        private float openingStartedAt;
        private float setupStepStartedAt;
        private float setupFeedbackUntil;
        private CardId setupFeedbackCard;
        private float nextSetupNavigationAt;
        private bool setupNavigationHeld;
#if UNITY_EDITOR
        private float openingPreviewElapsed = -1f;
#endif
        private string manualLanAddress = "192.168.";
        private string manualOnlineCode = string.Empty;
        private int selectedLocalGamesToWin = MatchScore.DefaultGamesToWin;

        public FrontEndScreen CurrentScreen => frontEndFlow.Screen;
        public MultiplayerEntry PendingMultiplayerEntry => frontEndFlow.PendingMultiplayerEntry;
        public SetupStep CurrentSetupStep => setupFlow.Step;
        public int SelectedLocalGamesToWin => selectedLocalGamesToWin;

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
            selectedIdentity = (AthleteIdentity)Mathf.Clamp(PlayerPrefs.GetInt("PrideCourt.PlayerIdentity", 0),
                0, AthleteCatalog.All.Count - 1);
            characterAtlas = Resources.Load<Texture2D>("CharacterCardAtlas");
            foreach (AthleteIdentity identity in AthleteCatalog.All)
            {
                Texture2D portrait = Resources.Load<Texture2D>("CharacterPortraits/" + identity);
                if (portrait != null) characterPortraits[identity] = portrait;
            }
            foreach (CardId card in CardCatalog.All)
            {
                Texture2D texture = Resources.Load<Texture2D>("CardArt/" + card);
                if (texture != null) setupCardArt[card] = texture;
            }
            openingArtwork = Resources.Load<Texture2D>(OpeningArtworkResourcePath);
            openingStartedAt = Time.unscaledTime;
            setupStepStartedAt = Time.unscaledTime;
            LoadDeck(selectedIdentity);
            FocusFirstAllowedCard();
        }

        private void Update()
        {
            if (match == null) return;
            if (!match.HasStarted)
            {
                if (frontEndFlow.Screen == FrontEndScreen.Opening &&
                    (OpeningElapsed >= OpeningDurationSeconds || OpeningSkipPressed()))
                {
                    ApplyFrontEndAction(FrontEndAction.FinishOpening);
                    return;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                    UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1))
                {
                    if (frontEndFlow.Screen != FrontEndScreen.Setup ||
                        !ApplySetupAction(SetupAction.Back))
                    {
                        ApplyFrontEndAction(FrontEndAction.Back);
                    }
                    return;
                }

                HandleFrontEndKeyboard();
                return;
            }

            if (match.Phase == MatchPhase.MatchOver && UnityEngine.Input.GetKeyDown(KeyCode.Return)) StartMatch();
        }

        private void OnGUI()
        {
            if (HudSettingsController.IsAnyOpen) return;
            if (match == null || player == null || cpu == null) return;
            if (!match.HasStarted)
            {
                int previousDepth = GUI.depth;
                GUI.depth = -1000;
                try
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
                }
                finally
                {
                    GUI.depth = previousDepth;
                }
                return;
            }

            if (match.Phase == MatchPhase.MatchOver) DrawMatchOver();
        }

        public bool ApplyFrontEndAction(FrontEndAction action)
        {
            FrontEndScreen previousScreen = frontEndFlow.Screen;
            if (action == FrontEndAction.Back && lanSession != null &&
                (frontEndFlow.Screen == FrontEndScreen.LocalNetworkLobby ||
                 frontEndFlow.Screen == FrontEndScreen.OnlineNetworkLobby) &&
                (lanSession.IsBusy || lanSession.IsConnected))
            {
                lanSession.Disconnect();
            }
            bool changed = frontEndFlow.Apply(action);
            if (changed && frontEndFlow.Screen == FrontEndScreen.Setup &&
                previousScreen != FrontEndScreen.Setup)
            {
                ResetSetupFlow();
            }
            if (changed && frontEndFlow.Screen == FrontEndScreen.Opening)
            {
                openingStartedAt = Time.unscaledTime;
#if UNITY_EDITOR
                openingPreviewElapsed = -1f;
#endif
            }
            if (changed && frontEndFlow.Screen == FrontEndScreen.LocalNetworkLobby)
            {
                lanSession?.RefreshHosts();
            }
            return changed;
        }

        public bool ApplySetupAction(SetupAction action)
        {
            bool changed = setupFlow.Apply(action);
            if (!changed) return false;
            setupStepStartedAt = Time.unscaledTime;
            if (setupFlow.Step == SetupStep.CardLoadout) FocusFirstAllowedCard();
            return true;
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
            if (frontEndFlow.Screen == FrontEndScreen.LocalNetworkLobby)
            {
                for (int gamesToWin = MatchScore.MinimumGamesToWin;
                     gamesToWin <= MatchScore.MaximumGamesToWin;
                     gamesToWin++)
                {
                    if (UnityEngine.Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + gamesToWin)) ||
                        UnityEngine.Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + gamesToWin)))
                    {
                        TrySelectLocalGamesToWin(gamesToWin);
                        return;
                    }
                }
            }

            if (frontEndFlow.Screen == FrontEndScreen.Setup)
            {
                if (HandleSetupNavigation()) return;
                if (setupFlow.Step != SetupStep.CharacterSelect) return;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
                    SelectIdentity(AthleteIdentity.Lux);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
                    SelectIdentity(AthleteIdentity.Bastion);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3))
                    SelectIdentity(AthleteIdentity.Lucia);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
                    SelectIdentity(AthleteIdentity.Charlotte);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
                    SelectIdentity(AthleteIdentity.Zephyr);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad6))
                    SelectIdentity(AthleteIdentity.Poko);
                return;
            }

            if (frontEndFlow.Screen == FrontEndScreen.Title &&
                (UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                 UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0)))
            {
                ApplyFrontEndAction(FrontEndAction.OpenModeSelect);
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

        private float OpeningElapsed
        {
            get
            {
#if UNITY_EDITOR
                if (openingPreviewElapsed >= 0f) return openingPreviewElapsed;
#endif
                return Time.unscaledTime - openingStartedAt;
            }
        }

#if UNITY_EDITOR
        public void SetOpeningElapsedForValidation(float elapsed)
        {
            openingPreviewElapsed = Mathf.Clamp(elapsed, 0f, OpeningDurationSeconds - 0.01f);
        }
#endif

        private void DrawOpeningMovie()
        {
            float elapsed = OpeningElapsed;
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect safe = GetGuiSafeArea();
            float scale = UiScale();
            float reveal = EaseOutCubic(Phase(elapsed, 0f, 0.52f));
            float logoReveal = EaseOutCubic(Phase(elapsed, OpeningLogoStartSeconds, 0.44f));
            float backgroundDrive = EaseOutCubic(Phase(elapsed, 0.18f, 3.9f)) *
                                    (1f - EaseOutCubic(Phase(elapsed, 4.58f, 0.62f)));
            float zoom = backgroundDrive * 54f * scale;
            float drift = backgroundDrive * 18f * scale;
            PrideCourtUiTheme.DrawSolid(full, PrideCourtUiTheme.Ink);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, reveal);
            DrawOpeningArtwork(new Rect(-zoom - drift, -zoom * 0.55f,
                full.width + zoom * 2f, full.height + zoom * 1.1f));
            GUI.color = previous;
            float darkness = Mathf.Lerp(0.2f, 0.56f, logoReveal);
            PrideCourtUiTheme.DrawSolid(full, new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g,
                PrideCourtUiTheme.Ink.b, darkness));

            DrawOpeningKickoffWipes(full, scale, elapsed);

            if (characterAtlas != null)
            {
                DrawOpeningVersusPoster(safe, scale, elapsed);
            }

            DrawOpeningCopy(safe, scale, elapsed);

            if (logoReveal > 0f)
            {
                Rect settledLogo = new Rect(safe.x + safe.width * 0.15f, safe.y + safe.height * 0.13f,
                    safe.width * 0.7f, safe.height * 0.38f);
                float logoScale = Mathf.Lerp(1.16f, 1f, logoReveal);
                DrawLogo(ScaleAroundCenter(settledLogo, logoScale), scale * logoScale, logoReveal);

                Color cyanEdge = PrideCourtUiTheme.Cyan;
                cyanEdge.a = logoReveal;
                Color magentaEdge = PrideCourtUiTheme.Magenta;
                magentaEdge.a = logoReveal;
                PrideCourtUiTheme.DrawSolid(new Rect(0f, 0f, Screen.width, 6f * scale), cyanEdge);
                PrideCourtUiTheme.DrawSolid(new Rect(0f, Screen.height - 6f * scale,
                    Screen.width, 6f * scale), magentaEdge);
            }

            float flash = 1f - Mathf.Clamp01(Mathf.Abs(elapsed - OpeningLogoStartSeconds) / 0.12f);
            if (flash > 0f)
            {
                PrideCourtUiTheme.DrawSolid(full, new Color(PrideCourtUiTheme.Paper.r,
                    PrideCourtUiTheme.Paper.g, PrideCourtUiTheme.Paper.b, flash * 0.42f));
            }

            float promptReveal = EaseOutCubic(Phase(elapsed, 0.48f, 0.28f));
            Rect prompt = new Rect(safe.center.x - 190f * scale, safe.yMax - 52f * scale,
                380f * scale, 34f * scale);
            PrideCourtUiTheme.DrawSolid(prompt, new Color(PrideCourtUiTheme.Ink.r,
                PrideCourtUiTheme.Ink.g, PrideCourtUiTheme.Ink.b, promptReveal * 0.76f));
            PrideCourtUiTheme.DrawSolid(new Rect(prompt.x, prompt.y, 42f * scale, 3f * scale),
                WithAlpha(PrideCourtUiTheme.Cyan, promptReveal));
            PrideCourtUiTheme.DrawSolid(new Rect(prompt.xMax - 42f * scale, prompt.yMax - 3f * scale,
                42f * scale, 3f * scale), WithAlpha(PrideCourtUiTheme.Magenta, promptReveal));
            GUI.Label(prompt,
                "PRESS ANY BUTTON  /  TAP TO SKIP",
                PrideCourtUiTheme.Label(scale, 15, TextAnchor.MiddleCenter,
                    new Color(PrideCourtUiTheme.Paper.r, PrideCourtUiTheme.Paper.g,
                        PrideCourtUiTheme.Paper.b, promptReveal * 0.94f)));
        }

        private static void DrawOpeningKickoffWipes(Rect full, float scale, float elapsed)
        {
            float sweep = EaseOutCubic(Phase(elapsed, 0.04f, 0.62f));
            float fade = 1f - EaseOutCubic(Phase(elapsed, 0.58f, 0.28f));
            float alpha = sweep * fade;
            if (alpha <= 0f) return;

            float bandWidth = full.width * 1.38f;
            float bandHeight = Mathf.Max(72f * scale, full.height * 0.13f);
            float cyanX = Mathf.Lerp(-bandWidth, full.width * 0.38f, sweep);
            float magentaX = Mathf.Lerp(full.width, -full.width * 0.36f, sweep);
            DrawRotatedSolid(new Rect(cyanX, full.height * 0.2f, bandWidth, bandHeight),
                WithAlpha(PrideCourtUiTheme.Cyan, alpha * 0.88f), -11f);
            DrawRotatedSolid(new Rect(magentaX, full.height * 0.62f, bandWidth, bandHeight * 0.82f),
                WithAlpha(PrideCourtUiTheme.Magenta, alpha * 0.82f), -11f);
            DrawRotatedSolid(new Rect(cyanX - full.width * 0.08f, full.height * 0.47f,
                    bandWidth * 0.55f, 12f * scale),
                WithAlpha(PrideCourtUiTheme.Yellow, alpha), -11f);
        }

        private void DrawOpeningVersusPoster(Rect safe, float scale, float elapsed)
        {
            float enter = EaseOutCubic(Phase(elapsed, OpeningPortraitStartSeconds, 0.58f));
            float exit = EaseInCubic(Phase(elapsed, OpeningPortraitExitSeconds, 0.42f));
            float alpha = enter * (1f - exit);
            if (alpha <= 0f) return;

            float luxWidth = Mathf.Min(390f * scale, safe.width * 0.37f);
            float bastionWidth = Mathf.Min(350f * scale, safe.width * 0.33f);
            float portraitHeight = Mathf.Min(560f * scale, safe.height * 0.73f);
            float luxTargetX = safe.x + safe.width * 0.025f;
            float bastionTargetX = safe.xMax - safe.width * 0.025f - bastionWidth;
            float luxX = Mathf.Lerp(-luxWidth * 1.08f, luxTargetX, enter) - exit * safe.width * 0.12f;
            float bastionX = Mathf.Lerp(Screen.width + bastionWidth * 0.08f, bastionTargetX, enter) +
                              exit * safe.width * 0.12f;
            Rect lux = new Rect(luxX, safe.y + safe.height * 0.17f, luxWidth, portraitHeight);
            Rect bastion = new Rect(bastionX, safe.y + safe.height * 0.21f, bastionWidth, portraitHeight * 0.94f);

            DrawOpeningPortrait(lux, new Rect(0f, 0f, 0.25f, 1f), PrideCourtUiTheme.Cyan,
                -3.5f, alpha, scale);
            DrawOpeningPortrait(bastion, new Rect(0.5f, 0f, 0.25f, 1f), PrideCourtUiTheme.Yellow,
                3.5f, alpha, scale);

            float versusReveal = EaseOutCubic(Phase(elapsed, 2.05f, 0.28f)) * (1f - exit);
            if (versusReveal <= 0f) return;

            float badgeSize = Mathf.Min(86f * scale, safe.height * 0.12f);
            Rect badge = new Rect(safe.center.x - badgeSize * 0.5f,
                safe.y + safe.height * 0.48f - badgeSize * 0.5f, badgeSize, badgeSize);
            DrawRotatedSolid(badge, WithAlpha(PrideCourtUiTheme.Magenta, versusReveal), 45f);
            DrawRotatedSolid(ScaleAroundCenter(badge, 0.78f),
                WithAlpha(PrideCourtUiTheme.Ink, versusReveal), 45f);
            GUI.Label(badge, "VS", PrideCourtUiTheme.Heading(scale * 0.82f,
                TextAnchor.MiddleCenter, WithAlpha(PrideCourtUiTheme.Paper, versusReveal)));

            float slashWidth = Mathf.Min(170f * scale, safe.width * 0.14f);
            DrawRotatedSolid(new Rect(badge.x - slashWidth * 0.82f, badge.center.y - 4f * scale,
                    slashWidth, 7f * scale), WithAlpha(PrideCourtUiTheme.Cyan, versusReveal), -18f);
            DrawRotatedSolid(new Rect(badge.xMax - slashWidth * 0.18f, badge.center.y - 4f * scale,
                    slashWidth, 7f * scale), WithAlpha(PrideCourtUiTheme.Yellow, versusReveal), -18f);
        }

        private void DrawOpeningPortrait(Rect rect, Rect uv, Color accent, float angle, float alpha, float scale)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            Rect backing = Grow(rect, 7f * scale);
            PrideCourtUiTheme.DrawSolid(backing, WithAlpha(PrideCourtUiTheme.Ink, alpha * 0.94f));
            PrideCourtUiTheme.DrawSolid(new Rect(backing.x, backing.y, 7f * scale, backing.height),
                WithAlpha(accent, alpha));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(rect, characterAtlas, uv, true);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private static void DrawOpeningCopy(Rect safe, float scale, float elapsed)
        {
            DrawOpeningLine(new Rect(safe.x + safe.width * 0.08f, safe.y + safe.height * 0.075f,
                    safe.width * 0.56f, 54f * scale), "世界を決めるのは、力か。", PrideCourtUiTheme.Cyan,
                TextAnchor.MiddleLeft, elapsed, 0.78f, 1.58f, scale);
            DrawOpeningLine(new Rect(safe.x + safe.width * 0.54f, safe.y + safe.height * 0.08f,
                    safe.width * 0.38f, 54f * scale), "速さか。", PrideCourtUiTheme.Yellow,
                TextAnchor.MiddleRight, elapsed, 1.72f, 2.66f, scale);
            DrawOpeningLine(new Rect(safe.x + safe.width * 0.24f, safe.y + safe.height * 0.065f,
                    safe.width * 0.58f, 62f * scale), "譲れない誇りか。", PrideCourtUiTheme.Magenta,
                TextAnchor.MiddleCenter, elapsed, 3.02f, 4.52f, scale * 1.08f);
        }

        private static void DrawOpeningLine(Rect rect, string text, Color accent, TextAnchor alignment,
            float elapsed, float start, float end, float scale)
        {
            float enter = EaseOutCubic(Phase(elapsed, start, 0.18f));
            float exit = EaseInCubic(Phase(elapsed, end - 0.2f, 0.2f));
            float alpha = enter * (1f - exit);
            if (alpha <= 0f) return;

            float ruleWidth = Mathf.Min(rect.width * 0.34f, 180f * scale);
            float ruleX = alignment == TextAnchor.MiddleRight ? rect.xMax - ruleWidth : rect.x;
            if (alignment == TextAnchor.MiddleCenter) ruleX = rect.center.x - ruleWidth * 0.5f;
            PrideCourtUiTheme.DrawSolid(new Rect(ruleX, rect.yMax - 4f * scale, ruleWidth, 4f * scale),
                WithAlpha(accent, alpha));
            GUI.Label(rect, text, PrideCourtUiTheme.Heading(scale * 0.8f, alignment,
                WithAlpha(PrideCourtUiTheme.Paper, alpha)));
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
            DrawLobbyLoadoutSummary(new Rect(panel.x + 42f * scale, identityY,
                panel.width - 84f * scale, 42f * scale), scale);

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
            DrawLobbyLoadoutSummary(new Rect(panel.x + 42f * scale, identityY,
                panel.width - 84f * scale, 42f * scale), scale);

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
            DrawLocalGamesToWin(hostPanel, scale);
            GUI.enabled = lanSession != null && !lanSession.IsBusy && !lanSession.IsConnected && DeckSize == 16;
            if (GUI.Button(new Rect(hostPanel.x + 58f * scale, hostPanel.y + 252f * scale, hostPanel.width - 116f * scale, 58f * scale),
                    selectedLocalGamesToWin + "ゲーム先取で開く",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 16)))
                lanSession.StartHosting(selectedIdentity, BuildDeck(), selectedLocalGamesToWin);
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
                                joinPanel.width - 40f * scale, 39f * scale),
                            host.RoomName + "  /  " + host.GamesToWin + "ゲーム先取  /  " + host.Address,
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

        private static void DrawLogo(Rect rect, float scale, float alpha = 1f)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height * 0.44f), "PRIDE",
                PrideCourtUiTheme.Heading(scale * 2.05f, TextAnchor.MiddleCenter,
                    WithAlpha(PrideCourtUiTheme.Cyan, alpha)));
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.31f, rect.width, rect.height * 0.44f), "COURT",
                PrideCourtUiTheme.Heading(scale * 2.05f, TextAnchor.MiddleCenter,
                    WithAlpha(PrideCourtUiTheme.Magenta, alpha)));
            Rect tag = new Rect(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.78f,
                rect.width * 0.56f, 28f * scale);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(tag, GUIContent.none, PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Violet, scale));
            GUI.color = previous;
            GUI.Label(tag, "種族の誇りが、世界を決める",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter,
                    WithAlpha(PrideCourtUiTheme.Paper, alpha)));
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

        private static Rect ScaleAroundCenter(Rect rect, float scale)
        {
            float width = rect.width * scale;
            float height = rect.height * scale;
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        private static float Phase(float elapsed, float start, float duration)
        {
            return Mathf.Clamp01((elapsed - start) / duration);
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void DrawRotatedSolid(Rect rect, Color color, float degrees)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, rect.center);
            PrideCourtUiTheme.DrawSolid(rect, color);
            GUI.matrix = previous;
        }

        private void DrawSetup()
        {
            DrawFrontEndBackdrop(0.8f);
            Rect panel = FrontEndPanel(1120f, 660f, out float scale);
            PrideCourtUiTheme.DrawPanel(panel, IdentityTone(selectedIdentity), scale);
            DrawSetupHeader(panel, scale);

            float reveal = EaseOutCubic(Phase(Time.unscaledTime, setupStepStartedAt, 0.24f));
            Rect content = new Rect(panel.x + (1f - reveal) * 28f * scale, panel.y,
                panel.width, panel.height);
            if (setupFlow.Step == SetupStep.CharacterSelect)
                DrawCharacterSelection(content, scale);
            else
                DrawCardLoadoutSelection(content, scale);
        }

        private void DrawSetupHeader(Rect panel, float scale)
        {
            GUI.Label(new Rect(panel.x + 32f * scale, panel.y + 10f * scale,
                    430f * scale, 58f * scale), "PRIDE COURT",
                PrideCourtUiTheme.Heading(scale * 0.9f, TextAnchor.MiddleLeft, PrideCourtUiTheme.Cyan));
            GUI.Label(new Rect(panel.x + 34f * scale, panel.y + 54f * scale,
                    430f * scale, 26f * scale), "選手の誇りと、コートへ持ち込む戦術を決める",
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleLeft, PrideCourtUiTheme.Muted));

            Rect characterStep = new Rect(panel.xMax - 424f * scale, panel.y + 22f * scale,
                190f * scale, 34f * scale);
            Rect cardStep = new Rect(panel.xMax - 222f * scale, panel.y + 22f * scale,
                190f * scale, 34f * scale);
            PrideCourtUiTheme.DrawTag(characterStep, "01  CHARACTER",
                setupFlow.Step == SetupStep.CharacterSelect
                    ? IdentityTone(selectedIdentity)
                    : PrideCourtUiTheme.Tone.Neutral, scale);
            PrideCourtUiTheme.DrawTag(cardStep, "02  CARD LOADOUT",
                setupFlow.Step == SetupStep.CardLoadout
                    ? PrideCourtUiTheme.Tone.Magenta
                    : PrideCourtUiTheme.Tone.Neutral, scale);
        }

        private void DrawCharacterSelection(Rect panel, float scale)
        {
            Rect hero = new Rect(panel.x + 28f * scale, panel.y + 94f * scale,
                372f * scale, 472f * scale);
            Rect roster = new Rect(hero.xMax + 20f * scale, hero.y,
                panel.xMax - hero.xMax - 48f * scale, hero.height);
            DrawSelectedCharacterHero(hero, scale);
            DrawCharacterRoster(roster, scale);

            Rect back = new Rect(panel.x + 30f * scale, panel.yMax - 72f * scale,
                190f * scale, 48f * scale);
            if (GUI.Button(back, "戻る", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 15)))
                ApplyFrontEndAction(FrontEndAction.Back);

            Rect confirm = new Rect(panel.xMax - 322f * scale, panel.yMax - 76f * scale,
                292f * scale, 54f * scale);
            if (GUI.Button(confirm, "この選手で挑む  ▶",
                    PrideCourtUiTheme.Button(IdentityTone(selectedIdentity), scale, 19)))
            {
                ApplySetupAction(SetupAction.ConfirmCharacter);
            }
            GUI.Label(new Rect(back.xMax + 18f * scale, back.y, confirm.x - back.xMax - 36f * scale,
                    back.height), "方向キー / スティックで選択   •   決定で次へ",
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
        }

        private void DrawSelectedCharacterHero(Rect rect, float scale)
        {
            AthleteDefinition selected = AthleteCatalog.Get(selectedIdentity);
            PrideCourtUiTheme.Tone tone = IdentityTone(selectedIdentity);
            Color accent = PrideCourtUiTheme.ToneColor(tone);
            PrideCourtUiTheme.DrawPanel(rect, tone, scale,
                "P1  CHALLENGER / " + (IdentityIndex(selectedIdentity) + 1).ToString("00"));

            Rect portrait = new Rect(rect.x + 16f * scale, rect.y + 34f * scale,
                rect.width - 32f * scale, 224f * scale);
            GUI.Box(Grow(portrait, 3f * scale), GUIContent.none,
                PrideCourtUiTheme.CardPanel(accent, scale));
            DrawCharacterPortrait(portrait, selectedIdentity, scale);
            PrideCourtUiTheme.DrawSolid(new Rect(portrait.x, portrait.yMax - 5f * scale,
                portrait.width, 5f * scale), accent);

            GUI.Label(new Rect(rect.x + 18f * scale, portrait.yMax + 8f * scale,
                    rect.width - 36f * scale, 42f * scale), selected.DisplayName,
                PrideCourtUiTheme.Heading(scale * 0.72f, TextAnchor.MiddleLeft, accent));
            GUI.Label(new Rect(rect.x + 18f * scale, portrait.yMax + 48f * scale,
                    rect.width - 36f * scale, 56f * scale), selected.SelectionTrait,
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.UpperLeft, PrideCourtUiTheme.Paper, true));

            float barY = portrait.yMax + 112f * scale;
            PrideCourtUiTheme.DrawBar(new Rect(rect.x + 18f * scale, barY,
                    rect.width - 36f * scale, 22f * scale), selected.Stats.WalkSpeed / 7.2f,
                accent, "SPEED", scale);
            PrideCourtUiTheme.DrawBar(new Rect(rect.x + 18f * scale, barY + 28f * scale,
                    rect.width - 36f * scale, 22f * scale), selected.Stats.Acceleration / 32f,
                PrideCourtUiTheme.Magenta, "ACCEL", scale);
            PrideCourtUiTheme.DrawBar(new Rect(rect.x + 18f * scale, barY + 56f * scale,
                    rect.width - 36f * scale, 22f * scale), selected.Stats.HitRadius / 2.5f,
                PrideCourtUiTheme.Yellow, "REACH", scale);
        }

        private void DrawCharacterRoster(Rect rect, float scale)
        {
            PrideCourtUiTheme.DrawPanel(rect, PrideCourtUiTheme.Tone.Violet, scale,
                "SELECT YOUR PRIDE / 全6選手");
            const int columns = 3;
            const float gap = 10f;
            int rows = Mathf.CeilToInt(AthleteCatalog.All.Count / (float)columns);
            float innerX = rect.x + 14f * scale;
            float innerY = rect.y + 34f * scale;
            float innerWidth = rect.width - 28f * scale;
            float innerHeight = rect.height - 48f * scale;
            float tileWidth = (innerWidth - gap * scale * (columns - 1)) / columns;
            float tileHeight = (innerHeight - gap * scale * (rows - 1)) / rows;

            for (int i = 0; i < AthleteCatalog.All.Count; i++)
            {
                AthleteIdentity identity = AthleteCatalog.All[i];
                Rect tile = new Rect(innerX + (i % columns) * (tileWidth + gap * scale),
                    innerY + (i / columns) * (tileHeight + gap * scale), tileWidth, tileHeight);
                bool selected = identity == selectedIdentity;
                bool hovered = tile.Contains(Event.current.mousePosition);
                Rect visual = hovered && !selected ? ScaleAroundCenter(tile, 1.025f) : tile;
                Color accent = selected
                    ? PrideCourtUiTheme.ToneColor(IdentityTone(identity))
                    : PrideCourtUiTheme.Muted;
                GUI.Box(visual, GUIContent.none, PrideCourtUiTheme.CardPanel(accent, scale));

                Rect art = new Rect(visual.x + 6f * scale, visual.y + 6f * scale,
                    visual.width - 12f * scale, visual.height - 42f * scale);
                DrawCharacterPortrait(art, identity, scale);
                if (selected)
                {
                    PrideCourtUiTheme.DrawTag(new Rect(visual.x + 7f * scale, visual.y + 7f * scale,
                        66f * scale, 22f * scale), "P1", IdentityTone(identity), scale);
                }
                GUI.Label(new Rect(visual.x + 6f * scale, visual.yMax - 36f * scale,
                        visual.width - 12f * scale, 30f * scale), AthleteCatalog.Get(identity).DisplayName,
                    PrideCourtUiTheme.Heading(scale * 0.47f, TextAnchor.MiddleCenter,
                        selected ? accent : PrideCourtUiTheme.Paper));

                if (GUI.Button(tile, GUIContent.none, GUIStyle.none)) SelectIdentity(identity);
            }
        }

        private void DrawCardLoadoutSelection(Rect panel, float scale)
        {
            Rect pool = new Rect(panel.x + 28f * scale, panel.y + 94f * scale,
                720f * scale, 472f * scale);
            Rect detail = new Rect(pool.xMax + 18f * scale, pool.y,
                panel.xMax - pool.xMax - 46f * scale, pool.height);
            DrawCardPool(pool, scale);
            DrawCardDetail(detail, scale);

            Rect back = new Rect(panel.x + 30f * scale, panel.yMax - 72f * scale,
                190f * scale, 48f * scale);
            if (GUI.Button(back, "◀  選手選択へ",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 14)))
                ApplySetupAction(SetupAction.Back);

            Rect reset = new Rect(back.xMax + 14f * scale, back.y,
                178f * scale, back.height);
            if (GUI.Button(reset, "おすすめに戻す",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, scale, 13)))
                ResetDeckToPreset();

            string readyLabel = DeckSize == 16
                ? (frontEndFlow.PendingMultiplayerEntry == MultiplayerEntry.None
                    ? "READY  /  コートへ挑む"
                    : "READY  /  ロビーへ進む")
                : $"LOADOUT  {DeckSize}/16";
            Rect ready = new Rect(panel.xMax - 342f * scale, panel.yMax - 76f * scale,
                312f * scale, 54f * scale);
            GUI.enabled = DeckSize == 16;
            if (GUI.Button(ready, readyLabel,
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Magenta, scale, 18)))
                CompleteSetup();
            GUI.enabled = true;

            GUI.Label(new Rect(reset.xMax + 14f * scale, back.y,
                    ready.x - reset.xMax - 28f * scale, back.height),
                "全16枚  •  通常3枚まで  •  固有カード2枚まで",
                PrideCourtUiTheme.Label(scale, 11, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));
        }

        private void DrawCardPool(Rect rect, float scale)
        {
            PrideCourtUiTheme.DrawPanel(rect, PrideCourtUiTheme.Tone.Violet, scale,
                "TACTIC BOARD / 使用可能カード");
            const int columns = 4;
            const float gap = 8f;
            int cardCount = AllowedCardCount(selectedIdentity);
            int rows = Mathf.Max(1, Mathf.CeilToInt(cardCount / (float)columns));
            float innerX = rect.x + 12f * scale;
            float innerY = rect.y + 34f * scale;
            float innerWidth = rect.width - 24f * scale;
            float innerHeight = rect.height - 46f * scale;
            float tileWidth = (innerWidth - gap * scale * (columns - 1)) / columns;
            float tileHeight = (innerHeight - gap * scale * (rows - 1)) / rows;

            int index = 0;
            foreach (CardId card in CardCatalog.All)
            {
                if (!CardCatalog.IsAllowedFor(card, selectedIdentity)) continue;
                Rect tile = new Rect(innerX + (index % columns) * (tileWidth + gap * scale),
                    innerY + (index / columns) * (tileHeight + gap * scale), tileWidth, tileHeight);
                DrawSetupCardTile(tile, card, scale);
                index++;
            }
        }

        private void DrawSetupCardTile(Rect rect, CardId card, float scale)
        {
            CardDefinition definition = CardCatalog.Get(card);
            Color accent = definition.IsCharacterCard
                ? PrideCourtUiTheme.ToneColor(IdentityTone(selectedIdentity))
                : CategoryColor(definition.Category);
            bool focused = hasFocusedCard && focusedCard == card;
            bool feedback = setupFeedbackCard == card && Time.unscaledTime < setupFeedbackUntil;
            float pulse = feedback
                ? 1f + Mathf.Sin((setupFeedbackUntil - Time.unscaledTime) * 28f) * 0.025f
                : 1f;
            Rect visual = ScaleAroundCenter(rect, pulse);
            GUI.Box(visual, GUIContent.none,
                PrideCourtUiTheme.CardPanel(focused ? accent : PrideCourtUiTheme.Muted, scale));
            PrideCourtUiTheme.DrawSolid(new Rect(visual.x + 5f * scale, visual.y + 5f * scale,
                4f * scale, visual.height - 10f * scale), accent);

            Rect art = new Rect(visual.x + 12f * scale, visual.y + 6f * scale,
                visual.width - 24f * scale, Mathf.Max(42f * scale, visual.height - 60f * scale));
            DrawCardArt(art, card, accent, scale);
            GUI.Label(new Rect(visual.x + 8f * scale, art.yMax + 2f * scale,
                    visual.width - 16f * scale, 20f * scale), definition.DisplayName,
                PrideCourtUiTheme.Label(scale, 10, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));

            int count = deckCounts.TryGetValue(card, out int value) ? value : 0;
            int max = definition.IsCharacterCard ? 2 : 3;
            Rect minus = new Rect(visual.x + 10f * scale, visual.yMax - 30f * scale,
                34f * scale, 25f * scale);
            Rect plus = new Rect(visual.xMax - 44f * scale, minus.y, minus.width, minus.height);
            GUI.enabled = count > 0;
            if (GUI.Button(minus, "−", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 12)))
                ChangeCount(card, -1);
            GUI.enabled = count < max && DeckSize < 16;
            if (GUI.Button(plus, "+", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, 12)))
                ChangeCount(card, 1);
            GUI.enabled = true;
            GUI.Label(new Rect(minus.xMax, minus.y, plus.x - minus.xMax, minus.height),
                count + "/" + max,
                PrideCourtUiTheme.Heading(scale * 0.4f, TextAnchor.MiddleCenter, accent));

            Rect focusArea = new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, rect.height - 34f * scale));
            if (GUI.Button(focusArea, GUIContent.none, GUIStyle.none)) FocusCard(card);
        }

        private void DrawCardDetail(Rect rect, float scale)
        {
            PrideCourtUiTheme.DrawPanel(rect, IdentityTone(selectedIdentity), scale,
                "MATCH BAG / " + DeckSize + " / 16");
            AthleteDefinition athlete = AthleteCatalog.Get(selectedIdentity);
            Color identityAccent = PrideCourtUiTheme.ToneColor(IdentityTone(selectedIdentity));
            Rect portrait = new Rect(rect.x + 14f * scale, rect.y + 34f * scale,
                70f * scale, 70f * scale);
            DrawCharacterPortrait(portrait, selectedIdentity, scale);
            GUI.Label(new Rect(portrait.xMax + 10f * scale, portrait.y,
                    rect.xMax - portrait.xMax - 24f * scale, 34f * scale), athlete.DisplayName,
                PrideCourtUiTheme.Heading(scale * 0.55f, TextAnchor.MiddleLeft, identityAccent));
            GUI.Label(new Rect(portrait.xMax + 10f * scale, portrait.y + 32f * scale,
                    rect.xMax - portrait.xMax - 24f * scale, 34f * scale), athlete.SpecialName,
                PrideCourtUiTheme.Label(scale, 10, TextAnchor.UpperLeft, PrideCourtUiTheme.Muted, true));

            if (!hasFocusedCard) FocusFirstAllowedCard();
            CardDefinition definition = CardCatalog.Get(focusedCard);
            Color accent = definition.IsCharacterCard ? identityAccent : CategoryColor(definition.Category);
            Rect art = new Rect(rect.x + 18f * scale, portrait.yMax + 12f * scale,
                rect.width - 36f * scale, 154f * scale);
            GUI.Box(Grow(art, 3f * scale), GUIContent.none, PrideCourtUiTheme.CardPanel(accent, scale));
            DrawCardArt(art, focusedCard, accent, scale);
            PrideCourtUiTheme.DrawTag(new Rect(art.x + 8f * scale, art.y + 8f * scale,
                112f * scale, 23f * scale), CategoryLabel(definition),
                definition.IsCharacterCard ? IdentityTone(selectedIdentity) : CategoryTone(definition.Category), scale);

            GUI.Label(new Rect(rect.x + 18f * scale, art.yMax + 9f * scale,
                    rect.width - 36f * scale, 34f * scale), definition.DisplayName,
                PrideCourtUiTheme.Heading(scale * 0.56f, TextAnchor.MiddleLeft, accent));
            GUI.Label(new Rect(rect.x + 18f * scale, art.yMax + 42f * scale,
                    rect.width - 36f * scale, 42f * scale), Describe(focusedCard),
                PrideCourtUiTheme.Label(scale, 11, TextAnchor.UpperLeft, PrideCourtUiTheme.Paper, true));

            Rect slots = new Rect(rect.x + 18f * scale, rect.yMax - 86f * scale,
                rect.width - 36f * scale, 54f * scale);
            DrawDeckSlots(slots, scale);
        }

        private void DrawDeckSlots(Rect rect, float scale)
        {
            const int columns = 8;
            const int rows = 2;
            const float gap = 4f;
            float slotWidth = (rect.width - gap * scale * (columns - 1)) / columns;
            float slotHeight = (rect.height - gap * scale * (rows - 1)) / rows;
            List<CardId> deck = BuildDeck();
            for (int i = 0; i < 16; i++)
            {
                Rect slot = new Rect(rect.x + (i % columns) * (slotWidth + gap * scale),
                    rect.y + (i / columns) * (slotHeight + gap * scale), slotWidth, slotHeight);
                Color color = i < deck.Count
                    ? CategoryColor(CardCatalog.Get(deck[i]).Category)
                    : PrideCourtUiTheme.Muted;
                GUI.Box(slot, GUIContent.none, PrideCourtUiTheme.CardPanel(color, scale));
                GUI.Label(slot, (i + 1).ToString("00"), PrideCourtUiTheme.Label(scale, 8,
                    TextAnchor.MiddleCenter, i < deck.Count ? PrideCourtUiTheme.Paper : PrideCourtUiTheme.Muted));
            }
        }

        private void DrawCardArt(Rect rect, CardId card, Color fallback, float scale)
        {
            if (setupCardArt.TryGetValue(card, out Texture2D texture) && texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
                return;
            }

            CardDefinition definition = CardCatalog.Get(card);
            if (definition.IsCharacterCard && characterAtlas != null &&
                (definition.Owner == AthleteIdentity.Lux || definition.Owner == AthleteIdentity.Bastion))
            {
                float u = definition.Owner == AthleteIdentity.Lux ? 0f : 0.5f;
                GUI.DrawTextureWithTexCoords(rect, characterAtlas, new Rect(u, 0f, 0.25f, 1f), true);
                return;
            }

            PrideCourtUiTheme.DrawSolid(rect, Color.Lerp(PrideCourtUiTheme.Raised, fallback, 0.26f));
            GUI.Label(rect, CardCatalog.Get(card).DisplayName,
                PrideCourtUiTheme.Label(scale, 11, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper, true));
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
                ? $"ゲーム  {PrideCourtUiTheme.FormatGameStars(match.Score.NearGames, match.Score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(match.Score.FarGames, match.Score.GamesToWin)}"
                : $"ゲーム  {PrideCourtUiTheme.FormatGameStars(match.Score.FarGames, match.Score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(match.Score.NearGames, match.Score.GamesToWin)}";
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
            AthleteIdentity opponent = AthleteCatalog.Get(selectedIdentity).DefaultOpponent;
            player.SetIdentity(selectedIdentity);
            cpu.SetIdentity(opponent);
            player.GetComponent<CardLoadoutController>().RebuildDeck(BuildDeck());
            cpu.GetComponent<CardLoadoutController>().RebuildDeck(CardCatalog.BuildPreset(opponent));
            SaveDeck();
            match.StartNewMatch(MatchScore.DefaultGamesToWin);
        }

        public bool TrySelectLocalGamesToWin(int gamesToWin)
        {
            if (frontEndFlow.Screen != FrontEndScreen.LocalNetworkLobby ||
                !MatchScore.IsSupportedGamesToWin(gamesToWin) ||
                (lanSession != null && (lanSession.IsBusy || lanSession.IsConnected)))
            {
                return false;
            }

            selectedLocalGamesToWin = gamesToWin;
            return true;
        }

        private void DrawLocalGamesToWin(Rect hostPanel, float scale)
        {
            GUI.Label(new Rect(hostPanel.x + 24f * scale, hostPanel.y + 170f * scale,
                    hostPanel.width - 48f * scale, 24f * scale), "試合形式 / ホストが決定",
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));

            const float gap = 8f;
            float rowX = hostPanel.x + 42f * scale;
            float rowWidth = hostPanel.width - 84f * scale;
            float buttonWidth = (rowWidth - gap * scale * 2f) / 3f;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && lanSession != null && !lanSession.IsBusy && !lanSession.IsConnected;
            for (int gamesToWin = MatchScore.MinimumGamesToWin;
                 gamesToWin <= MatchScore.MaximumGamesToWin;
                 gamesToWin++)
            {
                int index = gamesToWin - MatchScore.MinimumGamesToWin;
                Rect button = new Rect(rowX + index * (buttonWidth + gap * scale),
                    hostPanel.y + 198f * scale, buttonWidth, 40f * scale);
                PrideCourtUiTheme.Tone tone = selectedLocalGamesToWin == gamesToWin
                    ? PrideCourtUiTheme.Tone.Magenta
                    : PrideCourtUiTheme.Tone.Neutral;
                if (GUI.Button(button, gamesToWin + "ゲーム", PrideCourtUiTheme.Button(tone, scale, 13)))
                    TrySelectLocalGamesToWin(gamesToWin);
            }
            GUI.enabled = previousEnabled;
        }

        private bool HandleSetupNavigation()
        {
            bool confirm = UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                           UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) ||
                           UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0);
            if (confirm)
            {
                if (setupFlow.Step == SetupStep.CharacterSelect)
                {
                    ApplySetupAction(SetupAction.ConfirmCharacter);
                }
                else if (DeckSize == 16)
                {
                    CompleteSetup();
                }
                else if (hasFocusedCard)
                {
                    ChangeCount(focusedCard, 1);
                }
                return true;
            }

            bool remove = UnityEngine.Input.GetKeyDown(KeyCode.Backspace) ||
                          UnityEngine.Input.GetKeyDown(KeyCode.Delete) ||
                          UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2);
            if (remove && setupFlow.Step == SetupStep.CardLoadout && hasFocusedCard)
            {
                ChangeCount(focusedCard, -1);
                return true;
            }

            int horizontal = 0;
            int vertical = 0;
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow)) horizontal = -1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)) horizontal = 1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)) vertical = 1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow)) vertical = -1;

            if (horizontal != 0 || vertical != 0)
            {
                setupNavigationHeld = true;
                nextSetupNavigationAt = Time.unscaledTime + 0.18f;
            }
            else
            {
                float axisX = UnityEngine.Input.GetAxisRaw("Horizontal");
                float axisY = UnityEngine.Input.GetAxisRaw("Vertical");
                bool axisActive = Mathf.Abs(axisX) > 0.62f || Mathf.Abs(axisY) > 0.62f;
                if (!axisActive)
                {
                    setupNavigationHeld = false;
                    return false;
                }

                if (setupNavigationHeld && Time.unscaledTime < nextSetupNavigationAt) return false;
                setupNavigationHeld = true;
                nextSetupNavigationAt = Time.unscaledTime + 0.18f;
                if (Mathf.Abs(axisX) >= Mathf.Abs(axisY)) horizontal = axisX < 0f ? -1 : 1;
                else vertical = axisY < 0f ? -1 : 1;
            }

            if (setupFlow.Step == SetupStep.CharacterSelect)
                MoveCharacterFocus(horizontal, vertical);
            else
                MoveCardFocus(horizontal, vertical);
            return true;
        }

        private void MoveCharacterFocus(int horizontal, int vertical)
        {
            const int columns = 3;
            int count = AthleteCatalog.All.Count;
            int index = IdentityIndex(selectedIdentity);
            int row = index / columns;
            int column = index % columns;
            int rows = Mathf.CeilToInt(count / (float)columns);
            if (horizontal != 0) column = (column + horizontal + columns) % columns;
            if (vertical != 0) row = (row - vertical + rows) % rows;
            int target = Mathf.Min(row * columns + column, count - 1);
            SelectIdentity(AthleteCatalog.All[target]);
        }

        private void MoveCardFocus(int horizontal, int vertical)
        {
            List<CardId> allowed = BuildAllowedCards();
            if (allowed.Count == 0) return;
            int index = hasFocusedCard ? allowed.IndexOf(focusedCard) : 0;
            if (index < 0) index = 0;
            int offset = horizontal != 0 ? horizontal : -vertical * 4;
            index = (index + offset) % allowed.Count;
            if (index < 0) index += allowed.Count;
            FocusCard(allowed[index]);
        }

        private void ResetSetupFlow()
        {
            setupFlow.Reset();
            setupStepStartedAt = Time.unscaledTime;
            setupNavigationHeld = false;
            FocusFirstAllowedCard();
        }

        private void CompleteSetup()
        {
            if (DeckSize != 16) return;
            SaveDeck();
            switch (frontEndFlow.PendingMultiplayerEntry)
            {
                case MultiplayerEntry.Local:
                    ApplyFrontEndAction(FrontEndAction.ReturnToLocalLobby);
                    break;
                case MultiplayerEntry.Network:
                    ApplyFrontEndAction(FrontEndAction.ReturnToOnlineLobby);
                    break;
                default:
                    StartMatch();
                    break;
            }
        }

        private void DrawLobbyLoadoutSummary(Rect rect, float scale)
        {
            Color accent = PrideCourtUiTheme.ToneColor(IdentityTone(selectedIdentity));
            GUI.Box(rect, GUIContent.none, PrideCourtUiTheme.CardPanel(accent, scale));
            GUI.Label(new Rect(rect.x + 14f * scale, rect.y, rect.width - 260f * scale, rect.height),
                "使用選手  " + AthleteCatalog.Get(selectedIdentity).DisplayName + "    •    デッキ  " + DeckSize + "/16",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleLeft, PrideCourtUiTheme.Paper));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && (lanSession == null || (!lanSession.IsBusy && !lanSession.IsConnected));
            if (GUI.Button(new Rect(rect.xMax - 230f * scale, rect.y + 3f * scale,
                        218f * scale, rect.height - 6f * scale), "選手・カードを変更",
                    PrideCourtUiTheme.Button(IdentityTone(selectedIdentity), scale, 12)))
                ApplyFrontEndAction(FrontEndAction.EditSetup);
            GUI.enabled = previousEnabled;
        }

        private void FocusCard(CardId card)
        {
            if (!CardCatalog.IsAllowedFor(card, selectedIdentity)) return;
            focusedCard = card;
            hasFocusedCard = true;
        }

        private void FocusFirstAllowedCard()
        {
            hasFocusedCard = false;
            foreach (CardId card in CardCatalog.All)
            {
                if (!CardCatalog.IsAllowedFor(card, selectedIdentity)) continue;
                focusedCard = card;
                hasFocusedCard = true;
                return;
            }
        }

        private List<CardId> BuildAllowedCards()
        {
            List<CardId> allowed = new List<CardId>();
            foreach (CardId card in CardCatalog.All)
                if (CardCatalog.IsAllowedFor(card, selectedIdentity)) allowed.Add(card);
            return allowed;
        }

        private void ResetDeckToPreset()
        {
            deckCounts.Clear();
            foreach (CardId card in CardCatalog.BuildPreset(selectedIdentity))
            {
                deckCounts.TryGetValue(card, out int count);
                deckCounts[card] = count + 1;
                setupFeedbackCard = card;
            }
            setupFeedbackUntil = Time.unscaledTime + 0.42f;
            FocusFirstAllowedCard();
        }

        private static int IdentityIndex(AthleteIdentity identity)
        {
            for (int i = 0; i < AthleteCatalog.All.Count; i++)
                if (AthleteCatalog.All[i] == identity) return i;
            return 0;
        }

        private void SelectIdentity(AthleteIdentity identity)
        {
            if (selectedIdentity == identity) return;
            selectedIdentity = identity;
            PlayerPrefs.SetInt("PrideCourt.PlayerIdentity", (int)identity);
            LoadDeck(identity);
            FocusFirstAllowedCard();
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
            if (!CardCatalog.IsAllowedFor(card, selectedIdentity)) return;
            deckCounts.TryGetValue(card, out int count);
            int max = CardCatalog.Get(card).IsCharacterCard ? 2 : 3;
            if (delta > 0 && DeckSize >= 16) return;
            int next = Mathf.Clamp(count + delta, 0, max);
            if (next == count) return;
            deckCounts[card] = next;
            FocusCard(card);
            setupFeedbackCard = card;
            setupFeedbackUntil = Time.unscaledTime + 0.38f;
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

        private static PrideCourtUiTheme.Tone CategoryTone(CardCategory category) => category switch
        {
            CardCategory.PersonalBuff => PrideCourtUiTheme.Tone.Cyan,
            CardCategory.Instant => PrideCourtUiTheme.Tone.Magenta,
            CardCategory.NextShot => PrideCourtUiTheme.Tone.Violet,
            _ => PrideCourtUiTheme.Tone.Yellow
        };

        private static string CategoryLabel(CardDefinition definition)
        {
            if (definition.IsCharacterCard) return "CHARACTER";
            return definition.Category switch
            {
                CardCategory.PersonalBuff => "PERSONAL",
                CardCategory.Instant => "INSTANT",
                CardCategory.NextShot => "NEXT SHOT",
                _ => "COURT"
            };
        }

        private static string Describe(CardId card) => card switch
        {
            CardId.AccelStep => "加速力アップ • 5秒", CardId.EcoRun => "ダッシュ消費半減 • 6秒",
            CardId.RecoveryPulse => "スタミナを35回復", CardId.SpinBoost => "次のショットのスピン強化",
            CardId.GaugeCharge => "スペシャルゲージ +15", CardId.GripCourt => "コート上の加速力アップ",
            CardId.SlipCourt => "コート上の加速力ダウン", CardId.HighBounce => "このラリーのバウンド上昇",
            CardId.FlashStep => "ルクスの加速力 +30%", CardId.TailFeint => "ルクスの次のBショットがフェイント",
            CardId.RailBoost => "バスティオンの移動速度 +20%", CardId.AnchorCore => "バスティオンの強打消費半減",
            CardId.LeafVeil => "相手の視界を木の葉で1.6秒遮る",
            CardId.MischiefCurve => "次の球が途中で悪戯な変化をする",
            CardId.TimeTease => "相手の移動と反応を一瞬遅くする",
            CardId.DragonGrace => "6秒間ショット威力と精度を強化",
            CardId.NobleRetake => "次のミスショットを1回やり直す",
            CardId.DragonAwakening => "劣勢か6返球以上で竜醒する",
            CardId.JetIgnition => "4秒間、最高速 +30% • 消費も増加",
            CardId.VectorWing => "次のダイブの消費と硬直を軽減",
            CardId.AirBrake => "5秒間、加減速と方向転換を強化",
            CardId.LeafMasquerade => "木の葉で相手の視界を1.35秒撹乱",
            CardId.BorrowedForm => "次の返球に判定を持たない偽球を重ねる",
            _ => "次の返球フォームだけ強弱を逆に見せる"
        };

        private void DrawIdentityTabs(Rect rect, float scale)
        {
            const float gap = 5f;
            float buttonWidth = (rect.width - gap * scale * (AthleteCatalog.All.Count - 1)) /
                                AthleteCatalog.All.Count;
            for (int i = 0; i < AthleteCatalog.All.Count; i++)
            {
                AthleteIdentity identity = AthleteCatalog.All[i];
                Rect button = new Rect(rect.x + i * (buttonWidth + gap * scale), rect.y, buttonWidth, rect.height);
                PrideCourtUiTheme.Tone tone = selectedIdentity == identity ? IdentityTone(identity) : PrideCourtUiTheme.Tone.Neutral;
                if (GUI.Button(button, AthleteCatalog.Get(identity).DisplayName,
                        PrideCourtUiTheme.Button(tone, scale, 11)))
                {
                    SelectIdentity(identity);
                }
            }
        }

        private void DrawCharacterPortrait(Rect rect, AthleteIdentity identity, float scale)
        {
            if (characterPortraits.TryGetValue(identity, out Texture2D portrait) && portrait != null)
            {
                DrawTopAlignedCrop(rect, portrait);
                return;
            }

            if (characterAtlas != null && (identity == AthleteIdentity.Lux || identity == AthleteIdentity.Bastion))
            {
                float u = identity == AthleteIdentity.Lux ? 0f : 0.5f;
                GUI.DrawTextureWithTexCoords(rect, characterAtlas, new Rect(u, 0f, 0.25f, 1f), true);
                return;
            }

            GUI.Box(rect, AthleteCatalog.Get(identity).DisplayName,
                PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.ToneColor(IdentityTone(identity)), scale));
        }

        private static void DrawTopAlignedCrop(Rect rect, Texture2D texture)
        {
            float sourceAspect = texture.width / (float)texture.height;
            float targetAspect = rect.width / Mathf.Max(1f, rect.height);
            Rect uv;
            if (sourceAspect > targetAspect)
            {
                float visibleWidth = targetAspect / sourceAspect;
                uv = new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
            }
            else
            {
                float visibleHeight = sourceAspect / targetAspect;
                uv = new Rect(0f, 1f - visibleHeight, 1f, visibleHeight);
            }

            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }

        private static int AllowedCardCount(AthleteIdentity identity)
        {
            int count = 0;
            foreach (CardId card in CardCatalog.All)
                if (CardCatalog.IsAllowedFor(card, identity)) count++;
            return count;
        }

        private static PrideCourtUiTheme.Tone IdentityTone(AthleteIdentity identity)
        {
            return identity switch
            {
                AthleteIdentity.Lux => PrideCourtUiTheme.Tone.Cyan,
                AthleteIdentity.Bastion => PrideCourtUiTheme.Tone.Yellow,
                AthleteIdentity.Lucia => PrideCourtUiTheme.Tone.Magenta,
                AthleteIdentity.Charlotte => PrideCourtUiTheme.Tone.Violet,
                AthleteIdentity.Zephyr => PrideCourtUiTheme.Tone.Cyan,
                _ => PrideCourtUiTheme.Tone.Yellow
            };
        }

    }
}
