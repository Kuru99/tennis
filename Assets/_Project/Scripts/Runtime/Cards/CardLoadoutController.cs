using System.Collections.Generic;
using System.Linq;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Input;
using UnityEngine;
using PrideCourt.Presentation;

namespace PrideCourt.Cards
{
    public sealed class CardLoadoutController : MonoBehaviour
    {
        private const float LongPressSeconds = 0.3f;
        private const float HandCardWidth = 96f;
        private const float HandCardHeight = 142f;
        private const float HandCardGap = 8f;
        private const float HandRightMargin = 20f;
        private const float HandTopMargin = 18f;
        private const float PreparationPanelWidthRatio = 0.58f;
        private const float PreparationPanelMaxWidth = 560f;
        private const float PreparationPanelHeight = 136f;
        private const float PreparationPanelTopRatio = 0.38f;
        private const float PreparationButtonHeight = 54f;
        private const float PreparationDiscardHeight = 48f;
        private const float PreparationControlGap = 10f;
        // Android touch targets need a little more breathing room than the PC HUD.
        // Keep the multiplier local to the card presentation so the shared HUD scale
        // preference and the PC layout remain unchanged.
        private const float AndroidCardScaleMultiplier = 1.25f;

        [SerializeField] private TennisAthleteController athlete;
        [SerializeField] private TennisMatchController match;
        [SerializeField] private bool isCpu;

        private DeckState deck;
        private DeckState rewardDeck;
        private CardId? pendingNormal;
        private CardId? pendingReward;
        private CardId? specialEncoreCard;
        private float cardHeldSince;
        private bool selectionOpened;
        private float nextCpuCardDecision;
        private Texture2D characterCardAtlas;
        private readonly Dictionary<CardId, Texture2D> cardArt = new Dictionary<CardId, Texture2D>();
        private bool networkReplica;

        public CardHandState Hand { get; } = new CardHandState();
        public bool UsedCardThisPoint { get; private set; }
        public bool IsPreparationReady => !pendingNormal.HasValue && !pendingReward.HasValue;
        public CardId? PendingCard => pendingReward ?? pendingNormal;
        public int DrawCount => deck?.DrawCount ?? 0;
        public int DiscardCount => deck?.DiscardCount ?? 0;

        private void Awake()
        {
            characterCardAtlas = Resources.Load<Texture2D>("CharacterCardAtlas");
            foreach (CardId card in CardCatalog.All)
            {
                Texture2D texture = Resources.Load<Texture2D>("CardArt/" + card);
                if (texture != null) cardArt[card] = texture;
            }
            InitializeIfNeeded();
        }

        public void Configure(TennisAthleteController configuredAthlete, TennisMatchController configuredMatch, bool configuredCpu)
        {
            athlete = configuredAthlete;
            match = configuredMatch;
            isCpu = configuredCpu;
            InitializeIfNeeded();
        }

        public void SetCpuControlled(bool value)
        {
            isCpu = value;
        }

        public void SetNetworkReplica(bool value)
        {
            networkReplica = value;
            selectionOpened = false;
        }

        public LanCardState CaptureLanState()
        {
            return new LanCardState
            {
                Hand = Hand.Cards.ToArray(),
                ActiveIndex = Hand.ActiveIndex,
                UsedThisPoint = UsedCardThisPoint,
                HasPending = PendingCard.HasValue,
                Pending = PendingCard.GetValueOrDefault()
            };
        }

        public void ApplyLanState(LanCardState state)
        {
            networkReplica = true;
            Hand.Restore(state.Hand, state.ActiveIndex);
            UsedCardThisPoint = state.UsedThisPoint;
            pendingNormal = state.HasPending ? state.Pending : null;
            pendingReward = null;
            specialEncoreCard = null;
            selectionOpened = false;
        }

        private void InitializeIfNeeded()
        {
            if (deck != null || athlete == null) return;
            int seed = athlete.Side == CourtSide.Near ? 7411 : 9137;
            deck = new DeckState(CardCatalog.BuildPreset(athlete.Identity), seed);
            rewardDeck = new DeckState(CardCatalog.BuildCharacterRewardDeck(athlete.Identity), seed + 1);
            DrawInitialHand();
        }

