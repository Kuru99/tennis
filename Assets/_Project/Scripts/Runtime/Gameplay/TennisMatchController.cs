using System;
using System.Collections;
using PrideCourt.Cards;
using PrideCourt.Domain;
using UnityEngine;
using PrideCourt.Presentation;

namespace PrideCourt.Gameplay
{
    public sealed class TennisMatchController : MonoBehaviour
    {
        private const float ServeLimitSeconds = 10f;
        private const float PointResultSeconds = 1.35f;
        private const float CardSelectionSeconds = 15f;

        [SerializeField] private TennisAthleteController nearAthlete;
        [SerializeField] private TennisAthleteController farAthlete;
        [SerializeField] private TennisBallController ball;

        private readonly MatchScore score = new MatchScore();
        private float phaseTimer;
        private int serveFaults;
        private string statusMessage = string.Empty;
        private CardLoadoutController nearCards;
        private CardLoadoutController farCards;
        private bool grantNearReward;
        private bool grantFarReward;
        private int validRallyReturns;
        private bool isGameplayPaused;
        private bool serveHasBeenStruck;
        private CardId? activeCourtCard;
        private string timingMessage = string.Empty;
        private float timingMessageRemaining;
        private Texture2D specialCutInTexture;
        private bool networkReplica;

        public MatchPhase Phase { get; private set; } = MatchPhase.Preparing;
        public bool HasStarted { get; private set; }
        public CourtSide Server => score.Server;
        public MatchScore Score => score;
        public float ServeTimeRemaining => ServeRestrictionsActive ? Mathf.Max(0f, phaseTimer) : 0f;
        public float CardSelectionTimeRemaining => Phase == MatchPhase.CardSelection ? Mathf.Max(0f, phaseTimer) : 0f;
        public bool CardsAllowed => Phase == MatchPhase.Rally && !isGameplayPaused;
        public bool ServeRestrictionsActive => Phase == MatchPhase.Serving && !serveHasBeenStruck;
        public bool IsServeAwaitingReturn => Phase == MatchPhase.Serving && serveHasBeenStruck;
        public bool IsGameplayPaused => isGameplayPaused;
        public bool HasSpecialCutInTexture => specialCutInTexture != null;
        public CourtSide LocalPlayerSide { get; private set; } = CourtSide.Near;
        public float AccelerationMultiplier => activeCourtCard == CardId.GripCourt ? 1.25f : activeCourtCard == CardId.SlipCourt ? 0.65f : 1f;
        public float BounceMultiplier => activeCourtCard == CardId.HighBounce ? 1.3f : 1f;

        public void Configure(
            TennisAthleteController configuredNearAthlete,
            TennisAthleteController configuredFarAthlete,
            TennisBallController configuredBall)
        {
            nearAthlete = configuredNearAthlete;
            farAthlete = configuredFarAthlete;
            ball = configuredBall;
            nearCards = nearAthlete == null ? null : nearAthlete.GetComponent<CardLoadoutController>();
            farCards = farAthlete == null ? null : farAthlete.GetComponent<CardLoadoutController>();
        }

        private void Start()
        {
            nearAthlete?.BindMatchRuntime(this, ball);
            farAthlete?.BindMatchRuntime(this, ball);
            ball?.Configure(this);
            score.Reset();
            Phase = MatchPhase.Preparing;
            statusMessage = "プライド・コート";
            specialCutInTexture = Resources.Load<Texture2D>("SpecialCutIn");
        }

        private void Update()
        {
            if (networkReplica) return;
            if (!HasStarted) return;
            if (isGameplayPaused) return;
            if (timingMessageRemaining > 0f)
            {
                timingMessageRemaining -= Time.unscaledDeltaTime;
                if (timingMessageRemaining <= 0f) timingMessage = string.Empty;
            }
            switch (Phase)
            {
                case MatchPhase.Preparing:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        BeginPoint();
                    }
                    break;

                case MatchPhase.PointResult:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        BeginCardSelection();
                    }
                    break;

