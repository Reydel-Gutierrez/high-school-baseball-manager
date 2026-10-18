namespace HSBM.Domain;

/// <summary>
/// Lightweight program books: the annual baseball budget split into
/// operating allocations, plus local business sponsors. Sponsor cash is
/// already inside <see cref="ProgramProfile.BaseballBudget"/> — this is
/// the public face of that purse, not a second wallet.
/// </summary>
public static class ProgramFinances
{
	public static ProgramFinanceSnapshot Empty { get; } = new(
		0,
		0,
		0,
		[],
		Allocations(0),
		[]);

	public static ProgramFinanceSnapshot For(OrganizationState program, int coachSalary)
	{
		ProgramProfile profile = program.Profile;
		int salary = Math.Clamp(coachSalary, 0, Math.Max(0, profile.BaseballBudget));
		return new ProgramFinanceSnapshot(
			profile.BaseballBudget,
			Math.Max(0, profile.BaseballBudget - salary),
			salary,
			Spending(salary),
			Allocations(profile.BaseballBudget, salary),
			Sponsors(program.Id, program.ShortName, DistrictName(program.Id), profile.BaseballBudget));
	}

	public static IReadOnlyList<BudgetLine> Spending(int coachSalary)
	{
		int salary = Math.Max(0, coachSalary);
		return
		[
			new BudgetLine("Head Coach Stipend", salary, salary),
		];
	}

	public static IReadOnlyList<BudgetLine> Allocations(int baseballBudget) =>
		Allocations(baseballBudget, 0);

	/// <summary>
	/// What's left after the coaching stipend, split the way a high school
	/// baseball program actually spends: gear, buses, the field, uniforms.
	/// Always sums (with the stipend) to the full budget.
	/// </summary>
	public static IReadOnlyList<BudgetLine> Allocations(int baseballBudget, int coachSalary)
	{
		int total = Math.Max(0, baseballBudget);
		int salary = Math.Clamp(coachSalary, 0, total);
		int rest = total - salary;
		int equipment = Snap(rest * 0.30);
		int travel = Snap(rest * 0.22);
		int facilities = Snap(rest * 0.28);
		int uniforms = Snap(rest * 0.12);
		int misc = rest - equipment - travel - facilities - uniforms;
		if (misc < 0)
		{
			facilities = Math.Max(0, facilities + misc);
			misc = rest - equipment - travel - facilities - uniforms;
			if (misc < 0)
			{
				equipment = Math.Max(0, equipment + misc);
				misc = 0;
			}
		}

		return
		[
			new BudgetLine("Equipment", Math.Max(0, equipment)),
			new BudgetLine("Travel", Math.Max(0, travel)),
			new BudgetLine("Field & Facilities", Math.Max(0, facilities)),
			new BudgetLine("Uniforms", Math.Max(0, uniforms)),
			new BudgetLine("Misc.", Math.Max(0, misc)),
		];
	}

	public static IReadOnlyList<ProgramSponsor> Sponsors(
		string organizationId,
		string schoolShortName,
		string districtName,
		int baseballBudget)
	{
		int seed = StableSeed(organizationId);
		var rng = new SeededRandom(seed);
		string locality = Locality(schoolShortName, districtName);
		string[] banks = BanksFor(districtName, locality);
		var list = new List<ProgramSponsor>(3)
		{
			new(rng.Pick(banks), SnapSponsor(baseballBudget * LeadShare(rng)), false, ""),
			new("Local Sports Supply", 0, true, "Equipment sponsorship"),
		};

		if (baseballBudget >= 50_000)
		{
			string extra = rng.Pick(new[]
			{
				$"{locality} Auto Group",
				$"{locality} Hardware",
				$"{schoolShortName} Boosters",
			});
			list.Add(new ProgramSponsor(extra, SnapSponsor(baseballBudget * 0.05), false, ""));
		}

		return list;
	}

	private static string DistrictName(string organizationId)
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		if (!catalog.OrganizationDistricts.TryGetValue(organizationId, out string? districtId))
		{
			return "";
		}

		foreach (DistrictIdentity district in catalog.Districts)
		{
			if (district.Id == districtId)
			{
				return district.Name;
			}
		}

		return "";
	}

	private static string Locality(string schoolShortName, string districtName)
	{
		string name = schoolShortName.Trim();
		int space = name.IndexOf(' ');
		if (space > 0)
		{
			name = name[..space];
		}

		return string.IsNullOrWhiteSpace(name)
			? (string.IsNullOrWhiteSpace(districtName) ? "County" : districtName)
			: name;
	}

	private static string[] BanksFor(string districtName, string locality)
	{
		string key = districtName.Trim().ToLowerInvariant();
		return key switch
		{
			"great lakes" => ["South County Bank", "Lakeshore Bank", $"{locality} Trust"],
			"north shore" => ["North Shore Bank", "Lakefront Credit Union", $"{locality} Bank"],
			"capital valley" => ["Capital Valley Bank", "Valley Trust", $"{locality} Bank"],
			"iron range" => ["Range National Bank", "North Country Bank", $"{locality} Credit Union"],
			_ => [$"{locality} Bank", $"{locality} Credit Union", "County Trust"],
		};
	}

	private static double LeadShare(SeededRandom rng) =>
		0.10 + (rng.Next(0, 7) * 0.01);

	private static int Snap(double value)
	{
		if (value <= 0)
		{
			return 0;
		}

		return (int)Math.Round(value / 250.0, MidpointRounding.AwayFromZero) * 250;
	}

	private static int SnapSponsor(double value)
	{
		int snapped = (int)Math.Round(Math.Max(0, value) / 500.0, MidpointRounding.AwayFromZero) * 500;
		return Math.Clamp(snapped, 2_000, 12_000);
	}

	private static int StableSeed(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return hash == int.MinValue ? 17 : Math.Abs(hash);
		}
	}
}

public sealed record ProgramFinanceSnapshot(
	int BaseballBudget,
	int Available,
	int CoachSalary,
	IReadOnlyList<BudgetLine> Spending,
	IReadOnlyList<BudgetLine> Allocations,
	IReadOnlyList<ProgramSponsor> Sponsors)
{
	public int Spent
	{
		get
		{
			int total = 0;
			foreach (BudgetLine line in Spending)
			{
				total += line.Spent;
			}

			return total;
		}
	}

	public int AllocatedTotal
	{
		get
		{
			int total = 0;
			foreach (BudgetLine line in Allocations)
			{
				total += line.Amount;
			}

			return total;
		}
	}
}

public sealed record BudgetLine(string Label, int Amount, int Spent = 0)
{
	public int Remaining => Math.Max(0, Amount - Spent);

	public string AmountLabel => Money.Format(Amount);

	public string SpentLabel => Money.Format(Spent);

	public string RemainingLabel => Money.Format(Remaining);
}

public sealed record ProgramSponsor(string Name, int AnnualAmount, bool InKind, string Detail)
{
	public string AmountLabel =>
		InKind
			? (string.IsNullOrWhiteSpace(Detail) ? "In-kind support" : Detail)
			: $"{Money.Format(AnnualAmount)} / season";
}
