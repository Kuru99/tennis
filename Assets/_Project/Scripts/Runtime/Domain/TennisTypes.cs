using System;

namespace PrideCourt.Domain
{
    public enum CourtSide
    {
        Near = 0,
        Far = 1
    }

    public enum MatchPhase
    {
        Preparing,
        CardSelection,
        Serving,
        Rally,
        PointResult,
        MatchOver
    }

    public enum ShotPower
    {
        Safe,
        Strong
    }

    public enum ShotKind
    {
        Control,
        Topspin,
        Slice,
        Flat,
        Drop,
        AttackLob,
        DefensiveLob,
        Smash,
        AngleVolley
    }

    public enum AimZone
    {
        DeepLeft = -2,
        ShallowLeft = -1,
        Center = 0,
        ShallowRight = 1,
        DeepRight = 2
    }

    public enum TimingGrade
    {
        Mishit,
        Normal,
        Just
    }

    public enum AthleteIdentity
    {
        Lux,
        Bastion
    }

    public enum CpuDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public enum CardCategory
    {
        PersonalBuff,
        Instant,
        NextShot,
        Court
    }

    public enum CardId
    {
        AccelStep,
        EcoRun,
        RecoveryPulse,
        SpinBoost,
        GaugeCharge,
        GripCourt,
        SlipCourt,
        HighBounce,
        FlashStep,
        TailFeint,
        RailBoost,
        AnchorCore
    }

    [Serializable]
    public readonly struct PointResult
    {
        public PointResult(CourtSide winner, string reason)
        {
            Winner = winner;
            Reason = reason ?? string.Empty;
        }

        public CourtSide Winner { get; }
        public string Reason { get; }
    }

    public static class CourtSideExtensions
    {
        public static CourtSide Opposite(this CourtSide side)
        {
            return side == CourtSide.Near ? CourtSide.Far : CourtSide.Near;
        }
    }
}
