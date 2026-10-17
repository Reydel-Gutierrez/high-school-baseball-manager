namespace HSBM.Domain;

/// <summary>
/// Postseason copy on school history. Season review keeps a longer
/// "Missed Playoffs" line; the record table uses an em dash.
/// </summary>
public static class ProgramPostseason
{
	public const string None = "\u2014";
	public const string DistrictQuarterfinal = "District QF";
	public const string DistrictSemifinal = "District SF";
	public const string DistrictFinal = "District Final";
	public const string RegionalSemifinal = "Regional SF";
	public const string RegionalFinal = "Regional Final";
	public const string StateFinal = "State Final";
	public const string StateChampion = "State Champion";

	public static string HistoryLabel(bool wonDistrict, bool wonRegional, bool wonState, int districtFinish)
	{
		if (wonState)
		{
			return StateChampion;
		}

		if (wonRegional)
		{
			return StateFinal;
		}

		if (wonDistrict)
		{
			return RegionalFinal;
		}

		return districtFinish switch
		{
			1 => DistrictFinal,
			2 or 3 => DistrictSemifinal,
			4 => DistrictQuarterfinal,
			_ => None,
		};
	}

	/// <summary>
	/// Used when only district place is known (AI schools at year-end).
	/// Regular-season first place is treated as a district title.
	/// </summary>
	public static string FromDistrictFinish(int districtFinish) =>
		HistoryLabel(wonDistrict: districtFinish == 1, wonRegional: false, wonState: false, districtFinish);

	/// <summary>
	/// Live high-school path: top two go to the regional, then state.
	/// </summary>
	public static string FromPlayoff(bool wonState, bool wonRegional, bool wonSemi, bool qualified)
	{
		if (wonState)
		{
			return StateChampion;
		}

		if (wonRegional)
		{
			return StateFinal;
		}

		if (wonSemi)
		{
			return RegionalFinal;
		}

		return qualified ? RegionalSemifinal : None;
	}
}
