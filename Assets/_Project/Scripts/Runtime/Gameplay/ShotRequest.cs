using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    public readonly struct ShotRequest
    {
        public ShotRequest(ShotPower power, ShotKind kind, float requestedAt, Vector2 initialDirection)
        {
            Power = power;
            Kind = kind;
            RequestedAt = requestedAt;
            InitialDirection = initialDirection;
        }

        public ShotPower Power { get; }
        public ShotKind Kind { get; }
        public float RequestedAt { get; }
        public Vector2 InitialDirection { get; }
    }
}
