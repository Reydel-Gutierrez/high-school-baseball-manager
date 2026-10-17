using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PlayerTransactionTests
{
	[Fact]
	public void World_copy_matches_the_wire_lines()
	{
		Player promoted = Player("Luis", "Torres", 68);
		Player signed = Player("Mason", "Young", 60);
		Player released = Player("Diego", "Gonzalez", 48);
		var march12 = new DateOnly(2026, 3, 12);
		var march11 = new DateOnly(2026, 3, 11);
		var march9 = new DateOnly(2026, 3, 9);

		Assert.Equal(
			"Cascade promoted Luis Torres to Varsity.",
			PlayerTransaction.Promoted(promoted, "Cascade", "cascade", march12).Headline);
		Assert.Equal(
			"Great Lakes signed Mason Young.",
			PlayerTransaction.FromSigning(signed, "Great Lakes", "great-lakes", march11).Headline);
		Assert.Equal(
			"Palmetto released Diego Gonzalez.",
			PlayerTransaction.Released(released, "Palmetto", "palmetto", march9).Headline);
		Assert.Equal("MAR 12", PlayerTransaction.FormatDate(march12));
		Assert.Equal("MAR 9", PlayerTransaction.FormatDate(march9));
		Player senior = Player("Elena", "Voss", 72);
		senior.Grade = PlayerGrade.Senior;
		senior.CollegeSchool = "Ohio State";
		Assert.Equal(
			"Cascade graduated Elena Voss.",
			PlayerTransaction.Graduated(senior, "Cascade", "cascade", new DateOnly(2026, 6, 6)).Headline);
	}

	private static Player Player(string first, string last, int overall) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			PrimaryPosition = PlayerPosition.Shortstop,
			Grade = PlayerGrade.Sophomore,
			OrganizationId = "cascade",
			TeamId = "cascade-jv",
			TeamLevel = TeamLevel.JuniorVarsity,
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
		};
}