        private void DrawInitialHand()
        {
            List<CardId> opening = new List<CardId>(5);
            for (int i = 0; i < 5; i++) opening.Add(deck.Draw());
            for (int i = 0; i < CardHandState.Capacity; i++) Hand.TryAdd(opening[i]);
            deck.ReturnAndShuffle(new[] { opening[3], opening[4] });
        }

        public void RebuildDeck(IReadOnlyList<CardId> cards)
        {
            if (athlete == null || cards == null || cards.Count != 16) return;
            int seed = athlete.Side == CourtSide.Near ? 7411 : 9137;
            deck = new DeckState(cards.ToArray(), seed);
            rewardDeck = new DeckState(CardCatalog.BuildCharacterRewardDeck(athlete.Identity), seed + 1);
            Hand.Clear();
            pendingNormal = null;
            pendingReward = null;
            specialEncoreCard = null;
            DrawInitialHand();
        }

        public void BeginPointPreparation(bool grantCharacterReward)
        {
            pendingNormal = deck.Draw();
            pendingReward = grantCharacterReward && !Hand.HasCharacterCard ? rewardDeck.Draw() : null;
            if (Hand.Count < CardHandState.Capacity)
            {
                AcceptPendingIntoEmptySlot();
            }

            if (isCpu)
            {
                ResolveCpuPreparation();
            }
        }

        public void ForcePreparationReady()
        {
            DiscardPending();
        }

        public void BeginPoint()
        {
            UsedCardThisPoint = false;
            athlete.ClearExpiredPointEffects();
        }

        public void ProcessCommand(TennisCommand command)
        {
            if (match == null || !match.CardsAllowed || Hand.Count == 0)
            {
                selectionOpened = false;
                return;
            }

            if (command.DirectCardPressed)
            {
                int directIndex = command.DirectCardSlot;
                if (directIndex < 0)
                {
                    TryGetHandIndex(command.CardPointer, out directIndex);
                }

                if (directIndex >= 0 && directIndex < Hand.Count)
                {
                    Hand.SetActive(directIndex);
                    UseActiveCard();
                }

                selectionOpened = false;
                return;
            }

            if (command.CardDown)
            {
                cardHeldSince = Time.unscaledTime;
                selectionOpened = false;
            }

            if (command.CardHeld && Time.unscaledTime - cardHeldSince >= LongPressSeconds)
            {
                selectionOpened = true;
                if (TryGetSelectedIndex(command.CardPointer, out int hoveredIndex))
                {
                    Hand.SetActive(hoveredIndex);
                }
            }

            if (!command.CardReleased)
            {
                return;
            }

            if (selectionOpened)
            {
                if (TryGetSelectedIndex(command.CardPointer, out int selectedIndex))
                {
                    Hand.SetActive(selectedIndex);
                    UseActiveCard();
                }
                selectionOpened = false;
            }
            else
            {
                UseActiveCard();
            }
        }

        public void ProcessPreparationCommand(TennisCommand command)
        {
            if (networkReplica || match == null || match.Phase != MatchPhase.CardSelection || IsPreparationReady)
            {
                return;
            }

            if (command.PreparationChoice >= 0)
            {
                ReplaceWithPending(command.PreparationChoice);
            }
            else if (command.PreparationChoice == -1)
            {
                DiscardPending();
            }
        }

        public void UseActiveCard()
        {
            if (!match.CardsAllowed || Hand.Count == 0) return;
            CardId card = Hand.ConsumeActive(out bool fromRewardDeck);
            if (fromRewardDeck) rewardDeck.Discard(card);
            else deck.Discard(card);
            UsedCardThisPoint = true;
            CardDefinition definition = CardCatalog.Get(card);
            Apply(card);
            FlushSpecialEncore();
            CardActivationEffect.Play(card, athlete);
            PrideCourtAudio.Instance?.PlayCard(definition.Category, definition.IsCharacterCard);
            match.NotifyCardUsed(athlete.Side, definition.DisplayName);
        }

