using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class TryoutCampTests
{
	[Fact]
	public void Open_play_helper_matches_january_12_through_25()
	{
		SeasonEvent window = SeasonCalendar.OpenPlay(2026);
		Assert.Equal("open-play", window.Id);
		Assert.Equal(new DateOnly(2026, 1, 12), window.Start);
		Assert.Equal(new DateOnly(2026, 1, 25), window.End);
	}

	[Fact]
	public void Overall_read_is_a_range_around_the_true_number()
	{
		string shown = TryoutScouting.FormatOverall(61, looks: 1, salt: 4);
		Assert.Contains('-', shown);
		Assert.NotEqual("61", shown);
		string[] parts = shown.Split('-');
		Assert.Equal(2, parts.Length);
		Assert.True(int.Parse(parts[0]) <= 61);
		Assert.True(int.Parse(parts[1]) >= 61);
	}

	[Fact]
	public void Day_one_potential_is_always_hidden()
	{
		Assert.Equal("?", TryoutScouting.FormatPotential(61, 78, looks: 1, salt: 1));
		Assert.Equal("?", TryoutScouting.FormatPotential(50, 90, looks: 1, salt: 99));
	}

	[Fact]
	public void Potential_read_is_a_range_not_the_true_number()
	{
		for (int salt = 0; salt < 40; salt++)
		{
			string shown = TryoutScouting.FormatPotential(58, 74, looks: 3, salt);
			Assert.NotEqual("?", shown);
			Assert.NotEqual("74", shown);
			Assert.Contains('-', shown);
		}
	}

	[Fact]
	public void High_upside_stays_hidden_until_the_third_look()
	{
		Assert.Equal("?", TryoutScouting.FormatPotential(55, 80, looks: 2, salt: 3));
		string late = TryoutScouting.FormatPotential(55, 80, looks: 3, salt: 3);
		Assert.NotEqual("?", late);
		Assert.NotEqual("80", late);
	}

	[Fact]
	public void April_unlocks_the_full_open_play_look()
	{
		SeasonEvent window = SeasonCalendar.OpenPlay(2026);
		Assert.Equal(5, DistrictOpenPlay.LooksOn(new DateOnly(2026, 4, 24), window));
		Assert.True(TryoutCamp.HasPlayed(new DateOnly(2026, 4, 24), window, TryoutDay.Final));
	}

	[Fact]
	public void Opening_day_of_open_play_is_one_look()
	{
		SeasonEvent window = SeasonCalendar.OpenPlay(2026);
		Assert.Equal(1, DistrictOpenPlay.LooksOn(window.Start, window));
		Assert.Equal(5, DistrictOpenPlay.LooksOn(window.End, window));
	}

	[Fact]
	public void Visible_open_play_line_grows_with_looks()
	{
		var full = new OpenPlayLine
		{
			PlayerId = Guid.NewGuid(),
			Hitting = new TryoutHittingLine
			{
				Games = 5,
				AtBats = 20,
				Hits = 10,
			},
			Grade = LetterGrade.B,
		};
		var session = new DistrictOpenPlay(
			"great-lakes",
			2026,
			SeasonCalendar.OpenPlay(2026),
			[full.PlayerId],
			[full]);

		OpenPlayLine? first = session.VisibleLine(full.PlayerId, session.Window.Start);
		Assert.NotNull(first);
		Assert.Equal(1, first!.Games);
		Assert.InRange(first.Hitting.AtBats, 3, 5);

		OpenPlayLine? last = session.VisibleLine(full.PlayerId, session.Window.End);
		Assert.NotNull(last);
		Assert.Equal(5, last!.Games);
		Assert.Equal(20, last.Hitting.AtBats);
	}

	[Fact]
	public void Offer_can_cover_the_full_invite_class()
	{
		TryoutCamp camp = SampleCamp();
		foreach (TryoutProspect prospect in camp.Prospects)
		{
			Assert.True(camp.TryOffer(prospect.Id));
		}

		Assert.Equal(TryoutCamp.InviteCount, camp.OfferedCount);
		Assert.True(camp.TryOffer(camp.Prospects[^1].Id));
		Assert.False(camp.HasPendingDecisions);
	}

	[Fact]
	public void Fresh_camp_still_has_pending_decisions()
	{
		TryoutCamp camp = SampleCamp();
		Assert.True(camp.HasPendingDecisions);
		Assert.Equal(TryoutCamp.InviteCount, camp.PendingDecisionCount);
	}

	private static TryoutCamp SampleCamp()
	{
		var prospects = new List<TryoutProspect>();
		for (int i = 0; i < TryoutCamp.InviteCount; i++)
		{
			prospects.Add(new TryoutProspect
			{
				Id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}"),
				FirstName = "Player",
				LastName = $"{i:00}",
				Position = PlayerPosition.Shortstop,
				Throws = ThrowingHand.Right,
				Year = PlayerGrade.Freshman,
				TrueOverall = 55,
				TruePotential = 65,
				Day1Grade = LetterGrade.B,
				Day2Grade = LetterGrade.B,
				Day3Grade = LetterGrade.B,
				Day1Note = "Look 1",
				Day2Note = "Look 2",
				Day3Note = "Look 3",
			});
		}

		return new TryoutCamp(2026, SeasonCalendar.TryoutWeek(2026), prospects);
	}
}
