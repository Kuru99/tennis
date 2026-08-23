using System;
using System.Collections.Generic;
using PrideCourt.Domain;

namespace PrideCourt.Cards
{
    public sealed class CardHandState
    {
        public const int Capacity = 3;

        private readonly List<CardId> cards = new List<CardId>(Capacity);
        private readonly List<bool> rewardOrigins = new List<bool>(Capacity);

        public IReadOnlyList<CardId> Cards => cards;
        public int Count => cards.Count;
        public int ActiveIndex { get; private set; }
        public bool HasCharacterCard
        {
            get
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (CardCatalog.Get(cards[i]).IsCharacterCard) return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            cards.Clear();
            rewardOrigins.Clear();
            ActiveIndex = 0;
        }

        public CardId ActiveCard => cards.Count == 0 ? throw new InvalidOperationException("Hand is empty.") : cards[ActiveIndex];

        public bool TryAdd(CardId card, bool fromRewardDeck = false)
        {
            if (cards.Count >= Capacity) return false;
            cards.Add(card);
            rewardOrigins.Add(fromRewardDeck);
            ActiveIndex = Math.Min(ActiveIndex, cards.Count - 1);
            return true;
        }

        public CardId Replace(int index, CardId card, bool fromRewardDeck, out bool replacedFromRewardDeck)
        {
            if (index < 0 || index >= cards.Count) throw new ArgumentOutOfRangeException(nameof(index));
            CardId replaced = cards[index];
            replacedFromRewardDeck = rewardOrigins[index];
            cards[index] = card;
            rewardOrigins[index] = fromRewardDeck;
            return replaced;
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= cards.Count) return;
            ActiveIndex = index;
        }

        public CardId ConsumeActive(out bool fromRewardDeck)
        {
            CardId card = ActiveCard;
            fromRewardDeck = rewardOrigins[ActiveIndex];
            cards.RemoveAt(ActiveIndex);
            rewardOrigins.RemoveAt(ActiveIndex);
            ActiveIndex = cards.Count == 0 ? 0 : Math.Min(ActiveIndex, cards.Count - 1);
            return card;
        }

        public void Restore(IReadOnlyList<CardId> restoredCards, int activeIndex)
        {
            Clear();
            if (restoredCards != null)
            {
                for (int i = 0; i < restoredCards.Count && i < Capacity; i++)
                {
                    TryAdd(restoredCards[i]);
                }
            }
            ActiveIndex = cards.Count == 0 ? 0 : Math.Max(0, Math.Min(activeIndex, cards.Count - 1));
        }
    }
}
