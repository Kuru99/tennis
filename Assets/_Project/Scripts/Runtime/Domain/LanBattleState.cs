using System;
using UnityEngine;

namespace PrideCourt.Domain
{
    [Serializable]
    public struct LanAthleteState
    {
        public Vector3 Position;
        public float RotationY;
        public AthleteIdentity Identity;
        public float Stamina;
        public bool Exhausted;
        public float SpecialGauge;
        public float PlanarSpeed;
        public bool IsDashing;
        public bool IsDiving;
        public bool IsSpecialReserved;
    }

    [Serializable]
    public struct LanBallState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public int State;
        public CourtSide LastHitter;
        public int BounceCount;
        public bool SpecialBall;
    }

    [Serializable]
    public struct LanCardState
    {
        public CardId[] Hand;
        public int ActiveIndex;
        public bool UsedThisPoint;
        public bool HasPending;
        public CardId Pending;
    }

    [Serializable]
    public struct LanMatchState
    {
        public uint Sequence;
        public bool HasStarted;
        public MatchPhase Phase;
        public int NearPoints;
        public int FarPoints;
        public int NearGames;
        public int FarGames;
        public int GamesToWin;
        public int CompletedPoints;
        public float PhaseTimeRemaining;
        public bool ServeHasBeenStruck;
        public bool IsGameplayPaused;
        public string StatusMessage;
        public bool HasActiveCourtCard;
        public CardId ActiveCourtCard;
        public LanAthleteState NearAthlete;
        public LanAthleteState FarAthlete;
        public LanBallState Ball;
        public LanCardState NearCards;
        public LanCardState FarCards;
    }
}
