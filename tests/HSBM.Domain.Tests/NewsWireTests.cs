using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class NewsWireTests
{
	[Fact]
	public void Number_one_with_a_long_streak_writes_the_ranking_story()
	{
		NewsWorld world = World(
			new DateOnly(2026, 4, 24),
			Team("great-lakes", "Great Lakes", "great-lakes", 19, 3, 11),
			Team("cascade", "Cascade", "great-lakes", 16, 6, 2),
			Team("port-clinton", "Port Clinton", "north-shore", 15, 7, 1));

		List<NewsStory> stories = NewsWire.Discover(world, []);
		NewsStory? ranking = stories.FirstOrDefault(s => s.Tag == NewsTag.Rankings);

		Assert.NotNull(ranking);
		Assert.Contains("Great Lakes climbs to #1", ranking.Headline, StringComparison.Ordinal);
		Assert.Contains("11-game winning streak", ranking.Headline, StringComparison.Ordinal);
		Assert.Equal("great-lakes", ranking.SubjectOrganizationId);
	}

	[Fact]
	public void Freshman_average_writes_an_emerging_story()
	{
		NewsWorld world = World(
			new DateOnly(2026, 4, 18),
			[Team("cascade", "Cascade", "great-lakes", 12, 6, 3)],
			[
				new NewsPlayerSnapshot(
					Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1"),
					"Miguel Alvarez",
					"cascade",
					"Cascade",
					"Great Lakes",
					PlayerGrade.Freshman,
					"SS",
					42,
					0.381,
					3,
					18,
					0,
					0,
					0),
			]);

		List<NewsStory> stories = NewsWire.Discover(world, []);
		NewsStory? freshman = stories.FirstOrDefault(s => s.Tag == NewsTag.Freshman);

		Assert.NotNull(freshman);
		Assert.Equal("Freshman Miguel Alvarez emerging at Cascade.", freshman.Headline);
		Assert.Contains(".381", freshman.Body, StringComparison.Ordinal);
	}

	[Fact]
	public void Losing_start_puts_the_coach_on_the_wire()
	{
		NewsWorld world = World(
			new DateOnly(2026, 3, 22),
			Team("delaware-valley", "Delaware Valley", "great-lakes", 6, 13, -4),
			Team("cascade", "Cascade", "great-lakes", 14, 5, 5));

		List<NewsStory> stories = NewsWire.Discover(world, []);
		NewsStory? heat = stories.FirstOrDefault(s => s.Tag == NewsTag.Coach);

		Assert.NotNull(heat);
		Assert.Equal("Delaware Valley coach under pressure after 6–13 start.", heat.Headline);
	}

	[Fact]
	public void Playoff_selection_day_lists_the_qualifiers()
	{
		var teams = new List<NewsTeamSnapshot>();
		string[] districts = ["great-lakes", "north-shore", "capital-valley", "iron-range"];
		string[] names = ["Cascade", "Great Lakes", "Port Clinton", "Sandusky", "Capital City", "Springfield", "Hibbing", "Duluth East"];
		for (int i = 0; i < 8; i++)
		{
			string district = districts[i / 2];
			teams.Add(Team(names[i].ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal), names[i], district, 18 - i, 4 + i, 1, extra: i % 2));
			teams.Add(Team($"other-{i}", $"Other {i}", district, 8, 14, -2));
		}

		NewsWorld world = World(SeasonCalendar.PlayoffSelection(2026).Start, teams, []);
		List<NewsStory> stories = NewsWire.Discover(world, []);
		NewsStory? field = stories.FirstOrDefault(s => s.Tag == NewsTag.Playoffs && s.DedupKey.StartsWith("calendar:playoff-selection", StringComparison.Ordinal));

		Assert.NotNull(field);
		Assert.Contains("Regional brackets are official", field.Headline, StringComparison.Ordinal);
		Assert.Contains("Cascade", field.Body, StringComparison.Ordinal);
		Assert.Contains("Hibbing", field.Body, StringComparison.Ordinal);
	}

	[Fact]
	public void State_championship_open_finalizes_the_field()
	{
		NewsWorld world = World(SeasonCalendar.StateChampionship(2026).Start, Team("cascade", "Cascade", "great-lakes", 22, 6, 1));
		List<NewsStory> stories = NewsWire.Discover(world, []);
		NewsStory? state = stories.FirstOrDefault(s => s.Headline.Contains("State tournament field", StringComparison.Ordinal));

		Assert.NotNull(state);
		Assert.Equal(NewsTag.Playoffs, state.Tag);
	}

	[Fact]
	public void Open_play_start_is_a_calendar_story()
	{
		NewsWorld world = World(SeasonCalendar.OpenPlayStart(2026));
		List<NewsStory> stories = NewsWire.Discover(world, []);

		Assert.Contains(stories, s => s.Headline.Contains("District Open Play", StringComparison.Ordinal));
	}

	[Fact]
	public void The_same_condition_does_not_fire_twice()
	{
		NewsWorld world = World(
			new DateOnly(2026, 4, 24),
			Team("great-lakes", "Great Lakes", "great-lakes", 19, 3, 11),
			Team("cascade", "Cascade", "great-lakes", 16, 6, 2));
		List<NewsStory> first = NewsWire.Discover(world, []);
		Assert.NotEmpty(first);

		List<NewsStory> second = NewsWire.Discover(world, first);
		Assert.DoesNotContain(second, s => first.Any(existing => existing.DedupKey == s.DedupKey));
	}

	private static NewsWorld World(DateOnly date, params NewsTeamSnapshot[] teams) =>
		World(date, teams, []);

	private static NewsWorld World(
		DateOnly date,
		IReadOnlyList<NewsTeamSnapshot> teams,
		IReadOnlyList<NewsPlayerSnapshot> players)
	{
		return new NewsWorld
		{
			Date = date,
			SeasonYear = 2026,
			Teams = teams,
			Players = players,
			Districts =
			[
				new("great-lakes", "Great Lakes", "GL", "East"),
				new("north-shore", "North Shore", "NS", "East"),
				new("capital-valley", "Capital Valley", "CV", "West"),
				new("iron-range", "Iron Range", "IR", "West"),
			],
		};
	}

	private static NewsTeamSnapshot Team(
		string id,
		string shortName,
		string districtId,
		int wins,
		int losses,
		int streak,
		int extra = 0)
	{
		string districtName = districtId switch
		{
			"north-shore" => "North Shore",
			"capital-valley" => "Capital Valley",
			"iron-range" => "Iron Range",
			_ => "Great Lakes",
		};
		string region = districtId is "capital-valley" or "iron-range" ? "West" : "East";
		return new NewsTeamSnapshot(
			id + "-varsity",
			id,
			shortName,
			districtId,
			districtName,
			region,
			wins,
			losses,
			80 + extra,
			50,
			streak);
	}
}
