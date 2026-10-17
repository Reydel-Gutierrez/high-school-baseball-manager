using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class SeasonReviewTests
{
	[Fact]
	public void Postseason_label_follows_the_deepest_title_won()
	{
		Assert.Equal("State Champions", SeasonReview.PostseasonLabel(true, true, true, 1));
		Assert.Equal("State Final", SeasonReview.PostseasonLabel(true, true, false, 1));
		Assert.Equal("Regional Final", SeasonReview.PostseasonLabel(true, false, false, 1));
		Assert.Equal("District Playoffs", SeasonReview.PostseasonLabel(false, false, false, 3));
		Assert.Equal("Missed Playoffs", SeasonReview.PostseasonLabel(false, false, false, 6));
	}

	[Fact]
	public void District_playoff_target_is_met_from_place_or_a_district_title()
	{
		Assert.True(SeasonReview.TargetMet(BoardExpectation.DistrictPlayoffs, 18, 4, 2, false, false, false));
		Assert.True(SeasonReview.TargetMet(BoardExpectation.DistrictPlayoffs, 12, 14, 5, true, false, false));
		Assert.False(SeasonReview.TargetMet(BoardExpectation.DistrictPlayoffs, 8, 18, 7, false, false, false));
		Assert.True(SeasonReview.TargetMet(BoardExpectation.StateTitle, 20, 6, 1, true, true, true));
		Assert.False(SeasonReview.TargetMet(BoardExpectation.StateTitle, 20, 6, 1, true, true, false));
	}

	[Fact]
	public void Board_grade_rises_on_a_title_run_and_falls_when_the_target_is_missed()
	{
		int opening = SeasonReview.OpeningSatisfaction(BoardSatisfaction.Average);
		int closing = SeasonReview.ClosingSatisfaction(opening, 18, 4, 1, true, true, true, true);
		Assert.True(closing > opening);
		Assert.Equal(BoardSatisfaction.Excellent, SeasonReview.ToSatisfaction(closing));

		int miss = SeasonReview.ClosingSatisfaction(opening, 9, 17, 7, false, false, false, false);
		Assert.True(miss < opening);
		Assert.Equal(BoardSatisfaction.Fair, SeasonReview.ToSatisfaction(miss));
	}

	[Fact]
	public void Finance_purse_pays_out_for_a_deep_run()
	{
		int regional = SeasonReview.FinanceDelta(24, 8, true, true, false, false);
		Assert.Equal("+$7,200", SeasonReview.FormatMoneyDelta(regional));
		Assert.True(SeasonReview.FinanceDelta(18, 4, true, true, true, true) > regional);
	}

	[Fact]
	public void Board_review_is_a_featured_June_milestone()
	{
		SeasonEvent review = SeasonCalendar.BoardReview(2026);
		Assert.True(review.Featured);
		Assert.Equal(new DateOnly(2026, 6, 5), review.Start);
		Assert.True(SeasonCalendar.BoardReviewOn(2026, review.Start));
		Assert.False(SeasonCalendar.BoardReviewOn(2026, new DateOnly(2026, 6, 4)));
	}
}
