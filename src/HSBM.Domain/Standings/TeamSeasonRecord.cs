namespace HSBM.Domain;

public sealed class TeamSeasonRecord
{
	public string TeamId { get; set; } = "";
	public int SeasonYear { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int DistrictWins { get; set; }
	public int DistrictLosses { get; set; }
	public int RunsFor { get; set; }
	public int RunsAgainst { get; set; }

	public int GamesPlayed => Wins + Losses;

	public double WinningPercentage => GamesPlayed == 0 ? 0 : (double)Wins / GamesPlayed;

	public void ApplyGame(int runsFor, int runsAgainst, bool won, bool district)
	{
		RunsFor += runsFor;
		RunsAgainst += runsAgainst;
		if (won)
		{
			Wins++;
			if (district)
			{
				DistrictWins++;
			}
		}
		else
		{
			Losses++;
			if (district)
			{
				DistrictLosses++;
			}
		}
	}
}
