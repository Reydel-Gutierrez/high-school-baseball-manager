namespace HSBM.Domain;

/// <summary>
/// Head coach identity for a career. Appearance is the same
/// <see cref="PersonAppearance"/> used by players and staff.
/// </summary>
public sealed class CoachProfile
{
	public Guid Id { get; set; }
	public string FirstName { get; set; } = "";
	public string LastName { get; set; } = "";
	public int Age { get; set; } = CoachIdentity.DefaultAge;
	public string Hometown { get; set; } = "";
	public PersonAppearance Appearance { get; set; } = new();

	public string FullName
	{
		get
		{
			string first = FirstName.Trim();
			string last = LastName.Trim();
			if (first.Length == 0)
			{
				return last;
			}

			return last.Length == 0 ? first : $"{first} {last}";
		}
	}

	public CoachProfile Clone() => new()
	{
		Id = Id,
		FirstName = FirstName,
		LastName = LastName,
		Age = Age,
		Hometown = Hometown,
		Appearance = Appearance.Clone(),
	};
}

public static class CoachIdentity
{
	public const int MinAge = 21;
	public const int MaxAge = 80;
	public const int DefaultAge = 34;
	public const int MaxNameLength = 32;
	public const int MaxHometownLength = 48;

	public static int ClampAge(int age)
	{
		if (age < MinAge)
		{
			return MinAge;
		}

		return age > MaxAge ? MaxAge : age;
	}

	public static CoachIdentityValidation Validate(CoachProfile coach)
	{
		var errors = new List<string>();
		if (string.IsNullOrWhiteSpace(coach.FirstName))
		{
			errors.Add("First name is required.");
		}

		if (string.IsNullOrWhiteSpace(coach.LastName))
		{
			errors.Add("Last name is required.");
		}

		if (coach.Age < MinAge || coach.Age > MaxAge)
		{
			errors.Add($"Age must be between {MinAge} and {MaxAge}.");
		}

		if (string.IsNullOrWhiteSpace(coach.Hometown))
		{
			errors.Add("Hometown is required.");
		}

		return new CoachIdentityValidation(errors);
	}
}

public sealed class CoachIdentityValidation
{
	public CoachIdentityValidation(IReadOnlyList<string> errors)
	{
		Errors = errors;
	}

	public IReadOnlyList<string> Errors { get; }

	public bool IsValid => Errors.Count == 0;

	public string Hint
	{
		get
		{
			if (IsValid)
			{
				return "Ready to continue.";
			}

			if (Errors.Count == 1)
			{
				return Errors[0];
			}

			return "Fill in name, age, and hometown to continue.";
		}
	}
}
