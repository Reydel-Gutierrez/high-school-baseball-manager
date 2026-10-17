using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class TryoutCampGeneratorTests
{
	[Fact]
	public void Camp_invites_thirty_two_and_reserves_twenty_spots()
	{
		TryoutCamp camp = TryoutCampGenerator.Generate(2026, new SeededRandom(2026 * 37 + 19));
		Assert.Equal(TryoutCamp.InviteCount, camp.Prospects.Count);
		Assert.Equal(20, TryoutCamp.RosterSpots);
		Assert.Contains(camp.Prospects, p => p.FullName == "Mason Price");
		Assert.Contains(camp.Prospects, p => p.FullName == "Luis Moreno");
		Assert.Contains(camp.Prospects, p => p.FullName == "Daniel Brooks");
	}

	[Fact]
	public void Same_seed_reproduces_the_invite_list()
	{
		TryoutCamp a = TryoutCampGenerator.Generate(2026, new SeededRandom(42));
		TryoutCamp b = TryoutCampGenerator.Generate(2026, new SeededRandom(42));
		Assert.Equal(
			string.Join("|", a.Prospects.Select(p => p.FullName)),
			string.Join("|", b.Prospects.Select(p => p.FullName)));
	}

	[Fact]
	public void Day_one_board_hides_every_potential()
	{
		TryoutCamp camp = TryoutCampGenerator.Generate(2026, new SeededRandom(7));
		foreach (TryoutProspect prospect in camp.Prospects)
		{
			Assert.Equal("?", TryoutScouting.FormatPotential(prospect, TryoutDay.Day1));
		}
	}

	[Fact]
	public void Final_board_still_withholds_exact_potential()
	{
		TryoutCamp camp = TryoutCampGenerator.Generate(2026, new SeededRandom(7));
		foreach (TryoutProspect prospect in camp.Prospects)
		{
			string shown = TryoutScouting.FormatPotential(prospect, TryoutDay.Final);
			Assert.NotEqual(prospect.TruePotential.ToString(System.Globalization.CultureInfo.InvariantCulture), shown);
			Assert.Contains('-', shown);
		}
	}

	[Fact]
	public void Camp_writes_box_scores_for_every_invite()
	{
		TryoutCamp camp = TryoutCampGenerator.Generate(2026, new SeededRandom(7));
		foreach (TryoutProspect prospect in camp.Prospects)
		{
			Assert.True(prospect.Day1Hitting.AtBats > 0);
			Assert.True(prospect.HittingThrough(TryoutDay.Final).AtBats >= prospect.Day1Hitting.AtBats);
			if (prospect.Position.IsPitcher())
			{
				Assert.NotNull(prospect.Day1Pitching);
				Assert.True(prospect.Day1Pitching!.Outs > 0);
			}
		}
	}

	[Fact]
	public void Camp_builds_three_blue_white_scrimmages_on_tryout_dates()
	{
		TryoutCamp camp = TryoutCampGenerator.Generate(2026, new SeededRandom(7));
		Assert.Equal(TryoutGame.GameCount, camp.Games.Count);
		Assert.Equal(new DateOnly(2026, 1, 12), camp.Games[0].Date);
		Assert.Equal(new DateOnly(2026, 1, 14), camp.Games[1].Date);
		Assert.Equal(new DateOnly(2026, 1, 16), camp.Games[2].Date);
		for (int i = 0; i < camp.Games.Count; i++)
		{
			TryoutGame game = camp.Games[i];
			Assert.Equal(i + 1, game.Number);
			Assert.True(game.BlueBatters.Count >= 8);
			Assert.True(game.WhiteBatters.Count >= 8);
			Assert.NotEmpty(game.BluePitchers);
			Assert.NotEmpty(game.WhitePitchers);
			Assert.True(game.BlueRuns + game.WhiteRuns > 0);
			Assert.Equal(TryoutGame.Innings, game.BlueInningRuns.Length);
			Assert.Equal(game.BlueRuns, game.BlueInningRuns.Sum());
			Assert.Equal(game.WhiteRuns, game.WhiteInningRuns.Sum());
			Assert.NotEmpty(game.Standouts);
		}

		Assert.Equal(camp.Games[0], camp.GameFor(TryoutDay.Day1));
		Assert.Equal(camp.Games[0], camp.GameOn(new DateOnly(2026, 1, 12)));
	}
}