        private void Apply(CardId card)
        {
            switch (card)
            {
                case CardId.AccelStep:
                    athlete.ApplyPersonalBuff(1.2f, 1f, 1f, 5f);
                    break;
                case CardId.EcoRun:
                    athlete.ApplyPersonalBuff(1f, 0.5f, 1f, 6f);
                    break;
                case CardId.RecoveryPulse:
                    athlete.Stamina.Restore(35f);
                    break;
                case CardId.SpinBoost:
                    athlete.QueueNextShotModifier(1.25f, 1f);
                    break;
                case CardId.GaugeCharge:
                    athlete.SpecialGauge.Add(15f);
                    break;
                case CardId.GripCourt:
                case CardId.SlipCourt:
                case CardId.HighBounce:
                    match.ApplyCourtCard(card);
                    break;
                case CardId.FlashStep:
                    athlete.ApplyPersonalBuff(1.3f, 1f, 1f, 5f);
                    break;
                case CardId.TailFeint:
                    athlete.QueueNextShotModifier(1.35f, 0.7f);
                    break;
                case CardId.RailBoost:
                    athlete.ApplyPersonalBuff(1.2f, 0.75f, 1f, 5f);
                    break;
                case CardId.AnchorCore:
                    athlete.ApplyPersonalBuff(1f, 1f, 0.5f, 6f);
                    break;
                case CardId.LeafVeil:
                    match.ApplyVisualDisruption(athlete.Side, 1.6f);
                    athlete.PrepareLuciaFinisher(3.2f);
                    break;
                case CardId.MischiefCurve:
                    athlete.QueueTrickShot(1f);
                    athlete.PrepareLuciaFinisher(3.2f);
                    break;
                case CardId.TimeTease:
                    match.ApplyTimeDisruption(athlete.Side, 0.95f);
                    athlete.PrepareLuciaFinisher(3.2f);
                    break;
                case CardId.DragonGrace:
                    athlete.ApplyShotBuff(1.1f, 0.72f, 6f);
                    break;
                case CardId.NobleRetake:
                    athlete.GrantMistakeGuard(1);
                    break;
                case CardId.DragonAwakening:
                    athlete.ArmDragonAwakening();
                    break;
                case CardId.JetIgnition:
                    // Zephyr gets overwhelming burst speed, but the hotter engines consume
                    // more stamina. This keeps acceleration a commitment instead of a free win.
                    athlete.ApplyMobilityBuff(1.3f, 1.2f, 1.2f, 4f);
                    break;
                case CardId.VectorWing:
                    athlete.QueueDiveAssist(0.6f, 0.48f);
                    break;
                case CardId.AirBrake:
                    athlete.ApplyMobilityBuff(1.05f, 0.75f, 1.55f, 5f);
                    break;
                case CardId.LeafMasquerade:
                    match.ApplyVisualDisruption(athlete.Side, 1.35f);
                    break;
                case CardId.BorrowedForm:
                    athlete.QueueShotDecoy();
                    break;
                case CardId.FalseTell:
                    athlete.QueueFalseTell();
                    break;
            }
        }

        public void GrantSpecialEncore()
        {
            if (athlete == null || athlete.Identity != AthleteIdentity.Lucia || rewardDeck == null) return;
            CardId encore = rewardDeck.Draw();
            if (!Hand.TryAdd(encore, true)) specialEncoreCard = encore;
        }

        private void FlushSpecialEncore()
        {
            if (!specialEncoreCard.HasValue || Hand.Count >= CardHandState.Capacity) return;
            Hand.TryAdd(specialEncoreCard.Value, true);
            specialEncoreCard = null;
        }

        private void Update()
        {
            if (networkReplica) return;
            if (match == null) return;
            if (isCpu)
            {
                if (match.CardsAllowed && Hand.Count > 0 && Time.time >= nextCpuCardDecision)
                {
                    nextCpuCardDecision = Time.time + Random.Range(3.5f, 6.5f);
                    if (athlete.Stamina.Normalized < 0.45f || Random.value < 0.4f) UseActiveCard();
                }
                return;
            }

        }

