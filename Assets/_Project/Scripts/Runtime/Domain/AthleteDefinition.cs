using System;
using System.Collections.Generic;

namespace PrideCourt.Domain
{
    public readonly struct AthleteStats
    {
        public AthleteStats(
            float walkSpeed,
            float dashMultiplier,
            float acceleration,
            float brakingAcceleration,
            float hitRadius,
            float diveSpeed,
            float shotSpeedMultiplier,
            float shotAccuracyMultiplier,
            float dashCostMultiplier,
            float strongCostMultiplier,
            float specialSpeedMultiplier)
        {
            WalkSpeed = walkSpeed;
            DashMultiplier = dashMultiplier;
            Acceleration = acceleration;
            BrakingAcceleration = brakingAcceleration;
            HitRadius = hitRadius;
            DiveSpeed = diveSpeed;
            ShotSpeedMultiplier = shotSpeedMultiplier;
            ShotAccuracyMultiplier = shotAccuracyMultiplier;
            DashCostMultiplier = dashCostMultiplier;
            StrongCostMultiplier = strongCostMultiplier;
            SpecialSpeedMultiplier = specialSpeedMultiplier;
        }

        public float WalkSpeed { get; }
        public float DashMultiplier { get; }
        public float Acceleration { get; }
        public float BrakingAcceleration { get; }
        public float HitRadius { get; }
        public float DiveSpeed { get; }
        public float ShotSpeedMultiplier { get; }
        public float ShotAccuracyMultiplier { get; }
        public float DashCostMultiplier { get; }
        public float StrongCostMultiplier { get; }
        public float SpecialSpeedMultiplier { get; }
    }

    public readonly struct AthleteDefinition
    {
        public AthleteDefinition(
            AthleteIdentity identity,
            string internalName,
            string displayName,
            string selectionTrait,
            string specialName,
            AthleteIdentity defaultOpponent,
            AthleteStats stats)
        {
            Identity = identity;
            InternalName = internalName;
            DisplayName = displayName;
            SelectionTrait = selectionTrait;
            SpecialName = specialName;
            DefaultOpponent = defaultOpponent;
            Stats = stats;
        }

        public AthleteIdentity Identity { get; }
        public string InternalName { get; }
        public string DisplayName { get; }
        public string SelectionTrait { get; }
        public string SpecialName { get; }
        public AthleteIdentity DefaultOpponent { get; }
        public AthleteStats Stats { get; }
    }

    public static class AthleteCatalog
    {
        private static readonly AthleteIdentity[] Identities =
        {
            AthleteIdentity.Lux,
            AthleteIdentity.Bastion,
            AthleteIdentity.Lucia,
            AthleteIdentity.Charlotte,
            AthleteIdentity.Zephyr,
            AthleteIdentity.Poko
        };

        private static readonly Dictionary<AthleteIdentity, AthleteDefinition> Definitions =
            new Dictionary<AthleteIdentity, AthleteDefinition>
            {
                {
                    AthleteIdentity.Lux,
                    new AthleteDefinition(
                        AthleteIdentity.Lux,
                        "Lux",
                        "ルクス",
                        "俊敏なキツネ  •  幻影の絆\n素早い加速 / 正確な立て直し",
                        "ミラージュ・バウンド",
                        AthleteIdentity.Bastion,
                        new AthleteStats(6.2f, 1.65f, 22f, 58f, 2.05f, 12.5f, 1f, 1f, 1f, 1f, 1f))
                },
                {
                    AthleteIdentity.Bastion,
                    new AthleteDefinition(
                        AthleteIdentity.Bastion,
                        "Bastion",
                        "バスティオン",
                        "剛力のロボット  •  重力駆動\n広い守備範囲 / 重いプレッシャー",
                        "グラビティ・ドライブ",
                        AthleteIdentity.Lux,
                        new AthleteStats(4.75f, 1.42f, 13f, 46f, 2.35f, 12.5f, 1f, 1f, 1f, 1f, 1f))
                },
                {
                    AthleteIdentity.Lucia,
                    new AthleteDefinition(
                        AthleteIdentity.Lucia,
                        "Lucia",
                        "ルシア",
                        "妖精  •  テクニック型\n撹乱 → 接近 → 差し込みの三手",
                        "フェアリー・リプライズ",
                        AthleteIdentity.Charlotte,
                        new AthleteStats(6.65f, 1.72f, 25f, 62f, 1.95f, 14.6f, 0.91f, 1.12f, 1f, 1.12f, 0.98f))
                },
                {
                    AthleteIdentity.Charlotte,
                    new AthleteDefinition(
                        AthleteIdentity.Charlotte,
                        "Charlotte",
                        "シャルロット",
                        "竜人  •  バランス型\n高水準の基本性能 / 劣勢で竜醒",
                        "クリムゾン・レガリア",
                        AthleteIdentity.Lucia,
                        new AthleteStats(5.75f, 1.52f, 18.5f, 53f, 2.35f, 12.8f, 1.06f, 0.85f, 1f, 0.9f, 1.22f))
                },
                {
                    AthleteIdentity.Zephyr,
                    new AthleteDefinition(
                        AthleteIdentity.Zephyr,
                        "Zephyr",
                        "ゼファー",
                        "迎撃ジェット  •  スピード型\n最高速と初速 / 高いダッシュ消費",
                        "アフターバーナー・スマッシュ",
                        AthleteIdentity.Poko,
                        new AthleteStats(7.05f, 1.82f, 31f, 44f, 1.8f, 16.2f, 1f, 1.02f, 1.32f, 1.05f, 1.24f))
                },
                {
                    AthleteIdentity.Poko,
                    new AthleteDefinition(
                        AthleteIdentity.Poko,
                        "Poko",
                        "ぽこ",
                        "化けたぬき  •  テクニック型\n素は控えめ / 見せ方で読みを外す",
                        "大化けスマッシュ",
                        AthleteIdentity.Zephyr,
                        new AthleteStats(5.65f, 1.58f, 21f, 54f, 2f, 13.2f, 0.9f, 1.18f, 1.05f, 1.16f, 1.2f))
                }
            };

        public static IReadOnlyList<AthleteIdentity> All => Identities;

        public static AthleteDefinition Get(AthleteIdentity identity)
        {
            if (!Definitions.TryGetValue(identity, out AthleteDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(identity), identity, "Unknown athlete identity.");
            }

            return definition;
        }

        public static AthleteIdentity Previous(AthleteIdentity identity)
        {
            return Offset(identity, -1);
        }

        public static AthleteIdentity Next(AthleteIdentity identity)
        {
            return Offset(identity, 1);
        }

        private static AthleteIdentity Offset(AthleteIdentity identity, int direction)
        {
            int index = Array.IndexOf(Identities, identity);
            if (index < 0) index = 0;
            return Identities[(index + direction + Identities.Length) % Identities.Length];
        }
    }
}
