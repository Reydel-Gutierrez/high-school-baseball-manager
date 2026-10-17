using System.Globalization;

namespace HSBM.Domain;

public sealed record BoardScoreContext(
	int Wins,
	int Losses,
	int DistrictPlace,
	bool WonDistrict,
	bool WonRegional,
	bool WonState,
	bool TargetMet,
	bool RequiredMet,
	bool RequiredResolved,
	bool TargetResolved,
	int FinancePressure);

/// <summary>
/// Year-end board math: postseason copy, target checks, and the
/// satisfaction / reputation / budget deltas the recap screen shows.
/// </summary>
public static class SeasonReview
{
	public static string PostseasonLabel(bool wonDistrict, bool wonRegional, bool wonState, int districtPlace)
	{
		if (wonState)
		{
			return "State Champions";
		}

		if (wonRegional)
		{
			return "State Final";
		}

		if (wonDistrict)
		{
			return "Regional Final";
		}

		if (districtPlace <= 4)
		{
			return "District Playoffs";
		}

		return "Missed Playoffs";
	}

	public static string TargetLabel(BoardExpectation target) => target switch
	{
		BoardExpectation.Rebuild => "Rebuild",
		BoardExpectation.WinningSeason => "Post a Winning Season",
		BoardExpectation.Compete => "Compete",
		BoardExpectation.DistrictPlayoffs => "Reach District Playoffs",
		BoardExpectation.WinDistrict => "Win District",
		BoardExpectation.StateTitle => "Win State Title",
		_ => target.ToLabel(),
	};

	public static bool TargetMet(
		BoardExpectation target,
		int wins,
		int losses,
		int districtPlace,
		bool wonDistrict,
		bool wonRegional,
		bool wonState)
	{
		bool winning = wins > losses;
		bool playoffs = districtPlace <= 4 || wonDistrict;
		return target switch
		{
			BoardExpectation.Rebuild => true,
			BoardExpectation.WinningSeason => winning,
			BoardExpectation.Compete => winning || playoffs,
			BoardExpectation.DistrictPlayoffs => playoffs,
			BoardExpectation.WinDistrict => wonDistrict || districtPlace == 1,
			BoardExpectation.StateTitle => wonState,
			_ => winning,
		};
	}

	public static int OpeningSatisfaction(BoardSatisfaction current) => current switch
	{
		BoardSatisfaction.Poor => 32,
		BoardSatisfaction.Fair => 48,
		BoardSatisfaction.Good => 76,
		BoardSatisfaction.Excellent => 88,
		_ => 62,
	};

	/// <summary>
	/// Tight books worry the board. Healthy leftover cash is a small plus.
	/// </summary>
	public static int FinancePressure(int baseballBudget, int available)
	{
		if (baseballBudget <= 0)
		{
			return 0;
		}

		double share = Math.Clamp(available / (double)baseballBudget, 0, 1);
		if (share <= 0.02)
		{
			return -8;
		}

		if (share <= 0.10)
		{
			return -4;
		}

		if (share >= 0.50)
		{
			return 2;
		}

		return 0;
	}

	public static int Score(int opening, BoardScoreContext context)
	{
		int score = opening;
		if (context.Wins > context.Losses)
		{
			score += 6;
		}
		else if (context.Losses > context.Wins + 3)
		{
			score -= 4;
		}

		if (context.DistrictPlace <= 4)
		{
			score += 4;
		}

		if (context.DistrictPlace == 1)
		{
			score += 3;
		}

		if (context.WonDistrict)
		{
			score += 5;
		}

		if (context.WonRegional)
		{
			score += 4;
		}

		if (context.WonState)
		{
			score += 6;
		}

		if (context.RequiredResolved && !context.RequiredMet)
		{
			score -= 10;
		}
		else if (context.TargetResolved && !context.TargetMet)
		{
			score -= 8;
		}

		if (context.RequiredResolved && context.RequiredMet)
		{
			score += 2;
		}

		score += context.FinancePressure;
		return Math.Clamp(score, 5, 99);
	}

	public static int ClosingSatisfaction(
		int opening,
		int wins,
		int losses,
		int districtPlace,
		bool wonDistrict,
		bool wonRegional,
		bool wonState,
		bool targetMet) =>
		Score(
			opening,
			new BoardScoreContext(
				wins,
				losses,
				districtPlace,
				wonDistrict,
				wonRegional,
				wonState,
				targetMet,
				targetMet,
				RequiredResolved: true,
				TargetResolved: true,
				FinancePressure: 0));

	public static int ClosingSatisfaction(
		int opening,
		int wins,
		int losses,
		int districtPlace,
		bool wonDistrict,
		bool wonRegional,
		bool wonState,
		bool requiredMet,
		bool targetMet,
		int financePressure) =>
		Score(
			opening,
			new BoardScoreContext(
				wins,
				losses,
				districtPlace,
				wonDistrict,
				wonRegional,
				wonState,
				targetMet,
				requiredMet,
				RequiredResolved: true,
				TargetResolved: true,
				financePressure));

	public static BoardSatisfaction ToSatisfaction(int score) => score switch
	{
		>= 85 => BoardSatisfaction.Excellent,
		>= 70 => BoardSatisfaction.Good,
		>= 55 => BoardSatisfaction.Average,
		>= 40 => BoardSatisfaction.Fair,
		_ => BoardSatisfaction.Poor,
	};

	public static int ReputationGain(
		bool winning,
		bool playoffs,
		bool wonDistrict,
		bool wonRegional,
		bool wonState,
		bool targetMet)
	{
		int gain = 0;
		if (winning)
		{
			gain += 2;
		}

		if (playoffs)
		{
			gain += 2;
		}

		if (wonDistrict)
		{
			gain += 2;
		}

		if (wonRegional)
		{
			gain += 2;
		}

		if (wonState)
		{
			gain += 3;
		}

		if (!targetMet)
		{
			gain -= 3;
		}

		return gain;
	}

	public static int ClosingReputation(int opening, int gain) =>
		Math.Clamp(opening + gain, 20, 95);

	public static int FinanceDelta(
		int wins,
		int losses,
		bool playoffs,
		bool wonDistrict,
		bool wonRegional,
		bool wonState)
	{
		int purse = (wins - losses) * 200;
		if (playoffs)
		{
			purse += 2_000;
		}

		if (wonDistrict)
		{
			purse += 2_000;
		}

		if (wonRegional)
		{
			purse += 1_800;
		}

		if (wonState)
		{
			purse += 2_400;
		}

		return purse;
	}

	public static string FormatMoneyDelta(int value)
	{
		string abs = Math.Abs(value).ToString("#,##0", CultureInfo.InvariantCulture);
		if (value > 0)
		{
			return $"+${abs}";
		}

		if (value < 0)
		{
			return $"-${abs}";
		}

		return "$0";
	}
}
