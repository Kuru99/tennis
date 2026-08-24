using System;

namespace PrideCourt.Domain
{
    [Serializable]
    public sealed class MatchScore
    {
        public const int MinimumWinningPoints = 6;
        public const int RequiredLead = 2;
        public const int MinimumGamesToWin = 1;
        public const int MaximumGamesToWin = 3;
        public const int DefaultGamesToWin = 2;

        private int gamesToWin = DefaultGamesToWin;

        public int NearPoints { get; private set; }
        public int FarPoints { get; private set; }
        public int NearGames { get; private set; }
        public int FarGames { get; private set; }
        public int CompletedPoints { get; private set; }
        public int GamesToWin => gamesToWin;

        public CourtSide Server => CompletedPoints % 2 == 0 ? CourtSide.Near : CourtSide.Far;

        public bool IsMatchOver => TryGetWinner(out _);

        public static bool IsSupportedGamesToWin(int value)
        {
            return value >= MinimumGamesToWin && value <= MaximumGamesToWin;
        }

        public void ConfigureGamesToWin(int value)
        {
            if (!IsSupportedGamesToWin(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"Games to win must be between {MinimumGamesToWin} and {MaximumGamesToWin}.");
            }

            gamesToWin = value;
        }

        public void Reset()
        {
            NearPoints = 0;
            FarPoints = 0;
            NearGames = 0;
            FarGames = 0;
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
            if (!TryGetGameWinner(out CourtSide gameWinner))
            {
                return;
            }

            if (gameWinner == CourtSide.Near)
            {
                NearGames++;
            }
            else
            {
                FarGames++;
            }

            if (!IsMatchOver)
            {
                NearPoints = 0;
                FarPoints = 0;
            }
        }

        public void Restore(int nearPoints, int farPoints, int nearGames, int farGames, int completedPoints,
            int configuredGamesToWin)
        {
            ConfigureGamesToWin(configuredGamesToWin);
            NearPoints = Math.Max(0, nearPoints);
            FarPoints = Math.Max(0, farPoints);
            NearGames = Math.Max(0, nearGames);
            FarGames = Math.Max(0, farGames);
            CompletedPoints = Math.Max(0, completedPoints);
        }

        public int GetPoints(CourtSide side)
        {
            return side == CourtSide.Near ? NearPoints : FarPoints;
        }

        public int GetGames(CourtSide side)
        {
            return side == CourtSide.Near ? NearGames : FarGames;
        }

        public bool TryGetGameWinner(out CourtSide winner)
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

        public bool TryGetWinner(out CourtSide winner)
        {
            if (NearGames >= gamesToWin || FarGames >= gamesToWin)
            {
                winner = NearGames > FarGames ? CourtSide.Near : CourtSide.Far;
                return true;
            }

            winner = default;
            return false;
        }
    }
}