        private void ResolveCpuPreparation()
        {
            while (PendingCard.HasValue)
            {
                if (Hand.Count < CardHandState.Capacity) AcceptPendingIntoEmptySlot();
                else ReplaceWithPending(Random.Range(0, Hand.Count));
            }
        }

        private void AcceptPendingIntoEmptySlot()
        {
            while (Hand.Count < CardHandState.Capacity && PendingCard.HasValue)
            {
                CardId card = PendingCard.Value;
                bool fromReward = pendingReward.HasValue && pendingReward.Value.Equals(card);
                Hand.TryAdd(card, fromReward);
                ClearPending(card);
            }
        }

        private void ReplaceWithPending(int index)
        {
            if (!PendingCard.HasValue || index < 0 || index >= Hand.Count) return;
            CardId incoming = PendingCard.Value;
            bool incomingFromReward = pendingReward.HasValue && pendingReward.Value.Equals(incoming);
            CardId outgoing = Hand.Replace(index, incoming, incomingFromReward, out bool outgoingFromReward);
            if (outgoingFromReward) rewardDeck.Discard(outgoing);
            else deck.Discard(outgoing);
            ClearPending(incoming);
            if (Hand.Count < CardHandState.Capacity) AcceptPendingIntoEmptySlot();
        }

        private void DiscardPending()
        {
            if (pendingReward.HasValue)
            {
                rewardDeck.Discard(pendingReward.Value);
                pendingReward = null;
            }
            if (pendingNormal.HasValue)
            {
                deck.Discard(pendingNormal.Value);
                pendingNormal = null;
            }
        }

        private void ClearPending(CardId accepted)
        {
            if (pendingReward.HasValue && pendingReward.Value.Equals(accepted)) pendingReward = null;
            else if (pendingNormal.HasValue && pendingNormal.Value.Equals(accepted)) pendingNormal = null;
        }

        private void OnGUI()
        {
            if (athlete == null || match == null || athlete.Side != match.LocalPlayerSide) return;
            if (match.IsGameplayPaused) return;
            float scale = GetCardScale();
            if (!HudPreferences.ShowCards) return;

            for (int i = 0; i < Hand.Count; i++)
            {
                CardDefinition definition = CardCatalog.Get(Hand.Cards[i]);
                Rect cardRect = GetHandCardRect(i, scale);
                DrawCard(cardRect, Hand.Cards[i], i, i == Hand.ActiveIndex, definition, scale);
            }

            if (selectionOpened)
            {
                Rect handBounds = GetHandBounds(scale);
                GUI.Label(new Rect(handBounds.x, handBounds.yMax + 4f * scale, handBounds.width, 24f * scale),
                    "スライドで選択 / 離して発動",
                    PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Cyan));
            }

            if (match.Phase == MatchPhase.CardSelection && PendingCard.HasValue)
            {
                DrawPreparationOverlay(Screen.safeArea, scale);
            }
        }

