using PrideCourt.Domain;

namespace PrideCourt.Gameplay
{
    public static class ShotLandingProfile
    {
        public static float ResolveDepth(ShotKind kind)
        {
            float unscaledDepth = kind switch
            {
                ShotKind.Control => 9.4f,
                ShotKind.Topspin => 10f,
                ShotKind.Slice => 9.5f,
                ShotKind.Flat => 10.4f,
                ShotKind.Drop => 2.35f,
                ShotKind.AttackLob => 10.9f,
                ShotKind.DefensiveLob => 10.7f,
                ShotKind.Smash => 10.6f,
                ShotKind.AngleVolley => 3.8f,
                _ => 9.4f
            };

            return unscaledDepth * TennisCourtGeometry.Scale;
        }

        public static float ResolveBaselineInset(ShotKind kind)
        {
            return TennisCourtGeometry.HalfLength - ResolveDepth(kind);
        }
    }
}
