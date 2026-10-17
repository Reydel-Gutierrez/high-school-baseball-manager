using HSBM.Domain;

namespace HSBM.Simulation;

public static class ProgramHistoryGenerator
{
	public const int HistoryYears = 8;
	public const int RecordSlots = ProgramRecordBook.Slots;

	public static void Assign(
		IReadOnlyList<OrganizationState> organizations,
		ISeededRandom rng,
		int currentSeasonYear)
	{
		List<OrganizationState> programs = organizations
			.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id))
			.ToList();
		int lastYear = currentSeasonYear <= 0
			? LeagueCatalog.DefaultSeasonYear - 1
			: currentSeasonYear - 1;

		foreach (OrganizationState org in programs)
		{
			ISeededRandom local = rng.Derive(StableSalt(org.Id));
			org.Profile = RollIdentity(org.TalentTier, local);
			org.SeasonHistory = [];
			org.RecordBook = [];
			org.HallOfFame = [];
		}

		for (int year = lastYear - HistoryYears + 1; year <= lastYear; year++)
		{
			var regularWins = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (OrganizationState org in programs)
			{
				ISeededRandom yearRng = rng.Derive(StableSalt(org.Id) ^ year);
				regularWins[org.Id] = RollWins(org.TalentTier, yearRng);
			}

			foreach (IGrouping<string, OrganizationState> district in programs.GroupBy(org => org.DistrictId))
			{
				List<OrganizationState> ranked = district
					.OrderByDescending(org => regularWins[org.Id])
					.ThenByDescending(org => org.Profile.Reputation)
					.ThenBy(org => org.Name, StringComparer.Ordinal)
					.ToList();
				for (int i = 0; i < ranked.Count; i++)
				{
					OrganizationState org = ranked[i];
					int finish = i + 1;
					int wins = regularWins[org.Id];
					ISeededRandom postRng = rng.Derive(StableSalt(org.Id) ^ (year * 917));
					ProgramSeasonResult season = RollSeason(org, year, wins, finish, postRng);
					org.SeasonHistory.Add(season);
				}
			}
		}

		foreach (OrganizationState org in programs)
		{
			org.SeasonHistory.Sort((a, b) => a.SeasonYear.CompareTo(b.SeasonYear));
			ProgramSeasonResult last = org.SeasonHistory[^1];
			org.Profile.LastSeasonWins = last.RegularWins;
			org.Profile.LastSeasonLosses = last.RegularLosses;
			org.Profile.DistrictFinish = last.DistrictFinish;
			org.Profile.DistrictTitles = CountTitles(org.SeasonHistory, row => row.WonDistrict, org.TalentTier, rng, extraPowerhouse: 3);
			org.Profile.RegionalTitles = CountTitles(org.SeasonHistory, row => row.WonRegional, org.TalentTier, rng, extraPowerhouse: 2);
			org.Profile.StateTitles = CountTitles(org.SeasonHistory, row => row.WonState, org.TalentTier, rng, extraPowerhouse: 1);
			org.Profile.TargetExpectation = ProgramReputation.TargetExpectation(org.Profile.Reputation, last.RegularWins);
			org.Profile.MinimumExpectation = org.Profile.TargetExpectation.StepDown();
			org.RefreshTalentFromReputation();
			org.RecordBook = RollRecords(org, rng.Derive(StableSalt(org.Id) ^ 441));
			org.HallOfFame = RollHall(org, rng.Derive(StableSalt(org.Id) ^ 773));
		}
	}

	private static ProgramSeasonResult RollSeason(
		OrganizationState org,
		int year,
		int regularWins,
		int finish,
		ISeededRandom rng)
	{
		int regularLosses = ProgramProfile.SeasonGames - regularWins;
		(bool wonDistrict, bool wonRegional, bool wonState) = RollTitles(org.TalentTier, finish, rng);
		string postseason = ProgramPostseason.HistoryLabel(wonDistrict, wonRegional, wonState, finish);
		(int extraWins, int extraLosses) = PostseasonGames(postseason, rng);
		return new ProgramSeasonResult
		{
			OrganizationId = org.Id,
			SeasonYear = year,
			Wins = regularWins + extraWins,
			Losses = regularLosses + extraLosses,
			RegularWins = regularWins,
			RegularLosses = regularLosses,
			DistrictFinish = finish,
			Postseason = postseason,
			WonDistrict = wonDistrict,
			WonRegional = wonRegional,
			WonState = wonState,
		};
	}

	private static (bool District, bool Regional, bool State) RollTitles(
		ProgramTalentTier tier,
		int finish,
		ISeededRandom rng)
	{
		if (finish > 4)
		{
			return (false, false, false);
		}

		bool wonDistrict = finish == 1 || (finish == 2 && Chance(tier, rng, 0.08, 0.18, 0.04));
		if (!wonDistrict)
		{
			return (false, false, false);
		}

		bool wonRegional = Chance(tier, rng, 0.12, 0.38, 0.04);
		if (!wonRegional)
		{
			return (true, false, false);
		}

		bool wonState = Chance(tier, rng, 0.06, 0.28, 0.02);
		return (true, true, wonState);
	}

	private static bool Chance(
		ProgramTalentTier tier,
		ISeededRandom rng,
		double average,
		double powerhouse,
		double rebuilding)
	{
		double p = tier switch
		{
			ProgramTalentTier.Powerhouse => powerhouse,
			ProgramTalentTier.AboveAverage => (average + powerhouse) / 2,
			ProgramTalentTier.BelowAverage => (average + rebuilding) / 2,
			ProgramTalentTier.Rebuilding => rebuilding,
			_ => average,
		};
		return rng.NextDouble() < p;
	}

	private static (int Wins, int Losses) PostseasonGames(string postseason, ISeededRandom rng) =>
		postseason switch
		{
			ProgramPostseason.StateChampion => (5 + rng.Next(0, 3), rng.Next(0, 2)),
			ProgramPostseason.StateFinal => (4 + rng.Next(0, 2), 1),
			ProgramPostseason.RegionalFinal => (3 + rng.Next(0, 2), 1),
			ProgramPostseason.DistrictFinal => (2, 1),
			ProgramPostseason.DistrictSemifinal => (1, 1),
			ProgramPostseason.DistrictQuarterfinal => (0, 1),
			_ => (0, 0),
		};

	private static int CountTitles(
		List<ProgramSeasonResult> history,
		Func<ProgramSeasonResult, bool> won,
		ProgramTalentTier tier,
		ISeededRandom rng,
		int extraPowerhouse)
	{
		int counted = history.Count(won);
		int extra = tier switch
		{
			ProgramTalentTier.Powerhouse => rng.Next(0, extraPowerhouse + 1),
			ProgramTalentTier.AboveAverage => rng.Next(0, Math.Max(1, extraPowerhouse)),
			_ => 0,
		};
		return counted + extra;
	}

	private static List<ProgramRecordHolder> RollRecords(OrganizationState org, ISeededRandom rng)
	{
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string NextName()
		{
			for (int i = 0; i < 24; i++)
			{
				string name = $"{rng.Pick(NamePool.FirstNames)} {rng.Pick(NamePool.LastNames)}";
				if (used.Add(name))
				{
					return name;
				}
			}

			string fallback = $"{rng.Pick(NamePool.FirstNames)} {rng.Pick(NamePool.LastNames)} {used.Count}";
			used.Add(fallback);
			return fallback;
		}

		var holders = new List<ProgramRecordHolder>();
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.Hits, Scale(org.TalentTier, 118, 168, rng), 12, NextName, rng));
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.HomeRuns, Scale(org.TalentTier, 14, 34, rng), 4, NextName, rng));
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.RunsBattedIn, Scale(org.TalentTier, 78, 132, rng), 10, NextName, rng));
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.Doubles, Scale(org.TalentTier, 28, 48, rng), 4, NextName, rng));
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.Wins, Scale(org.TalentTier, 14, 28, rng), 3, NextName, rng));
		holders.AddRange(RollCategory(org.Id, ProgramRecordCategory.Strikeouts, Scale(org.TalentTier, 160, 320, rng), 22, NextName, rng));
		return ProgramRecordBook.Rank(org.Id, holders);
	}

	private static List<ProgramHallOfFameInductee> RollHall(OrganizationState org, ISeededRandom rng)
	{
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ProgramRecordHolder holder in org.RecordBook)
		{
			used.Add(holder.PlayerName);
		}

		string NextName()
		{
			for (int i = 0; i < 24; i++)
			{
				string name = $"{rng.Pick(NamePool.FirstNames)} {rng.Pick(NamePool.LastNames)}";
				if (used.Add(name))
				{
					return name;
				}
			}

			string fallback = $"{rng.Pick(NamePool.FirstNames)} {rng.Pick(NamePool.LastNames)} {used.Count}";
			used.Add(fallback);
			return fallback;
		}

		string[] positions = ["SS", "C", "CF", "1B", "3B", "LF", "HP", "P"];
		var hall = new List<ProgramHallOfFameInductee>();
		foreach (ProgramSeasonResult season in org.SeasonHistory)
		{
			bool pitcher = rng.NextDouble() < 0.32;
			string position = pitcher ? rng.Pick<string>(["HP", "P"]) : rng.Pick(positions[..6]);
			int years = rng.NextDouble() < 0.7 ? 4 : rng.Next(2, 4);
			var honors = new List<string>();
			if (season.WonState)
			{
				honors.Add("State Champion");
			}
			else if (season.WonRegional)
			{
				honors.Add("State Finalist");
			}
			else if (season.WonDistrict)
			{
				honors.Add("District Champion");
			}

			int mvpTimes = season.DistrictFinish <= 2
				? (org.TalentTier == ProgramTalentTier.Powerhouse && rng.NextDouble() < 0.45 ? 2 : 1)
				: rng.NextDouble() < 0.35 ? 1 : 0;
			if (mvpTimes == 2)
			{
				honors.Insert(0, "2\u00d7 District MVP");
			}
			else if (mvpTimes == 1)
			{
				honors.Insert(0, pitcher ? "Pitcher of the Year" : "District MVP");
			}

			if (honors.Count == 0)
			{
				honors.Add("All-District");
			}

			double average = 0.278 + (org.Profile.Reputation / 1400.0) + (rng.NextDouble() * 0.04);
			int wins = 12 + org.Profile.Reputation / 12 + rng.Next(0, 6);
			double era = Math.Clamp(3.40 - (org.Profile.Reputation / 80.0) + rng.NextDouble() * 0.4, 1.55, 4.20);
			hall.Add(new ProgramHallOfFameInductee
			{
				OrganizationId = org.Id,
				InductedYear = season.SeasonYear,
				PlayerName = NextName(),
				Position = position,
				FirstSeasonYear = ProgramHallOfFame.FirstSeasonYear(season.SeasonYear, years),
				LastSeasonYear = season.SeasonYear,
				CareerLine = pitcher
					? ProgramHallOfFame.PitchingLine(wins, Math.Max(2, 18 - wins / 3), era)
					: ProgramHallOfFame.BattingLine(average),
				Honors = ProgramHallOfFame.JoinHonors(honors),
			});
		}

		return hall;
	}

	private static List<ProgramRecordHolder> RollCategory(
		string organizationId,
		ProgramRecordCategory category,
		int top,
		int step,
		Func<string> nextName,
		ISeededRandom rng)
	{
		var list = new List<ProgramRecordHolder>(RecordSlots);
		int value = top;
		for (int rank = 1; rank <= RecordSlots; rank++)
		{
			list.Add(new ProgramRecordHolder
			{
				OrganizationId = organizationId,
				Category = category,
				Rank = rank,
				PlayerName = nextName(),
				Value = Math.Max(1, value),
			});
			value -= step + rng.Next(0, Math.Max(1, step / 2));
		}

		return list;
	}

	private static int Scale(ProgramTalentTier tier, int rebuilding, int powerhouse, ISeededRandom rng)
	{
		double t = tier switch
		{
			ProgramTalentTier.Powerhouse => 1.0,
			ProgramTalentTier.AboveAverage => 0.78,
			ProgramTalentTier.Average => 0.52,
			ProgramTalentTier.BelowAverage => 0.28,
			ProgramTalentTier.Rebuilding => 0.0,
			_ => 0.52,
		};
		int mid = rebuilding + (int)Math.Round((powerhouse - rebuilding) * t, MidpointRounding.AwayFromZero);
		int wobble = Math.Max(1, (powerhouse - rebuilding) / 10);
		return Math.Max(1, mid + rng.Next(-wobble, wobble + 1));
	}

	private static ProgramProfile RollIdentity(ProgramTalentTier tier, ISeededRandom rng)
	{
		int reputation = RollReputation(tier, rng);
		LetterGrade facilities = LetterGradeExtensions.FromScore(reputation).Shift(rng.Next(-1, 2));
		return new ProgramProfile
		{
			Reputation = reputation,
			Facilities = facilities,
			BaseballResources = facilities.Shift(rng.Next(-1, 2)),
			BaseballBudget = RollBudget(reputation, rng),
		};
	}

	private static int RollReputation(ProgramTalentTier tier, ISeededRandom rng)
	{
		var (min, max) = tier switch
		{
			ProgramTalentTier.Powerhouse => (78, 94),
			ProgramTalentTier.AboveAverage => (64, 80),
			ProgramTalentTier.Average => (50, 66),
			ProgramTalentTier.BelowAverage => (36, 52),
			ProgramTalentTier.Rebuilding => (22, 40),
			_ => (50, 66),
		};
		return rng.Next(min, max + 1);
	}

	private static int RollWins(ProgramTalentTier tier, ISeededRandom rng)
	{
		var (min, max) = tier switch
		{
			ProgramTalentTier.Powerhouse => (18, 24),
			ProgramTalentTier.AboveAverage => (15, 21),
			ProgramTalentTier.Average => (11, 16),
			ProgramTalentTier.BelowAverage => (7, 13),
			ProgramTalentTier.Rebuilding => (4, 10),
			_ => (11, 16),
		};
		return Math.Clamp(rng.Next(min, max + 1), 0, ProgramProfile.SeasonGames);
	}

	private static int RollBudget(int reputation, ISeededRandom rng)
	{
		int raw = 18_000 + (reputation * 550) + rng.Next(-3_500, 3_501);
		int rounded = (int)Math.Round(raw / 1000.0, MidpointRounding.AwayFromZero) * 1000;
		return Math.Clamp(rounded, 18_000, 95_000);
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
