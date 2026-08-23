using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TennisNetSurface : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float cordAssistTolerance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float tipForwardRetention = 0.55f;
        [SerializeField, Range(0f, 1f)] private float tipSideRetention = 0.72f;
        [SerializeField, Range(0f, 1f)] private float blockedReverseRetention = 0.12f;
        [SerializeField, Range(0f, 1f)] private float blockedSideRetention = 0.35f;
        [SerializeField, Min(0f)] private float minimumTipForwardSpeed = 2.2f;
        [SerializeField, Min(0f)] private float minimumBlockedReverseSpeed = 0.65f;

        private BoxCollider netCollider;

        private void Awake()
        {
            netCollider = GetComponent<BoxCollider>();
        }

        public bool DeflectBall(
            Rigidbody ballBody,
            CourtSide lastHitter,
            float ballRadius,
            float ballSpeedMultiplier)
        {
            netCollider ??= GetComponent<BoxCollider>();
            Bounds netBounds = netCollider.bounds;
            float forwardDirection = lastHitter == CourtSide.Near ? 1f : -1f;
            float speedScale = Mathf.Max(0.01f, ballSpeedMultiplier);
            Vector3 velocity = ballBody.linearVelocity;

            bool clippedTopCord = ballBody.position.y + ballRadius >= netBounds.max.y - cordAssistTolerance;
            if (clippedTopCord)
            {
                Vector3 clearedPosition = ballBody.position;
                clearedPosition.z = forwardDirection > 0f
                    ? netBounds.max.z + ballRadius + 0.02f
                    : netBounds.min.z - ballRadius - 0.02f;
                clearedPosition.y = Mathf.Max(clearedPosition.y, netBounds.max.y + ballRadius * 0.2f);
                ballBody.position = clearedPosition;

                velocity.x *= tipSideRetention;
                velocity.y = Mathf.Max(velocity.y * 0.25f, 0.85f * speedScale);
                velocity.z = forwardDirection * Mathf.Max(
                    Mathf.Abs(velocity.z) * tipForwardRetention,
                    minimumTipForwardSpeed * speedScale);
                ballBody.linearVelocity = velocity;
                return true;
            }

            velocity.x *= blockedSideRetention;
            velocity.y = -Mathf.Max(Mathf.Abs(velocity.y) * 0.25f, 0.55f * speedScale);
            velocity.z = -forwardDirection * Mathf.Max(
                Mathf.Abs(velocity.z) * blockedReverseRetention,
                minimumBlockedReverseSpeed * speedScale);
            ballBody.linearVelocity = velocity;
            return false;
        }
    }
}
