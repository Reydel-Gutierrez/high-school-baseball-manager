using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class ProgramArchiveTests
{
	[Fact]
	public void History_label_names_the_deepest_round()
	{
		Assert.Equal("State Champion", ProgramPostseason.HistoryLabel(true, true, true, 1));
		Assert.Equal("State Final", ProgramPostseason.HistoryLabel(true, true, false, 1));
		Assert.Equal("Regional Final", ProgramPostseason.HistoryLabel(true, false, false, 1));
		Assert.Equal("District SF", ProgramPostseason.HistoryLabel(false, false, false, 2));
		Assert.Equal("District QF", ProgramPostseason.HistoryLabel(false, false, false, 4));
		Assert.Equal(ProgramPostseason.None, ProgramPostseason.HistoryLabel(false, false, false, 6));
		Assert.Equal("Regional Final", ProgramPostseason.FromDistrictFinish(1));
		Assert.Equal("Regional SF", ProgramPostseason.FromPlayoff(false, false, false, true));
	}

	[Fact]
	public void Record_book_keeps_alumni_and_promotes_live_players()
	{
		var alumni = new List<ProgramRecordHolder>
		{
			new()
			{
				OrganizationId = "cascade",
				Category = ProgramRecordCategory.Hits,
				Rank = 1,
				PlayerName = "Anthony Marini",
				Value = 164,
			},
			new()
			{
				OrganizationId = "cascade",
				Category = ProgramRecordCategory.Hits,
				Rank = 2,
				PlayerName = "James Rodriguez",
				Value = 151,
			},
		};
		Guid liveId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
		var names = new Dictionary<Guid, string> { [liveId] = "Mark Jones" };
		var batting = new List<BattingSeasonStats>
		{
			new()
			{
				PlayerId = liveId,
				SeasonYear = 2026,
				TeamId = "cascade-varsity",
				Hits = 80,
				HomeRuns = 18,
				RunsBattedIn = 61,
				Doubles = 14,
			},
			new()
			{
				PlayerId = liveId,
				SeasonYear = 2025,
				TeamId = "cascade-varsity",
				Hits = 90,
				HomeRuns = 16,
				RunsBattedIn = 55,
				Doubles = 12,
			},
		};

		List<ProgramRecordHolder> book = ProgramRecordBook.Compile(
			"cascade",
			alumni,
			names,
			batting,
			[],
			new HashSet<string>(StringComparer.Ordinal) { "cascade-varsity" });

		ProgramRecordHolder hitsLeader = book.Single(row =>
			row.Category == ProgramRecordCategory.Hits && row.Rank == 1);
		Assert.Equal("Mark Jones", hitsLeader.PlayerName);
		Assert.Equal(170, hitsLeader.Value);
		Assert.Equal("Anthony Marini", book.Single(row =>
			row.Category == ProgramRecordCategory.Hits && row.Rank == 2).PlayerName);
		Assert.Equal(34, book.Single(row =>
			row.Category == ProgramRecordCategory.HomeRuns && row.Rank == 1).Value);
	}

	[Fact]
	public void Replace_year_updates_an_existing_row_in_place()
	{
		var history = new List<ProgramSeasonResult>
		{
			new() { OrganizationId = "cascade", SeasonYear = 2025, Wins = 14, Losses = 12 },
		};
		ProgramRecordBook.ReplaceYear(history, new ProgramSeasonResult
		{
			OrganizationId = "cascade",
			SeasonYear = 2026,
			Wins = 18,
			Losses = 10,
			DistrictFinish = 2,
			Postseason = ProgramPostseason.DistrictSemifinal,
		});
		ProgramRecordBook.ReplaceYear(history, new ProgramSeasonResult
		{
			OrganizationId = "cascade",
			SeasonYear = 2026,
			Wins = 24,
			Losses = 8,
			DistrictFinish = 1,
			Postseason = ProgramPostseason.RegionalFinal,
		});

		Assert.Equal(2, history.Count);
		Assert.Equal(2025, history[0].SeasonYear);
		Assert.Equal(24, history[1].Wins);
		Assert.Equal(ProgramPostseason.RegionalFinal, history[1].Postseason);
	}

	[Fact]
	public void Hall_of_fame_keeps_one_plaque_per_year()
	{
		var hall = new List<ProgramHallOfFameInductee>
		{
			new()
			{
				OrganizationId = "cascade",
				InductedYear = 2025,
				PlayerName = "James Rodriguez",
				Position = "CF",
				FirstSeasonYear = 2022,
				LastSeasonYear = 2025,
				CareerLine = ProgramHallOfFame.BattingLine(0.311),
				Honors = ProgramHallOfFame.JoinHonors(["All-District"]),
			},
		};
		ProgramHallOfFame.ReplaceYear(hall, new ProgramHallOfFameInductee
		{
			OrganizationId = "cascade",
			InductedYear = 2026,
			PlayerName = "Anthony Marini",
			Position = "SS",
			FirstSeasonYear = 2023,
			LastSeasonYear = 2026,
			CareerLine = ProgramHallOfFame.BattingLine(0.334),
			Honors = ProgramHallOfFame.JoinHonors(["District MVP", "District MVP", "State Champion"]),
		});
		ProgramHallOfFame.ReplaceYear(hall, new ProgramHallOfFameInductee
		{
			OrganizationId = "cascade",
			InductedYear = 2026,
			PlayerName = "Anthony Marini",
			Position = "SS",
			FirstSeasonYear = 2023,
			LastSeasonYear = 2026,
			CareerLine = ProgramHallOfFame.BattingLine(0.334),
			Honors = "2× District MVP|State Champion",
		});

		Assert.Equal(2, hall.Count);
		Assert.Equal("Anthony Marini", hall[^1].PlayerName);
		Assert.Equal("2023–2026", hall[^1].YearsLabel);
		Assert.Equal(["2× District MVP", "State Champion"], hall[^1].HonorLines);
		Assert.Equal("Career .334 AVG", hall[^1].CareerLine);
	}
}
