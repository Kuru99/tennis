using PrideCourt.Domain;

namespace PrideCourt.Gameplay
{
    public enum StrokeMotion
    {
        Forehand,
        Backhand,
        Serve
    }

    public static class StrokeMotionPolicy
    {
        private const float BackhandSideThreshold = 0.08f;

        public static StrokeMotion Resolve(CourtSide side, float athleteX, float ballX, bool isServe)
        {
            if (isServe)
            {
                return StrokeMotion.Serve;
            }

            float localBallX = side == CourtSide.Near
                ? ballX - athleteX
                : athleteX - ballX;
            return localBallX < -BackhandSideThreshold
                ? StrokeMotion.Backhand
                : StrokeMotion.Forehand;
        }
    }
}
