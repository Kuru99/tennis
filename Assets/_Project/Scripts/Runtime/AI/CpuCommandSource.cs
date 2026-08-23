using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Input;
using UnityEngine;

namespace PrideCourt.AI
{
    public sealed class CpuCommandSource : MonoBehaviour, ITennisCommandSource
    {
        [SerializeField] private TennisAthleteController athlete;
        [SerializeField] private TennisAthleteController opponent;
        [SerializeField] private TennisBallController ball;
        [SerializeField] private TennisMatchController match;

        private float nextActionTime;
        private bool tossedThisServe;
        private Vector3 perceivedBallPosition;
        private float nextPerceptionTime;
        private float nextDiveDecisionTime;
        private TennisCommand injectedCommand;
        private float injectedCommandUntil;
        private bool injectedEdgeConsumed;

        public void InjectCommandForValidation(TennisCommand command)
        {
            injectedCommand = command;
            injectedCommandUntil = Time.unscaledTime + 0.3f;
            injectedEdgeConsumed = false;
        }

        public void Configure(
            TennisAthleteController configuredAthlete,
            TennisAthleteController configuredOpponent,
            TennisBallController configuredBall,
            TennisMatchController configuredMatch)
        {
            athlete = configuredAthlete;
            opponent = configuredOpponent;
            ball = configuredBall;
            match = configuredMatch;
        }

        public TennisCommand ReadCommand()
        {
            if (athlete == null || opponent == null || ball == null || match == null)
            {
                return default;
            }

            if (Time.unscaledTime < injectedCommandUntil)
            {
                if (!injectedEdgeConsumed)
                {
                    injectedEdgeConsumed = true;
                    return injectedCommand;
                }

                return new TennisCommand(injectedCommand.Move, injectedCommand.Dash,
                    false, false, false, false, false, Vector2.zero,
                    shotInputHeld: injectedCommand.ShotInputHeld);
            }

            if (match.ServeRestrictionsActive && match.Server == athlete.Side)
            {
                return ReadServeCommand();
            }

            tossedThisServe = false;
            if (match.Phase != MatchPhase.Rally && !match.IsServeAwaitingReturn)
            {
                return default;
            }

            CpuDifficulty difficulty = CpuDifficultyPreferences.Current;
            float reactionDelay = difficulty == CpuDifficulty.Easy ? 0.28f : difficulty == CpuDifficulty.Normal ? 0.16f : 0.08f;
            if (Time.time >= nextPerceptionTime)
            {
                perceivedBallPosition = ball.transform.position;
                nextPerceptionTime = Time.time + reactionDelay;
            }

            Vector3 target = ball.CanBeHitBy(athlete.Side)
                ? new Vector3(perceivedBallPosition.x, athlete.transform.position.y, perceivedBallPosition.z)
                : new Vector3(0f, athlete.transform.position.y, athlete.Side == CourtSide.Near
                    ? -8.2f * TennisCourtGeometry.Scale
                    : 8.2f * TennisCourtGeometry.Scale);
            Vector3 delta = target - athlete.transform.position;
            Vector3 planarDelta = delta;
            planarDelta.y = 0f;
            Vector2 move = new Vector2(delta.x, athlete.Side == CourtSide.Near ? delta.z : -delta.z);
            move = Vector2.ClampMagnitude(move / 2.4f, 1f);

            float reachFactor = difficulty == CpuDifficulty.Easy ? 0.78f : difficulty == CpuDifficulty.Normal ? 0.88f : 0.95f;
            bool canSwing = ball.CanBeHitBy(athlete.Side) && delta.magnitude <= athlete.HitRadius * reachFactor && Time.time >= nextActionTime;
            bool strong = false;
            bool safe = false;
            bool dive = false;
            Vector2 diveDirection = Vector2.zero;
            float standingReach = athlete.HitRadius * reachFactor;
            float diveReach = athlete.HitRadius + athlete.DiveTravelDistance;
            bool canConsiderDive = !athlete.IsDiveSequenceActive && Time.time >= nextDiveDecisionTime && move.sqrMagnitude > 0.1f;
            if (canConsiderDive)
            {
                nextDiveDecisionTime = Time.time + reactionDelay + 0.12f;
                dive = CpuDivePolicy.ShouldDive(
                    difficulty,
                    ball.CanBeHitBy(athlete.Side),
                    planarDelta.magnitude,
                    standingReach,
                    diveReach,
                    athlete.Stamina.Current,
                    Random.value);
                if (dive)
                {
                    diveDirection = move.normalized;
                    safe = true;
                    nextActionTime = Time.time + 0.65f;
                }
            }

            if (!dive && canSwing)
            {
                float decisionCooldown = difficulty == CpuDifficulty.Easy ? 1.0f : difficulty == CpuDifficulty.Normal ? 0.72f : 0.52f;
                nextActionTime = Time.time + decisionCooldown;
                float strongThreshold = difficulty == CpuDifficulty.Easy ? 0.48f : difficulty == CpuDifficulty.Normal ? 0.3f : 0.2f;
                strong = athlete.Stamina.Current >= 30f && Random.value > strongThreshold;
                safe = !strong;
                float aimMistakeChance = difficulty == CpuDifficulty.Easy ? 0.35f : difficulty == CpuDifficulty.Normal ? 0.16f : 0.05f;
                bool readsOpponent = Random.value > aimMistakeChance;
                move.x = readsOpponent
                    ? (opponent.transform.position.x >= 0f ? -0.65f : 0.65f)
                    : (opponent.transform.position.x >= 0f ? 0.65f : -0.65f);
                move.y = Random.value > (difficulty == CpuDifficulty.Hard ? 0.68f : 0.82f) ? -0.7f : 0.1f;
            }

            bool special = athlete.SpecialGauge.IsReady && Random.value < (difficulty == CpuDifficulty.Hard ? 0.025f : 0.012f);
            return new TennisCommand(move, !dive && move.magnitude > 0.82f, strong, safe, false, special, dive, diveDirection);
        }

        private TennisCommand ReadServeCommand()
        {
            if (Time.time < nextActionTime)
            {
                return default;
            }

            if (!ball.IsTossed && !tossedThisServe)
            {
                tossedThisServe = true;
                nextActionTime = Time.time + 0.55f;
                return new TennisCommand(Vector2.zero, false, false, false, true, false);
            }

            if (ball.IsTossed)
            {
                nextActionTime = Time.time + 1f;
                tossedThisServe = false;
                return new TennisCommand(new Vector2(Random.value > 0.5f ? 0.7f : -0.7f, 0.2f), false, true, false, false, false);
            }

            tossedThisServe = false;
            return default;
        }
    }

    public static class CpuDivePolicy
    {
        public static bool ShouldDive(
            CpuDifficulty difficulty,
            bool ballCanBeHit,
            float planarDistance,
            float standingReach,
            float diveReach,
            float stamina,
            float decisionRoll)
        {
            if (!ballCanBeHit || stamina <= 0f || planarDistance <= standingReach || planarDistance > diveReach)
            {
                return false;
            }

            float chance = difficulty == CpuDifficulty.Easy ? 0.25f :
                difficulty == CpuDifficulty.Normal ? 0.58f : 0.85f;
            return decisionRoll <= chance;
        }
    }
}
