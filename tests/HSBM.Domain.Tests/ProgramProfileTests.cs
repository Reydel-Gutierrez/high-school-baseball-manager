using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class ProgramProfileTests
{
	[Fact]
	public void Reputation_sixty_one_reads_as_b_minus()
	{
		Assert.Equal(LetterGrade.BMinus, LetterGradeExtensions.FromScore(61));
		Assert.Equal("B-", LetterGrade.BMinus.ToLabel());
	}

	[Fact]
	public void Roster_outlook_blends_the_three_sides()
	{
		Assert.Equal(LetterGrade.B, LetterGradeExtensions.Blend(LetterGrade.BPlus, LetterGrade.B, LetterGrade.BMinus));
	}

	[Fact]
	public void Dismissal_reads_as_previous_coach_dismissed()
	{
		Assert.Equal("Previous coach dismissed", VacancyReason.Dismissal.ToLabel());
	}

	[Fact]
	public void Ordinals_use_st_nd_rd_th()
	{
		Assert.Equal("1ST", Ordinal.Format(1));
		Assert.Equal("2ND", Ordinal.Format(2));
		Assert.Equal("3RD", Ordinal.Format(3));
		Assert.Equal("4TH", Ordinal.Format(4));
		Assert.Equal("11TH", Ordinal.Format(11));
		Assert.Equal("22ND", Ordinal.Format(22));
	}

	[Fact]
	public void Display_name_drops_high_school_suffix()
	{
		Assert.Equal("Cascade Regional", OrganizationNaming.DisplayName("Cascade Regional High School"));
		Assert.Equal("Lakeside Academy", OrganizationNaming.DisplayName("Lakeside Academy"));
	}

	[Fact]
	public void Board_target_steps_down_to_a_minimum()
	{
		Assert.Equal("Playoffs", BoardExpectation.DistrictPlayoffs.ToShortLabel());
		Assert.Equal(BoardExpectation.WinningSeason, BoardExpectation.Compete.StepDown());
		Assert.Equal(BoardExpectation.Rebuild, BoardExpectation.Rebuild.StepDown());
	}

	[Fact]
	public void Coaching_carousel_is_june_tenth()
	{
		Assert.True(SeasonCalendar.CoachingCarouselOn(2026, new DateOnly(2026, 6, 10)));
		Assert.False(SeasonCalendar.CoachingCarouselOn(2026, new DateOnly(2026, 6, 5)));
	}

	[Fact]
	public void Unsigned_career_has_no_contract()
	{
		Assert.False(new Career().HasSignedContract);
		Assert.True(new Career { UserOrganizationId = "cascade" }.HasSignedContract);
	}

	[Fact]
	public void Licensure_letter_is_addressed_and_does_not_place_the_coach()
	{
		Assert.Equal("Hart", CoachingLicensure.LastNameOf("Daniel Hart"));
		string letter = CoachingLicensure.LetterBody("Daniel Hart");
		Assert.Contains("Coach Hart:", letter);
		Assert.Contains("Head Coach license", letter);
		Assert.Contains("not an appointment", letter);
		Assert.Contains("Athletic directors post vacancies", letter);
		Assert.Contains(CoachingLicensure.Director, letter);
	}
}
