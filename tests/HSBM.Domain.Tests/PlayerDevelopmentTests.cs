using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PlayerDevelopmentTests
{
	private static Player Prospect(PlayerPosition position, int rating, int potential, PlayerGrade grade = PlayerGrade.Freshman, PlayerTemperament temperament = PlayerTemperament.Composed) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = "Test",
			LastName = "Player",
			PrimaryPosition = position,
			Grade = grade,
			Personality = temperament,
			Ratings = new PlayerRatings(rating, rating, rating, rating, rating, rating, rating, rating, rating, rating),
			Potential = potential,
		};

	private static void PlaySeason(Player player, TrainingFocus focus, TrainingIntensity intensity, int seed)
	{
		var rng = new SeededRandom(seed);
		for (int week = 0; week < PlayerDevelopment.SeasonWeeks; week++)
		{
			PlayerDevelopment.Weekly(player, focus, intensity, LetterGrade.C, 0.8, rng);
		}
	}

	[Fact]
	public void Young_player_with_upside_improves_over_a_year()
	{
		Player player = Prospect(PlayerPosition.Shortstop, 50, 66);
		int start = player.Overall;
		PlayerDevelopment.Offseason(player, LetterGrade.C, 0.8, new SeededRandom(1));
		PlaySeason(player, TrainingFocus.Balanced, TrainingIntensity.Normal, 2);

		Assert.InRange(player.Overall - start, 3, 12);
	}

	[Fact]
	public void Growth_stops_at_potential_without_a_breakout()
	{
		for (int seed = 0; seed < 30; seed++)
		{
			Player player = Prospect(PlayerPosition.CenterField, 58, 61, PlayerGrade.Junior);
			for (int year = 0; year < 4; year++)
			{
				int potentialBefore = player.Potential;
				PlayerDevelopment.Offseason(player, LetterGrade.AMinus, 1.0, new SeededRandom(seed * 10 + year));
				PlaySeason(player, TrainingFocus.Balanced, TrainingIntensity.Hard, seed * 100 + year);
				Assert.True(player.Overall <= player.Potential, $"overall {player.Overall} above potential {player.Potential} (was {potentialBefore})");
			}
		}
	}

	[Fact]
	public void Peaked_player_does_not_grow()
	{
		Player player = Prospect(PlayerPosition.FirstBase, 70, 70, PlayerGrade.Senior);
		PlayerRatings before = player.Ratings;
		int potential = player.Potential;
		PlaySeason(player, TrainingFocus.Hitting, TrainingIntensity.Hard, 5);
		if (player.Potential == potential)
		{
			Assert.Equal(before, player.Ratings);
		}
	}

	[Fact]
	public void Coachable_players_outgrow_easygoing_ones_on_average()
	{
		int coachable = 0;
		int easygoing = 0;
		for (int seed = 0; seed < 60; seed++)
		{
			Player a = Prospect(PlayerPosition.SecondBase, 48, 70, temperament: PlayerTemperament.Coachable);
			Player b = Prospect(PlayerPosition.SecondBase, 48, 70, temperament: PlayerTemperament.Easygoing);
			PlaySeason(a, TrainingFocus.Balanced, TrainingIntensity.Normal, seed);
			PlaySeason(b, TrainingFocus.Balanced, TrainingIntensity.Normal, seed);
			coachable += a.Overall;
			easygoing += b.Overall;
		}

		Assert.True(coachable > easygoing);
	}

	[Fact]
	public void Training_focus_steers_which_ratings_improve()
	{
		int hittingContact = 0;
		int hittingFielding = 0;
		int defenseContact = 0;
		int defenseFielding = 0;
		for (int seed = 0; seed < 60; seed++)
		{
			Player hitter = Prospect(PlayerPosition.ThirdBase, 45, 75);
			Player glove = Prospect(PlayerPosition.ThirdBase, 45, 75);
			PlaySeason(hitter, TrainingFocus.Hitting, TrainingIntensity.Normal, seed);
			PlaySeason(glove, TrainingFocus.Defense, TrainingIntensity.Normal, seed);
			hittingContact += hitter.Ratings.Contact - 45;
			hittingFielding += hitter.Ratings.Fielding - 45;
			defenseContact += glove.Ratings.Contact - 45;
			defenseFielding += glove.Ratings.Fielding - 45;
		}

		Assert.True(hittingContact > defenseContact);
		Assert.True(defenseFielding > hittingFielding);
	}

	[Fact]
	public void Pitchers_only_grow_pitching_ratings()
	{
		Player arm = Prospect(PlayerPosition.StartingPitcher, 45, 72);
		PlayerRatings before = arm.Ratings;
		PlaySeason(arm, TrainingFocus.Pitching, TrainingIntensity.Hard, 9);

		Assert.Equal(before.Contact, arm.Ratings.Contact);
		Assert.Equal(before.Fielding, arm.Ratings.Fielding);
		Assert.True(arm.Ratings.Stuff + arm.Ratings.Control + arm.Ratings.Movement + arm.Ratings.Stamina
			> before.Stuff + before.Control + before.Movement + before.Stamina);
	}

	[Fact]
	public void Hard_training_grows_faster_than_light()
	{
		int hard = 0;
		int light = 0;
		for (int seed = 0; seed < 60; seed++)
		{
			Player a = Prospect(PlayerPosition.LeftField, 47, 72);
			Player b = Prospect(PlayerPosition.LeftField, 47, 72);
			PlaySeason(a, TrainingFocus.Balanced, TrainingIntensity.Hard, seed);
			PlaySeason(b, TrainingFocus.Balanced, TrainingIntensity.Light, seed);
			hard += a.Overall;
			light += b.Overall;
		}

		Assert.True(hard > light);
		Assert.True(PlayerDevelopment.PracticeInjuryChance(TrainingIntensity.Hard) > PlayerDevelopment.PracticeInjuryChance(TrainingIntensity.Light));
	}
}
