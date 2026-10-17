using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PowerRankingsTests
{
	[Fact]
	public void Better_record_and_reputation_ranks_first()
	{
		IReadOnlyList<PowerRankingRow> ranked = PowerRankings.Rank(
		[
			Entry("cascade", "Cascade", 18, 4, 80, 6, ["great-lakes"]),
			Entry("great-lakes", "Great Lakes", 12, 10, 55, 0, ["cascade"]),
		]);

		Assert.Equal("cascade", ranked[0].OrganizationId);
		Assert.Equal(1, ranked[0].Rank);
		Assert.Equal("great-lakes", ranked[1].OrganizationId);
	}

	[Fact]
	public void Equal_records_prefer_the_harder_slate()
	{
		IReadOnlyList<PowerRankingRow> ranked = PowerRankings.Rank(
		[
			Entry("cascade", "Cascade", 15, 7, 70, 0, ["elite-a", "elite-b"]),
			Entry("harbor", "Harbor Ridge", 15, 7, 70, 0, ["soft-a", "soft-b"]),
			Entry("elite-a", "Elite A", 20, 2, 80, 0, ["cascade"]),
			Entry("elite-b", "Elite B", 19, 3, 80, 0, ["cascade"]),
			Entry("soft-a", "Soft A", 4, 18, 40, 0, ["harbor"]),
			Entry("soft-b", "Soft B", 5, 17, 40, 0, ["harbor"]),
		]);

		Assert.True(RankOf(ranked, "cascade").Rank < RankOf(ranked, "harbor").Rank);
		Assert.True(RankOf(ranked, "cascade").StrengthOfSchedule > RankOf(ranked, "harbor").StrengthOfSchedule);
	}

	[Fact]
	public void Early_season_still_listens_to_reputation()
	{
		IReadOnlyList<PowerRankingRow> ranked = PowerRankings.Rank(
		[
			Entry("hot", "Hot Start", 2, 0, 40, 2, ["power"]),
			Entry("power", "Powerhouse", 1, 1, 90, 0, ["hot"]),
		]);

		Assert.Equal("power", ranked[0].OrganizationId);
	}

	[Fact]
	public void Win_streak_shows_as_a_rise_from_last_poll()
	{
		IReadOnlyList<PowerRankingRow> ranked = PowerRankings.Rank(
		[
			Entry("hot", "Hot", 10, 2, 70, 4, ["steady"]),
			Entry("steady", "Steady", 9, 3, 70, 0, ["hot"]),
		]);

		PowerRankingRow hot = RankOf(ranked, "hot");
		PowerRankingRow steady = RankOf(ranked, "steady");
		Assert.Equal(1, hot.Rank);
		Assert.True(hot.Movement > 0);
		Assert.True(steady.Movement < 0);
		Assert.Equal("\u2191", hot.MovementLabel);
		Assert.Equal("\u2193", steady.MovementLabel);
	}

	[Fact]
	public void No_streak_stays_flat()
	{
		IReadOnlyList<PowerRankingRow> ranked = PowerRankings.Rank(
		[
			Entry("a", "Alpha", 10, 4, 70, 0, ["b"]),
			Entry("b", "Beta", 8, 6, 60, 0, ["a"]),
		]);

		Assert.Equal(0, ranked[0].Movement);
		Assert.Equal("\u2014", ranked[0].MovementLabel);
	}

	private static PowerRankingRow RankOf(IReadOnlyList<PowerRankingRow> ranked, string id)
	{
		foreach (PowerRankingRow row in ranked)
		{
			if (row.OrganizationId == id)
			{
				return row;
			}
		}

		throw new InvalidOperationException(id);
	}

	private static PowerRankingEntry Entry(
		string id,
		string name,
		int wins,
		int losses,
		int reputation,
		int streak,
		string[] opponents) =>
		new(
			id,
			name,
			"great-lakes",
			"Great Lakes",
			wins,
			losses,
			wins * 7,
			losses * 5,
			reputation,
			streak,
			opponents);
}
