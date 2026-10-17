namespace HSBM.Domain;

/// <summary>
/// Seniors leave the high-school roster after the season. The player row
/// stays in the world so career stats and a future college destination
/// remain attached.
/// </summary>
public static class PlayerGraduation
{
	public static bool ShouldGraduate(Player player) =>
		!player.Alumni && !player.IsUnsigned && player.Grade == PlayerGrade.Senior;

	public static int Apply(IList<Player> players, int seasonYear)
	{
		int count = 0;
		foreach (Player player in players)
		{
			if (!ShouldGraduate(player))
			{
				continue;
			}

			player.Graduate(seasonYear, CollegeCatalog.Place(player.FullName, player.Overall, player.Potential));
			count++;
		}

		return count;
	}
}
