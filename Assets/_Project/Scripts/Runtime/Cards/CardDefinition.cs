using System;
using System.Collections.Generic;
using PrideCourt.Domain;

namespace PrideCourt.Cards
{
    public readonly struct CardDefinition
    {
        public CardDefinition(CardId id, string displayName, CardCategory category, float duration, AthleteIdentity? owner = null)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Duration = duration;
            Owner = owner;
        }

        public CardId Id { get; }
        public string DisplayName { get; }
        public CardCategory Category { get; }
        public float Duration { get; }
        public AthleteIdentity? Owner { get; }
        public bool IsCharacterCard => Owner.HasValue;
    }

    public static class CardCatalog
    {
        private static readonly CardId[] AllCardIds =
        {
            CardId.AccelStep, CardId.EcoRun, CardId.RecoveryPulse, CardId.SpinBoost,
            CardId.GaugeCharge, CardId.GripCourt, CardId.SlipCourt, CardId.HighBounce,
            CardId.FlashStep, CardId.TailFeint, CardId.RailBoost, CardId.AnchorCore
        };

        private static readonly Dictionary<CardId, CardDefinition> Definitions = new Dictionary<CardId, CardDefinition>
        {
            { CardId.AccelStep, new CardDefinition(CardId.AccelStep, "アクセルステップ", CardCategory.PersonalBuff, 5f) },
            { CardId.EcoRun, new CardDefinition(CardId.EcoRun, "エコラン", CardCategory.PersonalBuff, 6f) },
            { CardId.RecoveryPulse, new CardDefinition(CardId.RecoveryPulse, "リカバリーパルス", CardCategory.Instant, 0f) },
            { CardId.SpinBoost, new CardDefinition(CardId.SpinBoost, "スピンブースト", CardCategory.NextShot, 0f) },
            { CardId.GaugeCharge, new CardDefinition(CardId.GaugeCharge, "ゲージチャージ", CardCategory.Instant, 0f) },
            { CardId.GripCourt, new CardDefinition(CardId.GripCourt, "グリップコート", CardCategory.Court, -1f) },
            { CardId.SlipCourt, new CardDefinition(CardId.SlipCourt, "スリップコート", CardCategory.Court, -1f) },
            { CardId.HighBounce, new CardDefinition(CardId.HighBounce, "ハイバウンド", CardCategory.Court, -1f) },
            { CardId.FlashStep, new CardDefinition(CardId.FlashStep, "フラッシュステップ", CardCategory.PersonalBuff, 5f, AthleteIdentity.Lux) },
            { CardId.TailFeint, new CardDefinition(CardId.TailFeint, "テイルフェイント", CardCategory.NextShot, 0f, AthleteIdentity.Lux) },
            { CardId.RailBoost, new CardDefinition(CardId.RailBoost, "レールブースト", CardCategory.PersonalBuff, 5f, AthleteIdentity.Bastion) },
            { CardId.AnchorCore, new CardDefinition(CardId.AnchorCore, "アンカーコア", CardCategory.PersonalBuff, 6f, AthleteIdentity.Bastion) }
        };

        public static CardDefinition Get(CardId id)
        {
            return Definitions[id];
        }

        public static IReadOnlyList<CardId> All => AllCardIds;

        public static bool IsAllowedFor(CardId id, AthleteIdentity identity)
        {
            AthleteIdentity? owner = Get(id).Owner;
            return !owner.HasValue || owner.Value == identity;
        }

        public static IReadOnlyList<CardId> BuildPreset(AthleteIdentity identity)
        {
            List<CardId> deck = new List<CardId>(16)
            {
                CardId.AccelStep, CardId.AccelStep,
                CardId.EcoRun, CardId.EcoRun,
                CardId.RecoveryPulse, CardId.RecoveryPulse, CardId.RecoveryPulse,
                CardId.SpinBoost, CardId.SpinBoost,
                CardId.GaugeCharge, CardId.GaugeCharge,
                CardId.GripCourt,
                CardId.SlipCourt,
                CardId.HighBounce
            };

            if (identity == AthleteIdentity.Lux)
            {
                deck.Add(CardId.FlashStep);
                deck.Add(CardId.TailFeint);
            }
            else
            {
                deck.Add(CardId.RailBoost);
                deck.Add(CardId.AnchorCore);
            }

            return deck;
        }

        public static IReadOnlyList<CardId> BuildCharacterRewardDeck(AthleteIdentity identity)
        {
            return identity == AthleteIdentity.Lux
                ? new[] { CardId.FlashStep, CardId.FlashStep, CardId.TailFeint, CardId.TailFeint }
                : new[] { CardId.RailBoost, CardId.RailBoost, CardId.AnchorCore, CardId.AnchorCore };
        }
    }
}
