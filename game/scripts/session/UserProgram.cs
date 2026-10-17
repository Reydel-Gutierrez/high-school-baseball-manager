using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// The program the user currently coaches, resolved from the live session.
/// Every page that labels "your" school reads from here, never from a
/// hardcoded prototype id, so a career at any school renders correctly.
/// </summary>
public static class UserProgram
{
	public static string OrganizationId
	{
		get
		{
			string? id = GameSession.Current?.CatalogOrganizationId;
			return string.IsNullOrWhiteSpace(id) ? LeagueCatalog.DefaultUserOrganizationId : id;
		}
	}

	public static string Name
	{
		get
		{
			string? name = GameSession.Current?.Organization?.Name;
			if (!string.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			return CatalogName(OrganizationId);
		}
	}

	public static string ShortName
	{
		get
		{
			string? name = GameSession.Current?.Organization?.ShortName;
			if (!string.IsNullOrWhiteSpace(name))
			{
				return name;
			}

			try
			{
				return LeagueCatalog.Standard.GetOrganization(OrganizationId).ShortName;
			}
			catch (Exception)
			{
				return Name;
			}
		}
	}

	public static string NameUpper => Name.ToUpperInvariant();

	public static string DistrictId
	{
		get
		{
			try
			{
				return LeagueCatalog.Standard.DistrictIdFor(OrganizationId);
			}
			catch (Exception)
			{
				return "great-lakes";
			}
		}
	}

	public static string DistrictName
	{
		get
		{
			string id = DistrictId;
			foreach (DistrictIdentity district in LeagueCatalog.Standard.Districts)
			{
				if (district.Id == id)
				{
					return district.Name;
				}
			}

			return id;
		}
	}

	public static bool Is(string? organizationOrTeamId)
	{
		if (string.IsNullOrEmpty(organizationOrTeamId))
		{
			return false;
		}

		string org = OrganizationId;
		return organizationOrTeamId == org
			|| organizationOrTeamId == LeagueCatalog.TeamIdFor(org, TeamLevel.Varsity)
			|| organizationOrTeamId == LeagueCatalog.TeamIdFor(org, TeamLevel.JuniorVarsity);
	}

	public static Texture2D? Logo() => TeamLogos.Load(OrganizationId);

	/// <summary>Header crest that follows career/school changes on its own.</summary>
	public static ProgramMark MakeHeaderMark(float size = 84) => new(size) { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };

	/// <summary>
	/// Keeps a label showing the user's school (upper-case), optionally with
	/// a suffix such as "  ·  THE BOARD IS WATCHING". Updates on career change.
	/// </summary>
	public static void BindName(Label label, string suffix = "")
	{
		void Apply()
		{
			if (GodotObject.IsInstanceValid(label))
			{
				label.Text = NameUpper + suffix;
			}
		}

		Apply();
		GameSession? session = GameSession.Current;
		if (session == null)
		{
			return;
		}

		session.CareerChanged += Apply;
		session.ContractStateChanged += Apply;
		label.TreeExiting += () =>
		{
			session.CareerChanged -= Apply;
			session.ContractStateChanged -= Apply;
		};
		label.VisibilityChanged += Apply;
	}

	private static string CatalogName(string organizationId)
	{
		try
		{
			return LeagueCatalog.Standard.GetOrganization(organizationId).Name;
		}
		catch (Exception)
		{
			return "Your Program";
		}
	}
}
