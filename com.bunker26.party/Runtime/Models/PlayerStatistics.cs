using System;

namespace Bunker26.Party
{
    /// <summary>Generic, game-independent profile statistics.</summary>
    [Serializable]
    public sealed class PlayerStatistics
    {
        public int tournamentsPlayed;
        public int tournamentsWon;
        public int tournamentsLost;
        public int matchesPlayed;
        public int matchesWon;
        public int matchesLost;
        public int gamesPlayed;
        public int gamesWon;
        public int gamesLost;

        public void RecordTournament(bool won)
        {
            tournamentsPlayed++;
            if (won) tournamentsWon++;
            else tournamentsLost++;
        }

        public void RecordMatch(bool won)
        {
            matchesPlayed++;
            if (won) matchesWon++;
            else matchesLost++;
        }

        public void RecordGame(bool won)
        {
            gamesPlayed++;
            if (won) gamesWon++;
            else gamesLost++;
        }
    }
}
