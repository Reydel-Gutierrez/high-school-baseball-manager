using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// January 5: the baseball year rolls. Seniors who remain are archived,
/// everyone else advances a grade, a new Open Play class is posted, and
/// the district schedule is rebuilt.
/// </summary>
public static class SeasonRollover
{
	public static void Apply(CareerState state, LeagueCatalog catalog, ISeededRandom rng)
	{
		int previousYear = state.Career.CurrentSeasonYear;
		SeasonArchive.EnsureCommitted(state);
		int year = previousYear + 1;
		state.Career.CurrentSeasonYear = year;
		state.World.CurrentSeasonYear = year;

		PlayerGraduation.Apply(state.Players, previousYear);
		DevelopmentDesk.Offseason(state, previousYear, rng.Derive(53));
		AdvanceReturningPlayers(state);
		TickAgreements(state);
		state.Offers.Clear();
		state.RecruitingInterest.Clear();
		state.RecruitingTargets.Clear();

		ReplaceIncomingClass(state, catalog, year, rng.Derive(41));
		ReplaceSchedule(state, catalog, year, rng.Derive(47));
		AddSeasonRecords(state, year);
	}

	private static void AdvanceReturningPlayers(CareerState state)
	{
		foreach (Player player in state.Players)
		{
			if (player.Alumni || player.IsUnsigned)
			{
				player.Injury = null;
				continue;
			}

			PlayerGrade? next = player.Grade.Advance();
			if (next == null)
			{
				player.Graduate(
					state.Career.CurrentSeasonYear - 1,
					CollegeCatalog.Place(player.FullName, player.Overall, player.Potential));
				continue;
			}

			player.Grade = next.Value;
			player.Age++;
			player.Injury = null;
		}
	}

	private static void TickAgreements(CareerState state)
	{
		foreach (PlayerAgreement agreement in state.Agreements)
		{
			if (agreement.YearsRemaining > 0)
			{
				agreement.YearsRemaining--;
			}
		}
	}

	private static void ReplaceIncomingClass(
		CareerState state,
		LeagueCatalog catalog,
		int year,
		ISeededRandom rng)
	{
		state.Players.RemoveAll(player => player.IsUnsigned && !player.Alumni);
		state.OpenPlay.Clear();

		var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Player player in state.Players)
		{
			usedNames.Add(player.FullName);
		}

		var incoming = new List<Player>();
		foreach (DistrictIdentity district in catalog.Districts)
		{
			ISeededRandom districtRng = rng.Derive(StableSalt(district.Id));
			List<Player> districtPlayers = OpenPlayGenerator.GenerateDistrict(
				district.Id,
				year,
				districtRng,
				usedNames);
			incoming.AddRange(districtPlayers);
			ISeededRandom simRng = rng.Derive(StableSalt(district.Id) ^ 917);
			state.OpenPlay.Add(OpenPlaySimulator.Simulate(district.Id, year, districtPlayers, simRng));
		}

		state.Players.AddRange(incoming);
	}

	private static void ReplaceSchedule(
		CareerState state,
		LeagueCatalog catalog,
		int year,
		ISeededRandom rng)
	{
		state.Games.RemoveAll(game => game.Status != GameStatus.Completed);
		List<TeamIdentity> teams = state.Teams.Where(team => team.RosterCap > 0).ToList();
		state.Games.AddRange(ScheduleGenerator.Generate(catalog, teams, year, rng));
	}

	private static void AddSeasonRecords(CareerState state, int year)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (TeamSeasonRecord record in state.TeamRecords)
		{
			if (record.SeasonYear == year)
			{
				seen.Add(record.TeamId);
			}
		}

		foreach (TeamIdentity team in state.Teams)
		{
			if (team.RosterCap <= 0 || !seen.Add(team.Id))
			{
				continue;
			}

			state.TeamRecords.Add(new TeamSeasonRecord
			{
				TeamId = team.Id,
				SeasonYear = year,
			});
		}
	}

	private static int StableSalt(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return hash;
		}
	}
}
