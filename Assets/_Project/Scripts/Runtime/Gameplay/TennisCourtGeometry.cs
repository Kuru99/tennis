using PrideCourt.Domain;

namespace PrideCourt.Gameplay
{
    public static class TennisCourtGeometry
    {
        public const float Scale = 1.08f;
        public const float CourtWidthMultiplier = 1.5f;
        public const float LateralScale = Scale * CourtWidthMultiplier;
        public const float LineWidth = 0.07f;
        public const float NetHeight = 0.94f;
        public const float VisualWidth = 11.2f * LateralScale;
        public const float VisualLength = 23.8f * Scale;
        public const float HalfWidth = 5.5f * LateralScale;
        public const float HalfLength = 11.9f * Scale;
        public const float MovementHalfWidth = 5.25f * LateralScale;
        public const float MovementBaseline = 11.5f * Scale;
        public const float ServiceLineDepth = 6.4f * Scale;
        public const float ServiceBoxHalfWidth = 4.12f * LateralScale;
        public const float ServingLateralLimit = 4.45f * LateralScale;
        public const float ServingBaseline = 10.2f * Scale;
        public const float StartingLateralOffset = 2.65f * LateralScale;
        public const float BaselineVisualWidth = 11.05f * LateralScale;
        public const float ServiceLineVisualWidth = 8.25f * LateralScale;
        public const float NetVisualWidth = 11.35f * LateralScale;

        public static bool IsBallInSingles(float x, float z, float ballRadius)
        {
            float allowance = LineTouchAllowance(ballRadius);
            return System.Math.Abs(x) <= HalfWidth + allowance &&
                   System.Math.Abs(z) <= HalfLength + allowance;
        }

        /// <summary>
        /// Returns whether the ball is still close enough to the physical court
        /// for Unity collision detection to resolve a landing. This is a
        /// failsafe envelope, not the in/out rule used for a bounce.
        /// </summary>
        public static bool IsInsideSimulationBounds(float x, float z, float ballRadius)
        {
            float padding = LineTouchAllowance(ballRadius);
            return System.Math.Abs(x) <= VisualWidth * 0.5f + padding &&
                   System.Math.Abs(z) <= VisualLength * 0.5f + padding;
        }

        public static bool IsBallInServiceBox(
            float x,
            float z,
            CourtSide server,
            float expectedHorizontalSign,
            float ballRadius)
        {
            float allowance = LineTouchAllowance(ballRadius);
            float targetDepth = server == CourtSide.Near ? z : -z;
            float signedHorizontal = x * expectedHorizontalSign;
            return targetDepth > 0f &&
                   targetDepth <= ServiceLineDepth + allowance &&
                   signedHorizontal >= -allowance &&
                   System.Math.Abs(x) <= ServiceBoxHalfWidth + allowance;
        }

        private static float LineTouchAllowance(float ballRadius)
        {
            return System.Math.Max(0f, ballRadius) + LineWidth * 0.5f;
        }
    }
}
