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
        private const float GameAwardPopupSeconds = 1.2f;
        private const float GameAwardPopupExitSeconds = 0.2f;

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
        private float visualDisruptionRemaining;
        private int visualDisruptionSeed;
        private float gameAwardPopupRemaining;
        private CourtSide gameAwardPopupWinner;

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
        public int ValidRallyReturns => validRallyReturns;
        public bool IsGameAwardPopupVisible => gameAwardPopupRemaining > 0f;
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
            if (visualDisruptionRemaining > 0f) visualDisruptionRemaining -= Time.unscaledDeltaTime;
            if (gameAwardPopupRemaining > 0f)
                gameAwardPopupRemaining = Mathf.Max(0f, gameAwardPopupRemaining - Time.unscaledDeltaTime);
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
                NearGames = score.NearGames,
                FarGames = score.FarGames,
                GamesToWin = score.GamesToWin,
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
            bool hadStarted = HasStarted;
            int previousNearGames = score.NearGames;
            int previousFarGames = score.FarGames;
            bool remoteGameCompleted = state.NearGames > previousNearGames || state.FarGames > previousFarGames;
            networkReplica = true;
            HasStarted = state.HasStarted;
            Phase = state.Phase;
            score.Restore(state.NearPoints, state.FarPoints, state.NearGames, state.FarGames,
                state.CompletedPoints, state.GamesToWin);
            phaseTimer = state.PhaseTimeRemaining;
            serveHasBeenStruck = state.ServeHasBeenStruck;
            isGameplayPaused = state.IsGameplayPaused;
            Time.timeScale = isGameplayPaused ? 0f : 1f;
            statusMessage = state.StatusMessage ?? string.Empty;
            activeCourtCard = state.HasActiveCourtCard ? state.ActiveCourtCard : null;
            if (hadStarted && remoteGameCompleted)
            {
                BeginGameAwardPopup(state.NearGames > previousNearGames ? CourtSide.Near : CourtSide.Far);
            }
            nearAthlete.ApplyLanState(state.NearAthlete);
            farAthlete.ApplyLanState(state.FarAthlete);
            ball.ApplyLanState(state.Ball);
            nearCards?.ApplyLanState(state.NearCards);
            farCards?.ApplyLanState(state.FarCards);
        }

        public void StartNewMatch()
        {
            StartNewMatch(score.GamesToWin);
        }

        public void StartNewMatch(int gamesToWin)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            isGameplayPaused = false;
            score.ConfigureGamesToWin(gamesToWin);
            score.Reset();
            nearAthlete?.ResetSpecialForNewGame();
            farAthlete?.ResetSpecialForNewGame();
            serveFaults = 0;
            validRallyReturns = 0;
            serveHasBeenStruck = false;
            activeCourtCard = null;
            grantNearReward = false;
            grantFarReward = false;
            timingMessage = string.Empty;
            gameAwardPopupRemaining = 0f;
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
            gameAwardPopupRemaining = 0f;
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

        public void ApplyVisualDisruption(CourtSide source, float duration)
        {
            TennisAthleteController target = AthleteFor(source.Opposite());
            target?.ApplyVisualDisruption(duration);
            if (target != null && target.Side == LocalPlayerSide)
            {
                visualDisruptionRemaining = Mathf.Max(visualDisruptionRemaining, duration);
                visualDisruptionSeed = UnityEngine.Random.Range(1, 10000);
            }
        }

        public void ApplyTimeDisruption(CourtSide source, float duration)
        {
            AthleteFor(source.Opposite())?.ApplyTimeDisruption(duration);
        }

        public bool IsUnderPressure(CourtSide side)
        {
            int own = side == CourtSide.Near ? score.NearPoints : score.FarPoints;
            int opponent = side == CourtSide.Near ? score.FarPoints : score.NearPoints;
            return opponent > own;
        }

        public void NotifyMistakeGuard(CourtSide side)
        {
            statusMessage = AthleteLabel(side) + "のノーブル・リテイク — 判定を修正";
        }

        public void NotifyDragonAwakening(CourtSide side)
        {
            statusMessage = AthleteLabel(side) + " — 竜醒";
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
            specialCutInTexture = Resources.Load<Texture2D>("SpecialCutIns/" + athlete.Identity) ??
                                  Resources.Load<Texture2D>("SpecialCutIn");
            statusMessage = AthleteCatalog.Get(athlete.Identity).SpecialName;
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
            int gamesBeforePoint = score.GetGames(winner);
            score.AwardPoint(winner);
            bool gameCompleted = score.GetGames(winner) > gamesBeforePoint;
            if (gameCompleted && !score.IsMatchOver)
            {
                nearAthlete?.ResetSpecialForNewGame();
                farAthlete?.ResetSpecialForNewGame();
            }
            PrideCourtAudio.Instance?.PlayPoint();
            bool rewardEligible = validRallyReturns >= 1;
            grantNearReward = rewardEligible && nearCards != null && !nearCards.UsedCardThisPoint && !nearCards.Hand.HasCharacterCard;
            grantFarReward = rewardEligible && farCards != null && !farCards.UsedCardThisPoint && !farCards.Hand.HasCharacterCard;
            activeCourtCard = null;
            statusMessage = gameCompleted
                ? AthleteLabel(winner) + "がゲーム獲得 — " + reason
                : AthleteLabel(winner) + "のポイント — " + reason;
            if (gameCompleted)
            {
                BeginGameAwardPopup(winner);
            }
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
            Rect scorePanel = new Rect(safe.center.x - scoreWidth * 0.5f, safe.y + 14f * scale, scoreWidth, 84f * scale);
            PrideCourtUiTheme.DrawPanel(scorePanel, PrideCourtUiTheme.Tone.Cyan, scale);
            PrideCourtUiTheme.DrawTag(new Rect(scorePanel.x + 12f * scale, scorePanel.y - 5f * scale, 104f * scale, 23f * scale),
                PrideCourtUiTheme.FormatGameStars(score.GamesToWin, score.GamesToWin) + " 先取", PrideCourtUiTheme.Tone.Magenta, scale);
            GUI.Label(new Rect(scorePanel.x + 8f * scale, scorePanel.y + 5f * scale, scorePanel.width - 16f * scale, 42f * scale),
                LocalPlayerSide == CourtSide.Near
                    ? $"{score.NearPoints}  —  {score.FarPoints}"
                    : $"{score.FarPoints}  —  {score.NearPoints}",
                PrideCourtUiTheme.Heading(scale, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(scorePanel.x + 8f * scale, scorePanel.y + 46f * scale, scorePanel.width - 16f * scale, 28f * scale),
                LocalPlayerSide == CourtSide.Near
                    ? $"ゲーム  {PrideCourtUiTheme.FormatGameStars(score.NearGames, score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(score.FarGames, score.GamesToWin)}"
                    : $"ゲーム  {PrideCourtUiTheme.FormatGameStars(score.FarGames, score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(score.NearGames, score.GamesToWin)}",
                PrideCourtUiTheme.Label(scale, 13, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted));

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

            if (visualDisruptionRemaining > 0f)
            {
                DrawVisualDisruption(safe, scale);
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

            if (gameAwardPopupRemaining > 0f)
            {
                DrawGameAwardPopup(safe, scale);
            }
        }

        private void BeginGameAwardPopup(CourtSide winner)
        {
            gameAwardPopupWinner = winner;
            gameAwardPopupRemaining = GameAwardPopupSeconds;
        }

        private void DrawGameAwardPopup(Rect safe, float scale)
        {
            float elapsed = GameAwardPopupSeconds - gameAwardPopupRemaining;
            float enter = Mathf.Clamp01(elapsed / 0.16f);
            float exit = Mathf.Clamp01(gameAwardPopupRemaining / GameAwardPopupExitSeconds);
            float alpha = Mathf.Min(enter, exit);
            float emphasis = Mathf.Lerp(0.92f, 1f, 1f - (1f - enter) * (1f - enter));
            float width = Mathf.Min(safe.width * 0.78f, 760f * scale);
            float height = 238f * scale;
            Rect panel = new Rect(safe.center.x - width * emphasis * 0.5f,
                safe.center.y - height * emphasis * 0.5f,
                width * emphasis, height * emphasis);
            PrideCourtUiTheme.Tone tone = gameAwardPopupWinner == LocalPlayerSide
                ? PrideCourtUiTheme.Tone.Cyan
                : PrideCourtUiTheme.Tone.Yellow;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.34f * alpha);
            GUI.DrawTexture(safe, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            PrideCourtUiTheme.DrawPanel(panel, tone, scale, "GAME WON");
            PrideCourtUiTheme.DrawSolid(new Rect(panel.x + 34f * scale, panel.y + 38f * scale,
                    panel.width - 68f * scale, 3f * scale), PrideCourtUiTheme.ToneColor(tone));
            GUI.Label(new Rect(panel.x + 26f * scale, panel.y + 48f * scale,
                    panel.width - 52f * scale, 50f * scale), "ゲーム獲得",
                PrideCourtUiTheme.Heading(scale * 1.26f, TextAnchor.MiddleCenter,
                    PrideCourtUiTheme.ToneColor(tone)));
            GUI.Label(new Rect(panel.x + 26f * scale, panel.y + 104f * scale,
                    panel.width - 52f * scale, 34f * scale),
                AthleteLabel(gameAwardPopupWinner) + "  /  " + FormatLocalGameScore(),
                PrideCourtUiTheme.Label(scale, 16, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper));
            PrideCourtUiTheme.DrawTag(new Rect(panel.x + panel.width * 0.23f, panel.y + 154f * scale,
                    panel.width * 0.54f, 36f * scale), "次のゲームへ", PrideCourtUiTheme.Tone.Neutral, scale);
            GUI.color = previous;
        }

        private string FormatLocalGameScore()
        {
            return LocalPlayerSide == CourtSide.Near
                ? $"ゲーム {PrideCourtUiTheme.FormatGameStars(score.NearGames, score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(score.FarGames, score.GamesToWin)}"
                : $"ゲーム {PrideCourtUiTheme.FormatGameStars(score.FarGames, score.GamesToWin)}  —  {PrideCourtUiTheme.FormatGameStars(score.NearGames, score.GamesToWin)}";
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
            TennisAthleteController athlete = AthleteFor(side);
            return athlete == null ? string.Empty : AthleteCatalog.Get(athlete.Identity).DisplayName;
        }

        private TennisAthleteController AthleteFor(CourtSide side)
        {
            return side == CourtSide.Near ? nearAthlete : farAthlete;
        }

        private void DrawVisualDisruption(Rect safe, float scale)
        {
            float normalized = Mathf.Clamp01(visualDisruptionRemaining / 1.6f);
            Color previous = GUI.color;
            UnityEngine.Random.State previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(visualDisruptionSeed);
            for (int i = 0; i < 16; i++)
            {
                float width = UnityEngine.Random.Range(20f, 54f) * scale;
                float height = width * UnityEngine.Random.Range(0.28f, 0.5f);
                Rect leaf = new Rect(
                    safe.x + UnityEngine.Random.Range(0f, Mathf.Max(1f, safe.width - width)),
                    safe.y + UnityEngine.Random.Range(0f, Mathf.Max(1f, safe.height - height)),
                    width,
                    height);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(UnityEngine.Random.Range(-65f, 65f), leaf.center);
                Color leafColor = i % 3 == 0
                    ? new Color(1f, 0.12f, 0.62f, 0.32f * normalized)
                    : new Color(0.12f, 0.95f, 0.55f, 0.38f * normalized);
                GUI.color = leafColor;
                GUI.DrawTexture(leaf, Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            UnityEngine.Random.state = previousState;
            GUI.color = previous;
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
                PrideCourtUiTheme.Yellow,
                athlete.IsDragonAwakened ? "スペシャル / 竜醒" : athlete.SpecialGauge.IsReady ? "スペシャル / READY" : "スペシャル",
                scale);
        }
    }
}