                case MatchPhase.CardSelection:
                    phaseTimer -= Time.unscaledDeltaTime;
                    if ((nearCards == null || nearCards.IsPreparationReady) &&
                        (farCards == null || farCards.IsPreparationReady))
                    {
                        BeginPoint();
                    }
                    else if (phaseTimer <= 0f)
                    {
                        nearCards?.ForcePreparationReady();
                        farCards?.ForcePreparationReady();
                        BeginPoint();
                    }
                    break;

                case MatchPhase.Serving:
                    if (!serveHasBeenStruck)
                    {
                        phaseTimer -= Time.deltaTime;
                        if (phaseTimer <= 0f)
                        {
                            RegisterServeFault("10秒経過");
                        }
                    }
                    break;
            }
        }

        public void SetLocalPlayerSide(CourtSide side)
        {
            LocalPlayerSide = side;
        }

        public void SetNetworkReplica(bool value)
        {
            networkReplica = value;
        }

        public void SetSettingsPaused(bool paused)
        {
            if (!HasStarted && paused) return;
            isGameplayPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        public LanMatchState CaptureLanState(uint sequence)
        {
            return new LanMatchState
            {
                Sequence = sequence,
                HasStarted = HasStarted,
                Phase = Phase,
                NearPoints = score.NearPoints,
                FarPoints = score.FarPoints,
                CompletedPoints = score.CompletedPoints,
                PhaseTimeRemaining = phaseTimer,
                ServeHasBeenStruck = serveHasBeenStruck,
                IsGameplayPaused = isGameplayPaused,
                StatusMessage = statusMessage,
                HasActiveCourtCard = activeCourtCard.HasValue,
                ActiveCourtCard = activeCourtCard.GetValueOrDefault(),
                NearAthlete = nearAthlete.CaptureLanState(),
                FarAthlete = farAthlete.CaptureLanState(),
                Ball = ball.CaptureLanState(),
                NearCards = nearCards == null ? default : nearCards.CaptureLanState(),
                FarCards = farCards == null ? default : farCards.CaptureLanState()
            };
        }

        public void ApplyLanState(LanMatchState state)
        {
            networkReplica = true;
            HasStarted = state.HasStarted;
            Phase = state.Phase;
            score.Restore(state.NearPoints, state.FarPoints, state.CompletedPoints);
            phaseTimer = state.PhaseTimeRemaining;
            serveHasBeenStruck = state.ServeHasBeenStruck;
            isGameplayPaused = state.IsGameplayPaused;
            Time.timeScale = isGameplayPaused ? 0f : 1f;
            statusMessage = state.StatusMessage ?? string.Empty;
            activeCourtCard = state.HasActiveCourtCard ? state.ActiveCourtCard : null;
            nearAthlete.ApplyLanState(state.NearAthlete);
            farAthlete.ApplyLanState(state.FarAthlete);
            ball.ApplyLanState(state.Ball);
            nearCards?.ApplyLanState(state.NearCards);
            farCards?.ApplyLanState(state.FarCards);
        }

        public void StartNewMatch()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            isGameplayPaused = false;
            score.Reset();
            serveFaults = 0;
            validRallyReturns = 0;
            serveHasBeenStruck = false;
            activeCourtCard = null;
            grantNearReward = false;
            grantFarReward = false;
            timingMessage = string.Empty;
            phaseTimer = 0.2f;
            Phase = MatchPhase.Preparing;
            HasStarted = true;
            statusMessage = "準備";
        }

        public void ReturnToSetup()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            isGameplayPaused = false;
            HasStarted = false;
            Phase = MatchPhase.Preparing;
            statusMessage = "プライド・コート";
            ball.ResetForServe(nearAthlete);
        }

        public void NotifyShotTiming(CourtSide side, TimingGrade timing)
        {
            timingMessage = timing switch
            {
                TimingGrade.Just => "ジャスト！",
                TimingGrade.Normal => "グッド",
                _ => "セーフリターン"
            };
            timingMessageRemaining = timing == TimingGrade.Just ? 0.65f : 0.4f;
        }

        private void BeginPoint()
        {
            if (score.TryGetWinner(out CourtSide winner))
            {
                Phase = MatchPhase.MatchOver;
                statusMessage = AthleteLabel(winner) + "の勝利";
                return;
            }

            serveFaults = 0;
            validRallyReturns = 0;
            serveHasBeenStruck = false;
            activeCourtCard = null;
            phaseTimer = ServeLimitSeconds;
            Phase = MatchPhase.Serving;
            statusMessage = "サーブ";

            bool leftStart = score.CompletedPoints % 2 == 0;
            float serverX = leftStart ? -TennisCourtGeometry.StartingLateralOffset : TennisCourtGeometry.StartingLateralOffset;
            float receiverX = -serverX;
            nearAthlete.ResetForPoint(new Vector3(Server == CourtSide.Near ? serverX : receiverX, 1f, -TennisCourtGeometry.ServingBaseline));
            farAthlete.ResetForPoint(new Vector3(Server == CourtSide.Far ? serverX : receiverX, 1f, TennisCourtGeometry.ServingBaseline));
            ball.ResetForServe(Server == CourtSide.Near ? nearAthlete : farAthlete);
            nearCards?.BeginPoint();
            farCards?.BeginPoint();
        }

        public void NotifyServeStruck()
        {
            if (Phase != MatchPhase.Serving || serveHasBeenStruck)
            {
                return;
            }

            serveHasBeenStruck = true;
            phaseTimer = 0f;
            statusMessage = string.Empty;
        }

        private void BeginCardSelection()
        {
            nearCards ??= nearAthlete == null ? null : nearAthlete.GetComponent<CardLoadoutController>();
            farCards ??= farAthlete == null ? null : farAthlete.GetComponent<CardLoadoutController>();
            Phase = MatchPhase.CardSelection;
            phaseTimer = CardSelectionSeconds;
            statusMessage = "カードドロー";
            nearCards?.BeginPointPreparation(grantNearReward);
            farCards?.BeginPointPreparation(grantFarReward);
            grantNearReward = false;
            grantFarReward = false;
        }

        public void NotifyValidServe()
        {
            if (Phase != MatchPhase.Serving)
            {
                return;
            }

            Phase = MatchPhase.Rally;
            serveHasBeenStruck = false;
            statusMessage = string.Empty;
        }

        public void NotifyRallyReturn()
        {
            if (Phase == MatchPhase.Rally)
            {
                validRallyReturns++;
            }
        }

        public void NotifyCardUsed(CourtSide side, string cardName)
        {
            statusMessage = AthleteLabel(side) + "のカード — " + cardName;
        }

        public void ApplyCourtCard(CardId card)
        {
            activeCourtCard = card;
            statusMessage = "コート変化 — " + CardCatalog.Get(card).DisplayName;
        }

        public void PlaySpecialCutIn(TennisAthleteController athlete, Action strike)
        {
            if (isGameplayPaused || strike == null) return;
            StartCoroutine(PlaySpecialSequence(athlete, strike));
        }

        private IEnumerator PlaySpecialSequence(TennisAthleteController athlete, Action strike)
        {
            PrideCourtAudio.Instance?.PlaySpecial();
            isGameplayPaused = true;
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            statusMessage = athlete.Identity == AthleteIdentity.Lux ? "ミラージュ・バウンド" : "グラビティ・ドライブ";
            yield return new WaitForSecondsRealtime(1.2f);
            strike();
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = previousScale <= 0f ? 1f : previousScale;
            isGameplayPaused = false;
            statusMessage = string.Empty;
        }

        public void RegisterServeFault(string reason)
        {
            if (Phase != MatchPhase.Serving)
            {
                return;
            }

            serveFaults++;
            if (serveFaults >= 2)
            {
                AwardPoint(Server.Opposite(), "ダブルフォルト");
                return;
            }

            phaseTimer = ServeLimitSeconds;
            serveHasBeenStruck = false;
            statusMessage = "フォルト — セカンドサーブ";
            ball.ResetForServe(Server == CourtSide.Near ? nearAthlete : farAthlete);
        }

        public void RegisterServeLet()
        {
            if (Phase != MatchPhase.Serving)
            {
                return;
            }

            phaseTimer = ServeLimitSeconds;
            serveHasBeenStruck = false;
            statusMessage = "レット — サーブやり直し";
            ball.ResetForServe(Server == CourtSide.Near ? nearAthlete : farAthlete);
        }

        public void AwardPoint(CourtSide winner, string reason)
        {
            if (Phase == MatchPhase.PointResult || Phase == MatchPhase.MatchOver)
            {
                return;
            }

            ball?.StopForPointEnd();
            score.AwardPoint(winner);
            PrideCourtAudio.Instance?.PlayPoint();
            bool rewardEligible = validRallyReturns >= 1;
            grantNearReward = rewardEligible && nearCards != null && !nearCards.UsedCardThisPoint && !nearCards.Hand.HasCharacterCard;
            grantFarReward = rewardEligible && farCards != null && !farCards.UsedCardThisPoint && !farCards.Hand.HasCharacterCard;
            activeCourtCard = null;
            statusMessage = AthleteLabel(winner) + "のポイント — " + reason;
            if (score.TryGetWinner(out CourtSide matchWinner))
            {
                Phase = MatchPhase.MatchOver;
                statusMessage = AthleteLabel(matchWinner) + "の勝利";
            }
            else
            {
                Phase = MatchPhase.PointResult;
                phaseTimer = PointResultSeconds;
            }
        }

        private void OnGUI()
        {
            if (HudSettingsController.IsAnyOpen) return;
            if (!HasStarted || !HudPreferences.Visible) return;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.3f) * HudPreferences.Scale;
            float scoreWidth = Mathf.Clamp(safe.width * 0.24f, 250f * scale, 430f * scale);
            Rect scorePanel = new Rect(safe.center.x - scoreWidth * 0.5f, safe.y + 14f * scale, scoreWidth, 66f * scale);
            PrideCourtUiTheme.DrawPanel(scorePanel, PrideCourtUiTheme.Tone.Cyan, scale);
            PrideCourtUiTheme.DrawTag(new Rect(scorePanel.x + 12f * scale, scorePanel.y - 5f * scale, 74f * scale, 23f * scale),
                "ポイント", PrideCourtUiTheme.Tone.Magenta, scale);
            GUI.Label(new Rect(scorePanel.x + 8f * scale, scorePanel.y + 8f * scale, scorePanel.width - 16f * scale, 52f * scale),
                LocalPlayerSide == CourtSide.Near
                    ? $"{score.NearPoints}  —  {score.FarPoints}"
                    : $"{score.FarPoints}  —  {score.NearPoints}",
                PrideCourtUiTheme.Heading(scale, TextAnchor.MiddleCenter));

            GUIStyle statusStyle = PrideCourtUiTheme.Heading(scale * 0.62f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Cyan);

            if (ServeRestrictionsActive)
            {
                GUI.Label(new Rect(safe.center.x - 140f * scale, scorePanel.yMax - 2f * scale, 280f * scale, 36f * scale),
                    $"サーブ / {Mathf.CeilToInt(ServeTimeRemaining)}", statusStyle);
                if (Server == LocalPlayerSide)
                {
                    bool tossed = ball != null && ball.IsTossed;
                    string guide = Application.isMobilePlatform
                        ? tossed
                            ? "もう一度AかBを押してサーブ"
                            : "位置を決めて AかBでトス"
                        : tossed
                            ? "もう一度 左クリック/J または 右クリック/K でサーブ"
                            : "位置を決めて 左クリック/J または 右クリック/K でトス";
                    Rect guidePanel = new Rect(safe.x + safe.width * 0.12f, safe.yMax - 146f * scale, safe.width * 0.76f, 42f * scale);
                    GUI.Box(guidePanel, GUIContent.none, PrideCourtUiTheme.CardPanel(PrideCourtUiTheme.Magenta, scale));
                    GUI.Label(guidePanel, guide, PrideCourtUiTheme.Label(scale, 16, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));
                }
            }

            if (isGameplayPaused && specialCutInTexture != null)
            {
                DrawSpecialCutIn(safe, scale);
            }

            if (!string.IsNullOrEmpty(statusMessage) && !isGameplayPaused)
            {
                GUI.Label(new Rect(safe.x + safe.width * 0.2f, safe.y + safe.height * 0.17f, safe.width * 0.6f, 48f * scale),
                    statusMessage, statusStyle);
            }

            if (!string.IsNullOrEmpty(timingMessage))
            {
                Color timingColor = timingMessage == "ジャスト！" ? PrideCourtUiTheme.Magenta : PrideCourtUiTheme.Cyan;
                GUI.Label(new Rect(safe.x + safe.width * 0.2f, safe.y + safe.height * 0.64f, safe.width * 0.6f, 58f * scale),
                    timingMessage, PrideCourtUiTheme.Heading(scale * 0.9f, TextAnchor.MiddleCenter, timingColor));
            }

            if (HudPreferences.ShowResources) DrawResourceBars(LocalPlayerSide == CourtSide.Near ? nearAthlete : farAthlete);
            DrawOpponentCards();
            if (Phase == MatchPhase.CardSelection)
            {
                GUI.Label(new Rect(safe.center.x - 140f * scale, scorePanel.yMax + 28f * scale, 280f * scale, 32f * scale),
                    $"カード選択 / {Mathf.CeilToInt(CardSelectionTimeRemaining)}", statusStyle);
            }
        }

        private void DrawSpecialCutIn(Rect safe, float scale)
        {
            Rect imageRect = new Rect(safe.x, safe.y + safe.height * 0.12f, safe.width, safe.height * 0.76f);
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(imageRect, specialCutInTexture, ScaleMode.ScaleAndCrop, true, 16f / 9f);

            Rect titleBand = new Rect(safe.x, safe.y + safe.height * 0.17f, safe.width, 64f * scale);
            GUI.color = new Color(0.01f, 0.02f, 0.07f, 0.78f);
            GUI.DrawTexture(titleBand, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUIStyle titleStyle = PrideCourtUiTheme.Heading(scale * 0.78f, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper);
            GUI.Label(titleBand, statusMessage, titleStyle);

            Rect tagRect = new Rect(safe.center.x - 58f * scale, imageRect.yMax - 38f * scale, 116f * scale, 26f * scale);
            PrideCourtUiTheme.DrawTag(tagRect, "必殺技", PrideCourtUiTheme.Tone.Magenta, scale);
        }

        private void DrawOpponentCards()
        {
            CardLoadoutController opponentCards = LocalPlayerSide == CourtSide.Near ? farCards : nearCards;
            if (opponentCards == null) return;
            float scale = HudPreferences.Scale;
            float width = 52f * scale;
            for (int i = 0; i < opponentCards.Hand.Count; i++)
            {
                Rect card = new Rect(Screen.safeArea.x + 18f * scale + i * (width + 5f), Screen.safeArea.y + 18f * scale, width, 30f * scale);
                GUI.Box(card, "相手", PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Violet, scale, 11));
            }
        }

        private string AthleteLabel(CourtSide side)
        {
            TennisAthleteController athlete = side == CourtSide.Near ? nearAthlete : farAthlete;
            return athlete != null && athlete.Identity == AthleteIdentity.Bastion ? "バスティオン" : "ルクス";
        }

        private static void DrawResourceBars(TennisAthleteController athlete)
        {
            if (athlete == null)
            {
                return;
            }

            float scale = HudPreferences.Scale;
            float width = Mathf.Clamp(Screen.width * 0.2f, 210f, 390f) * scale;
            float x = Screen.safeArea.x + 24f * scale;
            float y = Screen.safeArea.yMax - 86f * scale;
            Color staminaColor = athlete.Stamina.IsExhausted
                ? PrideCourtUiTheme.Magenta
                : athlete.Stamina.Normalized < 0.3f ? PrideCourtUiTheme.Yellow : PrideCourtUiTheme.Cyan;

            PrideCourtUiTheme.DrawBar(new Rect(x, y, width, 21f * scale), athlete.Stamina.Normalized, staminaColor,
                athlete.Stamina.IsExhausted ? "スタミナ / 疲労" : "スタミナ", scale);
            PrideCourtUiTheme.DrawBar(new Rect(x, y + 26f * scale, width, 18f * scale), athlete.SpecialGauge.Normalized,
                PrideCourtUiTheme.Yellow, "スペシャル", scale);
        }
    }
}
