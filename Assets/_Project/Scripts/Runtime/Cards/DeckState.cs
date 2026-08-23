using System;
using System.Collections.Generic;
using PrideCourt.Domain;

namespace PrideCourt.Cards
{
    public sealed class DeckState
    {
        private readonly Random random;
        private readonly List<CardId> drawPile = new List<CardId>();
        private readonly List<CardId> discardPile = new List<CardId>();

        public DeckState(IEnumerable<CardId> cards, int seed)
        {
            random = new Random(seed);
            drawPile.AddRange(cards);
            Shuffle(drawPile);
        }

        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;

        public CardId Draw()
        {
            if (drawPile.Count == 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                Shuffle(drawPile);
            }

            if (drawPile.Count == 0)
            {
                throw new InvalidOperationException("The deck contains no cards.");
            }

            int last = drawPile.Count - 1;
            CardId card = drawPile[last];
            drawPile.RemoveAt(last);
            return card;
        }

        public void Discard(CardId card)
        {
            discardPile.Add(card);
        }

        public void ReturnAndShuffle(IEnumerable<CardId> cards)
        {
            drawPile.AddRange(cards);
            Shuffle(drawPile);
        }

        private void Shuffle(List<CardId> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int index = random.Next(i + 1);
                (cards[i], cards[index]) = (cards[index], cards[i]);
            }
        }
    }
}
