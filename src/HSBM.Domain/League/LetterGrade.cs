namespace HSBM.Domain;

/// <summary>
/// High-school letter grades used for facilities and roster outlook.
/// </summary>
public enum LetterGrade
{
	F = 0,
	DMinus = 1,
	D = 2,
	DPlus = 3,
	CMinus = 4,
	C = 5,
	CPlus = 6,
	BMinus = 7,
	B = 8,
	BPlus = 9,
	AMinus = 10,
	A = 11,
	APlus = 12,
}

public static class LetterGradeExtensions
{
	public static string ToLabel(this LetterGrade grade) => grade switch
	{
		LetterGrade.APlus => "A+",
		LetterGrade.A => "A",
		LetterGrade.AMinus => "A-",
		LetterGrade.BPlus => "B+",
		LetterGrade.B => "B",
		LetterGrade.BMinus => "B-",
		LetterGrade.CPlus => "C+",
		LetterGrade.C => "C",
		LetterGrade.CMinus => "C-",
		LetterGrade.DPlus => "D+",
		LetterGrade.D => "D",
		LetterGrade.DMinus => "D-",
		_ => "F",
	};

	public static LetterGrade FromScore(double score)
	{
		int clamped = (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
		return clamped switch
		{
			>= 88 => LetterGrade.APlus,
			>= 82 => LetterGrade.A,
			>= 76 => LetterGrade.AMinus,
			>= 71 => LetterGrade.BPlus,
			>= 66 => LetterGrade.B,
			>= 61 => LetterGrade.BMinus,
			>= 56 => LetterGrade.CPlus,
			>= 51 => LetterGrade.C,
			>= 46 => LetterGrade.CMinus,
			>= 41 => LetterGrade.DPlus,
			>= 36 => LetterGrade.D,
			>= 30 => LetterGrade.DMinus,
			_ => LetterGrade.F,
		};
	}

	public static LetterGrade Shift(this LetterGrade grade, int delta)
	{
		int next = Math.Clamp((int)grade + delta, (int)LetterGrade.F, (int)LetterGrade.APlus);
		return (LetterGrade)next;
	}

	public static LetterGrade Blend(LetterGrade pitching, LetterGrade hitting, LetterGrade defense)
	{
		double mean = ((int)pitching + (int)hitting + (int)defense) / 3.0;
		int rounded = (int)Math.Round(mean, MidpointRounding.AwayFromZero);
		return (LetterGrade)Math.Clamp(rounded, (int)LetterGrade.F, (int)LetterGrade.APlus);
	}
}
