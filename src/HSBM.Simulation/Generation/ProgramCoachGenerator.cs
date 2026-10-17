using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Deterministic sitting staff for every generated program. Vacant seats
/// keep the outgoing name on file until a new coach is hired.
/// </summary>
public static class ProgramCoachGenerator
{
	public static List<ProgramCoach> Assign(
		IReadOnlyList<OrganizationState> organizations,
		ISeededRandom rng)
	{
		var coaches = new List<ProgramCoach>();
		foreach (OrganizationState org in organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			ISeededRandom local = rng.Derive(StableSalt(org.Id));
			var profile = new CoachProfile
			{
				Id = local.NextGuid(),
				FirstName = local.Pick(NamePool.FirstNames),
				LastName = local.Pick(NamePool.LastNames),
				Age = CoachIdentity.ClampAge(32 + local.Next(0, 29)),
				Hometown = org.ShortName,
				Appearance = PersonAppearanceGenerator.GenerateCoach(local.Next(int.MinValue, int.MaxValue)),
			};
			var coach = new ProgramCoach
			{
				Id = profile.Id,
				OrganizationId = org.Id,
				Profile = profile,
			};
			org.HeadCoach = coach;
			coaches.Add(coach);
		}

		return coaches;
	}

	private static int StableSalt(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return hash;
		}
	}
}