        private void DrawPreparationOverlay(Rect safe, float scale)
        {
            string pending = CardCatalog.Get(PendingCard.Value).DisplayName;
            float panelWidth = Mathf.Min(safe.width * PreparationPanelWidthRatio,
                PreparationPanelMaxWidth * scale);
            float panelHeight = Mathf.Min(PreparationPanelHeight * scale, safe.height * 0.28f);
            float buttonHeight = Mathf.Min(Mathf.Max(PreparationButtonHeight * scale, 40f), safe.height * 0.12f);
            float discardHeight = Mathf.Min(Mathf.Max(PreparationDiscardHeight * scale, 38f), safe.height * 0.1f);
            float gap = PreparationControlGap * scale;
            float totalHeight = panelHeight + gap + buttonHeight + gap + discardHeight;
            float minimumPanelY = GetHandBounds(scale).yMax + 8f * scale;
            float preferredPanelY = safe.y + safe.height * PreparationPanelTopRatio;
            float maximumPanelY = safe.y + Mathf.Max(8f, safe.height - totalHeight - 8f);
            float panelY = Mathf.Clamp(preferredPanelY, minimumPanelY, maximumPanelY);
            Rect drawPanel = new Rect(safe.center.x - panelWidth * 0.5f, panelY, panelWidth, panelHeight);

            PrideCourtUiTheme.DrawPanel(drawPanel, PrideCourtUiTheme.Tone.Magenta, scale, "新しいカード / 選択");

            float contentInset = 16f * scale;
            float contentWidth = drawPanel.width - contentInset * 2f;
            float nameY = drawPanel.y + 35f * scale;
            GUI.Label(new Rect(drawPanel.x + contentInset, nameY, contentWidth, 26f * scale), pending,
                PrideCourtUiTheme.Label(scale, 14, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper, true));
            GUI.Label(new Rect(drawPanel.x + contentInset, drawPanel.y + 65f * scale,
                    contentWidth, Mathf.Max(20f, drawPanel.yMax - (drawPanel.y + 65f * scale) - 8f * scale)),
                "[1][2][3] 入れ替え  /  [ENTER] 捨てる",
                PrideCourtUiTheme.Label(scale, 12, TextAnchor.MiddleCenter, PrideCourtUiTheme.Muted, true));

            float buttonGap = 8f * scale;
            float buttonRowWidth = Mathf.Max(0f, drawPanel.width - contentInset * 2f);
            float buttonWidth = (buttonRowWidth - buttonGap * 2f) / 3f;
            float buttonY = drawPanel.yMax + gap;
            float buttonStartX = drawPanel.center.x - buttonRowWidth * 0.5f;
            for (int i = 0; i < Hand.Count; i++)
            {
                Rect buttonRect = new Rect(buttonStartX + i * (buttonWidth + buttonGap), buttonY,
                    buttonWidth, buttonHeight);
                bool compactButton = buttonWidth < 128f * scale;
                string buttonLabel = compactButton
                    ? "枠" + (i + 1) + "\n入替"
                    : "枠" + (i + 1) + "と入れ替え";
                if (GUI.Button(buttonRect, buttonLabel,
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Cyan, scale, compactButton ? 12 : 14)))
                {
                    LocalPreparationInputRouter.Queue(i);
                }
            }

