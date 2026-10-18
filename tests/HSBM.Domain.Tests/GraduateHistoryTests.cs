using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class GraduateHistoryTests
{
	[Fact]
	public void Graduation_marks_seniors_alumni_and_stores_a_college_destination()
	{
		var senior = Hitter("Chris", "Morgan", PlayerGrade.Senior, 88, 92);
		var junior = Hitter("Quiet", "Role", PlayerGrade.Junior, 60, 64);
		int count = PlayerGraduation.Apply([senior, junior], 2026);

		Assert.Equal(1, count);
		Assert.True(senior.Alumni);
		Assert.False(junior.Alumni);
		Assert.Equal(2026, senior.GraduatedYear);
		Assert.True(senior.HasCollegeDestination);
		Assert.Equal("Division I", senior.CollegePath);
		Assert.Equal(CollegeCatalog.IdOf(senior.CollegeSchool), senior.CollegeId);
		Assert.Equal(CompetitionLevel.College, senior.CollegeDestination!.CompetitionLevel);
	}

	[Fact]
	public void Seniors_not_on_a_team_are_not_graduated_from_a_high_school_roster()
	{
		var unsigned = Hitter("Cut", "Senior", PlayerGrade.Senior, 70, 74);
		unsigned.OrganizationId = LeagueCatalog.UnsignedOrganizationId;
		unsigned.TeamId = LeagueCatalog.InactiveTeamId;

		Assert.False(PlayerGraduation.ShouldGraduate(unsigned));
		Assert.Equal(0, PlayerGraduation.Apply([unsigned], 2026));
		Assert.False(unsigned.Alumni);
		Assert.False(unsigned.HasCollegeDestination);
	}

	[Fact]
	public void History_keeps_alumni_and_compiles_high_school_stats()
	{
		Guid id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
		var player = Hitter("Mark", "Jones", PlayerGrade.Senior, 80, 82);
		player.Id = id;
		player.OrganizationId = "cascade";
		player.TeamId = "cascade-varsity";
		player.Graduate(2026, CollegeCatalog.Place(player.FullName, player.Overall, player.Potential));

		var batting = new List<BattingSeasonStats>
		{
			new()
			{
				PlayerId = id,
				SeasonYear = 2025,
				TeamId = "cascade-varsity",
				AtBats = 90,
				Hits = 30,
				HomeRuns = 6,
				RunsBattedIn = 22,
				StolenBases = 4,
			},
			new()
			{
				PlayerId = id,
				SeasonYear = 2026,
				TeamId = "cascade-varsity",
				AtBats = 80,
				Hits = 28,
				HomeRuns = 8,
				RunsBattedIn = 30,
				StolenBases = 5,
			},
		};

		IReadOnlyList<ProgramGraduate> alumni = GraduateHistory.For(
			[player],
			batting,
			[],
			"cascade",
			2026);

		ProgramGraduate row = Assert.Single(alumni);
		Assert.Equal(id, row.PlayerId);
		Assert.Equal(2, row.Seasons);
		Assert.NotNull(row.CareerBatting);
		Assert.Equal(170, row.CareerBatting.AtBats);
		Assert.Equal(14, row.CareerBatting.HomeRuns);
		Assert.Equal(9, row.CareerBatting.StolenBases);
		Assert.True(row.HasDestination);
		Assert.Equal(player.CollegeId, row.Destination.CollegeId);
	}

	[Fact]
	public void Upcoming_seniors_preview_a_destination_before_graduation_day()
	{
		var senior = Hitter("James", "Rodriguez", PlayerGrade.Senior, 76, 80);
		senior.OrganizationId = "cascade";
		IReadOnlyList<ProgramGraduate> upcoming = GraduateHistory.Upcoming(
			[senior],
			[],
			[],
			"cascade");

		ProgramGraduate row = Assert.Single(upcoming);
		Assert.Equal(0, row.GraduatedYear);
		Assert.True(row.HasDestination);
		Assert.Equal(CollegeCatalog.Place(senior.FullName, senior.Overall, senior.Potential).School, row.Destination.School);
	}

	private static Player Hitter(string first, string last, PlayerGrade grade, int overallHint, int potential)
	{
		return new Player
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			Grade = grade,
			OrganizationId = "cascade",
			TeamId = "cascade-varsity",
			PrimaryPosition = PlayerPosition.Shortstop,
			Potential = potential,
			Ratings = PlayerRatings.Clamp(overallHint, overallHint - 8, overallHint - 4, overallHint, overallHint, overallHint, 30, 30, 30, 40),
		};
	}
}
