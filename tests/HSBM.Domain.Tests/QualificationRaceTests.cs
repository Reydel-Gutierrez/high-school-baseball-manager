using HSBM.Domain;

namespace HSBM.Domain.Tests;

public sealed class QualificationRaceTests
{
	private static StandingEntry Row(string id, int wins, int losses, int rank) =>
		new(id, id, id, "d", wins, losses, 0, 0, 0, 0, rank);

	[Fact]
	public void Early_season_nobody_is_eliminated()
	{
		var district = new List<StandingEntry>
		{
			Row("a", 5, 2, 1),
			Row("b", 4, 2, 2),
			Row("c", 2, 4, 3),
			Row("d", 0, 5, 4),
		};
		var remaining = new Dictionary<string, int> { ["a"] = 15, ["b"] = 15, ["c"] = 15, ["d"] = 15 };

		Dictionary<string, RaceStatus> race = QualificationRace.For(district, remaining);

		Assert.All(race.Values, status => Assert.Equal(RaceStatus.Alive, status));
	}

	[Fact]
	public void Team_that_cannot_reach_second_place_is_eliminated()
	{
		var district = new List<StandingEntry>
		{
			Row("a", 18, 2, 1),
			Row("b", 16, 4, 2),
			Row("c", 10, 10, 3),
		};
		var remaining = new Dictionary<string, int> { ["a"] = 2, ["b"] = 2, ["c"] = 2 };

		Dictionary<string, RaceStatus> race = QualificationRace.For(district, remaining);

		Assert.Equal(RaceStatus.Clinched, race["a"]);
		Assert.Equal(RaceStatus.Clinched, race["b"]);
		Assert.Equal(RaceStatus.Eliminated, race["c"]);
	}

	[Fact]
	public void Possible_tie_neither_clinches_nor_eliminates()
	{
		var district = new List<StandingEntry>
		{
			Row("a", 18, 2, 1),
			Row("b", 15, 5, 2),
			Row("c", 13, 7, 3),
		};
		var remaining = new Dictionary<string, int> { ["a"] = 2, ["b"] = 2, ["c"] = 2 };

		Dictionary<string, RaceStatus> race = QualificationRace.For(district, remaining);

		Assert.Equal(RaceStatus.Clinched, race["a"]);
		Assert.Equal(RaceStatus.Alive, race["b"]);
		Assert.Equal(RaceStatus.Alive, race["c"]);
	}

	[Fact]
	public void Finished_season_uses_final_rank()
	{
		var district = new List<StandingEntry>
		{
			Row("a", 12, 8, 1),
			Row("b", 12, 8, 2),
			Row("c", 12, 8, 3),
		};

		Dictionary<string, RaceStatus> race = QualificationRace.For(district, new Dictionary<string, int>());

		Assert.Equal(RaceStatus.Clinched, race["a"]);
		Assert.Equal(RaceStatus.Clinched, race["b"]);
		Assert.Equal(RaceStatus.Eliminated, race["c"]);
	}
}