            float discardWidth = Mathf.Min(160f * scale, buttonRowWidth);
            Rect discardRect = new Rect(drawPanel.center.x - discardWidth * 0.5f,
                buttonY + buttonHeight + gap, discardWidth, discardHeight);
            if (GUI.Button(discardRect, "捨てる",
                    PrideCourtUiTheme.Button(PrideCourtUiTheme.Tone.Neutral, scale, 16)))
            {
                LocalPreparationInputRouter.Queue(-1);
            }
        }

        private bool TryGetHandIndex(Vector2 normalizedPointer, out int index)
        {
            float scale = GetCardScale();
            float guiY = Screen.height - normalizedPointer.y * Screen.height;
            Vector2 guiPointer = new Vector2(normalizedPointer.x * Screen.width, guiY);
            float horizontalPadding = HandCardGap * 0.5f * scale;
            float verticalPadding = 10f * scale;

            for (int i = 0; i < Hand.Count; i++)
            {
                Rect cardRect = GetHandCardRect(i, scale);
                Rect hitRect = Rect.MinMaxRect(
                    cardRect.xMin - horizontalPadding,
                    cardRect.yMin - verticalPadding,
                    cardRect.xMax + horizontalPadding,
                    cardRect.yMax + verticalPadding);
                if (hitRect.Contains(guiPointer))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public int ResolveDirectCardSlot(Vector2 normalizedPointer)
        {
            return TryGetHandIndex(normalizedPointer, out int index) ? index : -1;
        }

        private bool TryGetSelectedIndex(Vector2 pointer, out int index)
        {
            if (pointer.y < 0f)
            {
                index = Mathf.Clamp(Mathf.FloorToInt(pointer.x * Hand.Count), 0, Hand.Count - 1);
                return Hand.Count > 0;
            }

            return TryGetHandIndex(pointer, out index);
        }

        private Rect GetHandBounds(float scale)
        {
            float width = Hand.Count * HandCardWidth * scale + Mathf.Max(0, Hand.Count - 1) * HandCardGap * scale;
            float x = Screen.safeArea.xMax - width - HandRightMargin * scale;
            float y = Screen.height - Screen.safeArea.yMax + HandTopMargin * scale;
            return new Rect(x, y, width, HandCardHeight * scale);
        }

        private Rect GetHandCardRect(int index, float scale)
        {
            Rect bounds = GetHandBounds(scale);
            return new Rect(
                bounds.x + index * (HandCardWidth + HandCardGap) * scale,
                bounds.y,
                HandCardWidth * scale,
                HandCardHeight * scale);
        }

#if UNITY_EDITOR
        public Vector2 GetHandCardPointerForValidation(int index)
        {
            Rect cardRect = GetHandCardRect(Mathf.Clamp(index, 0, Mathf.Max(0, Hand.Count - 1)), GetCardScale());
            return new Vector2(
                cardRect.center.x / Mathf.Max(1f, Screen.width),
                (Screen.height - cardRect.center.y) / Mathf.Max(1f, Screen.height));
        }
#endif

        private void DrawCard(Rect rect, CardId card, int slotIndex, bool active, CardDefinition definition, float scale)
        {
            Color accent = active ? PrideCourtUiTheme.Magenta : definition.Category switch
            {
                CardCategory.PersonalBuff => PrideCourtUiTheme.Cyan,
                CardCategory.Instant => PrideCourtUiTheme.Magenta,
                CardCategory.NextShot => PrideCourtUiTheme.Violet,
                _ => PrideCourtUiTheme.Yellow
            };
            GUI.Box(rect, GUIContent.none, PrideCourtUiTheme.CardPanel(accent, scale));
            if (active)
            {
                PrideCourtUiTheme.DrawTag(new Rect(rect.x + 6f * scale, rect.y - 6f * scale,
                    56f * scale, 22f * scale), "選択中", PrideCourtUiTheme.Tone.Magenta, scale);
            }

            GUI.Label(new Rect(rect.xMax - 29f * scale, rect.y + 5f * scale, 22f * scale, 22f * scale),
                (slotIndex + 1).ToString(), PrideCourtUiTheme.Heading(scale * 0.55f, TextAnchor.MiddleCenter, accent));

            Rect artRect = new Rect(rect.x + 7f * scale, rect.y + 24f * scale,
                rect.width - 14f * scale, 82f * scale);
            if (cardArt.TryGetValue(card, out Texture2D texture) && texture != null)
            {
                GUI.DrawTexture(artRect, texture, ScaleMode.ScaleAndCrop, true);
            }
            else if (definition.IsCharacterCard && characterCardAtlas != null)
            {
                float u = card switch
                {
                    CardId.FlashStep => 0f,
                    CardId.TailFeint => 0.25f,
                    CardId.RailBoost => 0.5f,
                    _ => 0.75f
                };
                GUI.DrawTextureWithTexCoords(artRect, characterCardAtlas, new Rect(u, 0f, 0.25f, 1f), true);
            }
            else
            {
                Color previous = GUI.color;
                GUI.color = accent;
                GUI.DrawTexture(artRect, Texture2D.whiteTexture);
                GUI.color = PrideCourtUiTheme.Ink;
                GUI.DrawTexture(new Rect(artRect.center.x - 3f * scale, artRect.y + 12f * scale,
                    6f * scale, artRect.height - 24f * scale), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(artRect.x + 12f * scale, artRect.center.y - 3f * scale,
                    artRect.width - 24f * scale, 6f * scale), Texture2D.whiteTexture);
                GUI.color = previous;
            }

            GUI.Label(new Rect(rect.x + 7f * scale, artRect.yMax + 5f * scale,
                    rect.width - 14f * scale, rect.yMax - artRect.yMax - 10f * scale),
                definition.DisplayName,
                PrideCourtUiTheme.Label(scale, 11, TextAnchor.MiddleCenter, PrideCourtUiTheme.Paper, true));
        }

        private static float GetCardScale()
        {
            return HudPreferences.Scale *
                (Application.platform == RuntimePlatform.Android ? AndroidCardScaleMultiplier : 1f);
        }
    }
}
