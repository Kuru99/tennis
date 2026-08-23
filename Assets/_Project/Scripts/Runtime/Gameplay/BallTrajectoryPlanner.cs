using UnityEngine;

namespace PrideCourt.Gameplay
{
    public readonly struct BallTrajectoryPlan
    {
        public BallTrajectoryPlan(Vector3 initialVelocity, Vector3 acceleration, float duration,
            float netCrossingFraction, float netCrossingHeight)
        {
            InitialVelocity = initialVelocity;
            Acceleration = acceleration;
            Duration = duration;
            NetCrossingFraction = netCrossingFraction;
            NetCrossingHeight = netCrossingHeight;
        }

        public Vector3 InitialVelocity { get; }
        public Vector3 Acceleration { get; }
        public float Duration { get; }
        public float NetCrossingFraction { get; }
        public float NetCrossingHeight { get; }

        public Vector3 EvaluatePosition(Vector3 start, float elapsed)
        {
            float time = Mathf.Clamp(elapsed, 0f, Duration);
            return start + InitialVelocity * time + 0.5f * Acceleration * time * time;
        }
    }

    public static class BallTrajectoryPlanner
    {
        public static BallTrajectoryPlan Create(
            Vector3 start,
            Vector3 target,
            float requestedDuration,
            float speedMultiplier,
            Vector3 curveAcceleration,
            float minimumNetCenterHeight)
        {
            float speed = Mathf.Max(0.01f, speedMultiplier);
            Vector3 baseAcceleration = Physics.gravity + curveAcceleration;
            float crossingFraction = ResolveNetCrossingFraction(start.z, target.z);
            float shapedDuration = EnsureNetClearance(
                start.y,
                target.y,
                Mathf.Max(0.05f, requestedDuration),
                baseAcceleration.y,
                crossingFraction,
                minimumNetCenterHeight);

            float duration = shapedDuration / speed;
            Vector3 acceleration = baseAcceleration * speed * speed;
            Vector3 initialVelocity = (target - start - 0.5f * acceleration * duration * duration) / duration;
            float crossingTime = crossingFraction >= 0f ? duration * crossingFraction : 0f;
            float crossingHeight = crossingFraction >= 0f
                ? start.y + initialVelocity.y * crossingTime + 0.5f * acceleration.y * crossingTime * crossingTime
                : float.PositiveInfinity;

            return new BallTrajectoryPlan(initialVelocity, acceleration, duration, crossingFraction, crossingHeight);
        }

        private static float ResolveNetCrossingFraction(float startZ, float targetZ)
        {
            float distance = targetZ - startZ;
            if (Mathf.Abs(distance) < 0.001f || startZ * targetZ >= 0f) return -1f;
            float fraction = -startZ / distance;
            return fraction > 0f && fraction < 1f ? fraction : -1f;
        }

        private static float EnsureNetClearance(
            float startY,
            float targetY,
            float requestedDuration,
            float accelerationY,
            float crossingFraction,
            float minimumNetCenterHeight)
        {
            if (crossingFraction < 0f || accelerationY >= -0.01f) return requestedDuration;

            float linearHeight = Mathf.Lerp(startY, targetY, crossingFraction);
            float arcCoefficient = -0.5f * accelerationY * crossingFraction * (1f - crossingFraction);
            float requiredArcHeight = minimumNetCenterHeight - linearHeight;
            if (requiredArcHeight <= 0f || arcCoefficient <= 0f) return requestedDuration;

            float requiredDuration = Mathf.Sqrt(requiredArcHeight / arcCoefficient);
            return Mathf.Max(requestedDuration, requiredDuration);
        }
    }
}
