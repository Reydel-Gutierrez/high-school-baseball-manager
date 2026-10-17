using HSBM.Domain;

namespace HSBM.Simulation;

public static class InjuryDesk
{
	private static readonly InjuryTable[] Tables =
	[
		new("Hamstring Strain", 7, 14, false, 0.9),
		new("Ankle Sprain", 5, 12, false, 0.85),
		new("Oblique Strain", 10, 18, false, 1.0),
		new("Wrist Contusion", 3, 7, true, 0.7),
		new("Shoulder Inflammation", 14, 21, false, 2.4),
		new("Elbow Soreness", 8, 16, false, 2.1),
	];

	public const double BatterChance = 0.018;
	public const double PitcherChance = 0.032;

	public static List<PlayerTransaction> Tick(CareerState state, DateOnly today)
	{
		var posted = new List<PlayerTransaction>();
		foreach (Player player in state.Players)
		{
			if (player.Injury == null || player.Injury.ActiveOn(today))
			{
				continue;
			}

			player.Injury = null;
		}

		return posted;
	}

	public static List<PlayerTransaction> FromGame(
		CareerState state,
		ScheduledGame game,
		GameSimulationResult result,
		ISeededRandom rng)
	{
		var posted = new List<PlayerTransaction>();
		var seen = new HashSet<Guid>();
		foreach (PlayerGameBatting line in result.Batting)
		{
			if (line.PlateAppearances <= 0 || !seen.Add(line.PlayerId))
			{
				continue;
			}

			TryInjure(state, game, line.PlayerId, pitcher: false, rng, posted);
		}

		foreach (PlayerGamePitching line in result.Pitching)
		{
			if (line.OutsRecorded <= 0)
			{
				continue;
			}

			TryInjure(state, game, line.PlayerId, pitcher: true, rng, posted);
		}

		return posted;
	}

	private static void TryInjure(
		CareerState state,
		ScheduledGame game,
		Guid playerId,
		bool pitcher,
		ISeededRandom rng,
		List<PlayerTransaction> posted)
	{
		Player? player = state.PlayerById(playerId);
		if (player == null || player.Injury != null)
		{
			return;
		}

		double chance = pitcher ? PitcherChance : BatterChance;
		if (rng.NextDouble() >= chance)
		{
			return;
		}

		InjuryTable table = Pick(player.IsPitcher, rng);
		int days = rng.Next(table.MinDays, table.MaxDays + 1);
		var injury = new PlayerInjury
		{
			Kind = table.Kind,
			Start = game.Date,
			AvailableOn = game.Date.AddDays(days),
			DayToDay = table.DayToDay || days <= 4,
		};
		player.Injury = injury;

		OrganizationState school = state.OrganizationForTeam(player.TeamId);
		posted.Add(PlayerTransaction.Injured(player, school.ShortName, school.Id, game.Date, injury));
	}

	private static InjuryTable Pick(bool pitcher, ISeededRandom rng)
	{
		double total = 0;
		foreach (InjuryTable row in Tables)
		{
			total += pitcher ? row.PitcherBias : 1.0;
		}

		double roll = rng.NextDouble() * total;
		double cursor = 0;
		foreach (InjuryTable row in Tables)
		{
			cursor += pitcher ? row.PitcherBias : 1.0;
			if (roll <= cursor)
			{
				return row;
			}
		}

		return Tables[0];
	}

	private sealed record InjuryTable(string Kind, int MinDays, int MaxDays, bool DayToDay, double PitcherBias);
}
