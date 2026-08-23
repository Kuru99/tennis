using PrideCourt.Domain;

namespace PrideCourt.Gameplay
{
    public static class TennisCourtGeometry
    {
        public const float Scale = 1.08f;
        public const float LineWidth = 0.07f;
        public const float NetHeight = 0.94f;
        public const float VisualWidth = 11.2f * Scale;
        public const float VisualLength = 23.8f * Scale;
        public const float HalfWidth = 5.5f * Scale;
        public const float HalfLength = 11.9f * Scale;
        public const float MovementHalfWidth = 5.25f * Scale;
        public const float MovementBaseline = 11.5f * Scale;
        public const float ServiceLineDepth = 6.4f * Scale;
        public const float ServiceBoxHalfWidth = 4.12f * Scale;
        public const float ServingLateralLimit = 4.45f * Scale;
        public const float ServingBaseline = 10.2f * Scale;
        public const float StartingLateralOffset = 2.65f * Scale;

        public static bool IsBallInSingles(float x, float z, float ballRadius)
        {
            float allowance = LineTouchAllowance(ballRadius);
            return System.Math.Abs(x) <= HalfWidth + allowance &&
                   System.Math.Abs(z) <= HalfLength + allowance;
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
