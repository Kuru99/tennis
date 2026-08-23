using System;

namespace PrideCourt.Domain
{
    [Serializable]
    public sealed class MatchScore
    {
        public const int MinimumWinningPoints = 6;
        public const int RequiredLead = 2;

        public int NearPoints { get; private set; }
        public int FarPoints { get; private set; }
        public int CompletedPoints { get; private set; }

        public CourtSide Server => CompletedPoints % 2 == 0 ? CourtSide.Near : CourtSide.Far;

        public bool IsMatchOver => TryGetWinner(out _);

        public void Reset()
        {
            NearPoints = 0;
            FarPoints = 0;
            CompletedPoints = 0;
        }

        public void AwardPoint(CourtSide winner)
        {
            if (IsMatchOver)
            {
                throw new InvalidOperationException("The match has already ended.");
            }

            if (winner == CourtSide.Near)
            {
                NearPoints++;
            }
            else
            {
                FarPoints++;
            }

            CompletedPoints++;
        }

        public void Restore(int nearPoints, int farPoints, int completedPoints)
        {
            NearPoints = Math.Max(0, nearPoints);
            FarPoints = Math.Max(0, farPoints);
            CompletedPoints = Math.Max(0, completedPoints);
        }

        public int GetPoints(CourtSide side)
        {
            return side == CourtSide.Near ? NearPoints : FarPoints;
        }

        public bool TryGetWinner(out CourtSide winner)
        {
            int highest = Math.Max(NearPoints, FarPoints);
            int lead = Math.Abs(NearPoints - FarPoints);
            if (highest >= MinimumWinningPoints && lead >= RequiredLead)
            {
                winner = NearPoints > FarPoints ? CourtSide.Near : CourtSide.Far;
                return true;
            }

            winner = default;
            return false;
        }
    }
}
