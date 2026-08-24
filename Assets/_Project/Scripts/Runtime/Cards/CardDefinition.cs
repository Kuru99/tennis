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
            CardId.FlashStep, CardId.TailFeint, CardId.RailBoost, CardId.AnchorCore,
            CardId.LeafVeil, CardId.MischiefCurve, CardId.TimeTease,
            CardId.DragonGrace, CardId.NobleRetake, CardId.DragonAwakening,
            CardId.JetIgnition, CardId.VectorWing, CardId.AirBrake,
            CardId.LeafMasquerade, CardId.BorrowedForm, CardId.FalseTell
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
            { CardId.AnchorCore, new CardDefinition(CardId.AnchorCore, "アンカーコア", CardCategory.PersonalBuff, 6f, AthleteIdentity.Bastion) },
            { CardId.LeafVeil, new CardDefinition(CardId.LeafVeil, "木の葉隠れ", CardCategory.Instant, 1.6f, AthleteIdentity.Lucia) },
            { CardId.MischiefCurve, new CardDefinition(CardId.MischiefCurve, "フェアリー・ミスリード", CardCategory.NextShot, 0f, AthleteIdentity.Lucia) },
            { CardId.TimeTease, new CardDefinition(CardId.TimeTease, "ティック・トリック", CardCategory.Instant, 0.95f, AthleteIdentity.Lucia) },
            { CardId.DragonGrace, new CardDefinition(CardId.DragonGrace, "竜の加護", CardCategory.PersonalBuff, 6f, AthleteIdentity.Charlotte) },
            { CardId.NobleRetake, new CardDefinition(CardId.NobleRetake, "ノーブル・リテイク", CardCategory.Instant, 0f, AthleteIdentity.Charlotte) },
            { CardId.DragonAwakening, new CardDefinition(CardId.DragonAwakening, "紅竜覚醒", CardCategory.PersonalBuff, 9f, AthleteIdentity.Charlotte) },
            { CardId.JetIgnition, new CardDefinition(CardId.JetIgnition, "ジェット・イグニション", CardCategory.PersonalBuff, 4f, AthleteIdentity.Zephyr) },
            { CardId.VectorWing, new CardDefinition(CardId.VectorWing, "ベクター・ウイング", CardCategory.Instant, 0f, AthleteIdentity.Zephyr) },
            { CardId.AirBrake, new CardDefinition(CardId.AirBrake, "エアブレーキ", CardCategory.PersonalBuff, 5f, AthleteIdentity.Zephyr) },
            { CardId.LeafMasquerade, new CardDefinition(CardId.LeafMasquerade, "木の葉まぎれ", CardCategory.Instant, 1.35f, AthleteIdentity.Poko) },
            { CardId.BorrowedForm, new CardDefinition(CardId.BorrowedForm, "うつし身", CardCategory.NextShot, 0f, AthleteIdentity.Poko) },
            { CardId.FalseTell, new CardDefinition(CardId.FalseTell, "あべこべフォーム", CardCategory.NextShot, 0f, AthleteIdentity.Poko) }
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

            IReadOnlyList<CardId> characterCards = BuildCharacterPreset(identity);
            if (characterCards.Count == 3) deck.Remove(CardId.RecoveryPulse);
            deck.AddRange(characterCards);

            return deck;
        }

        public static IReadOnlyList<CardId> BuildCharacterRewardDeck(AthleteIdentity identity)
        {
            IReadOnlyList<CardId> characterCards = BuildCharacterPreset(identity);
            List<CardId> rewardCards = new List<CardId>(characterCards.Count * 2);
            for (int i = 0; i < characterCards.Count; i++)
            {
                rewardCards.Add(characterCards[i]);
                rewardCards.Add(characterCards[i]);
            }
            return rewardCards;
        }

        private static IReadOnlyList<CardId> BuildCharacterPreset(AthleteIdentity identity)
        {
            return identity switch
            {
                AthleteIdentity.Lux => new[] { CardId.FlashStep, CardId.TailFeint },
                AthleteIdentity.Bastion => new[] { CardId.RailBoost, CardId.AnchorCore },
                AthleteIdentity.Lucia => new[] { CardId.LeafVeil, CardId.MischiefCurve, CardId.TimeTease },
                AthleteIdentity.Charlotte => new[] { CardId.DragonGrace, CardId.NobleRetake, CardId.DragonAwakening },
                AthleteIdentity.Zephyr => new[] { CardId.JetIgnition, CardId.VectorWing, CardId.AirBrake },
                AthleteIdentity.Poko => new[] { CardId.LeafMasquerade, CardId.BorrowedForm, CardId.FalseTell },
                _ => throw new ArgumentOutOfRangeException(nameof(identity), identity, "Unknown athlete identity.")
            };
        }
    }
}
