using PrideCourt.Gameplay;
using UnityEngine;

namespace PrideCourt.Presentation
{
    public sealed class TennisCameraController : MonoBehaviour
    {
        public static readonly Vector3 DefaultBasePosition = new Vector3(0f, 15.5f, -25.5f);
        public static readonly Vector3 DefaultLookTarget = new Vector3(0f, 1.1f, -2.5f);

        [SerializeField] private Transform ball;
        [SerializeField] private float horizontalFollow = 0.18f;
        [SerializeField] private float smoothTime = 0.22f;

        private Vector3 velocity;
        private PrideCourt.Domain.CourtSide localSide = PrideCourt.Domain.CourtSide.Near;

        public void Configure(Transform configuredBall)
        {
            ball = configuredBall;
        }

        public void SetLocalSide(PrideCourt.Domain.CourtSide side)
        {
            localSide = side;
            velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            float maximumHorizontalOffset = 1.15f * TennisCourtGeometry.CourtWidthMultiplier;
            float targetX = ball == null ? 0f : Mathf.Clamp(ball.position.x * horizontalFollow,
                -maximumHorizontalOffset, maximumHorizontalOffset);
            float perspective = localSide == PrideCourt.Domain.CourtSide.Near ? 1f : -1f;
            Vector3 targetPosition = new Vector3(DefaultBasePosition.x + targetX * perspective,
                DefaultBasePosition.y, DefaultBasePosition.z * perspective);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            Vector3 lookTarget = new Vector3(targetX * 0.35f * perspective, DefaultLookTarget.y,
                DefaultLookTarget.z * perspective);
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }
    }
}
