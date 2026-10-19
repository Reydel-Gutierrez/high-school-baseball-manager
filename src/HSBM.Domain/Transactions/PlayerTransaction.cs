using System.Globalization;

namespace HSBM.Domain;

public enum PlayerTransactionKind
{
	/// <summary>Legacy: high school programs don't trade players. Kept so old saves still load.</summary>
	Trade,
	OpenPool,
	Tryout,
	Release,
	Promotion,
	Injury,
	Graduation,
}

public sealed record PlayerTransaction(
	PlayerTransactionKind Kind,
	string DateLabel,
	string Headline,
	string Detail,
	DateOnly Date = default,
	string OrganizationId = "",
	Guid PlayerId = default,
	string DedupKey = "")
{
	public static string FormatDate(DateOnly date) =>
		date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();

	public static PlayerTransaction Tryout(
		string name,
		string position,
		string year,
		string squad,
		string team,
		string date) =>
		Signed(name, position, year, squad, team, date, PlayerTransactionKind.Tryout);

	public static PlayerTransaction Release(
		string name,
		string position,
		string year,
		string squad,
		string team,
		string date) =>
		new(
			PlayerTransactionKind.Release,
			date,
			$"{team} released {name}.",
			$"{position}  ·  {year}  ·  {squad}",
			DedupKey: $"Release:{team}:{name}:{date}");

	public static PlayerTransaction Promote(
		string name,
		string position,
		string year,
		string team,
		string date) =>
		new(
			PlayerTransactionKind.Promotion,
			date,
			$"{team} promoted {name} to Varsity.",
			$"{position}  ·  {year}  ·  Varsity",
			DedupKey: $"Promotion:{team}:{name}:{date}");

	/// <summary>A tryout candidate made the team on Decision Day.</summary>
	public static PlayerTransaction MadeTeam(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date) =>
		From(
			PlayerTransactionKind.Tryout,
			date,
			organizationId,
			player,
			$"{player.FullName} made the {schoolShortName} {player.TeamLevel.ToLabel()} roster.",
			player.TeamLevel.ToLabel());

	/// <summary>A tryout candidate was cut on Decision Day.</summary>
	public static PlayerTransaction CutAtTryouts(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date) =>
		From(
			PlayerTransactionKind.Release,
			date,
			organizationId,
			player,
			$"{schoolShortName} cut {player.FullName} after tryouts.",
			"Tryouts");

	public static PlayerTransaction Released(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date) =>
		From(
			PlayerTransactionKind.Release,
			date,
			organizationId,
			player,
			$"{schoolShortName} released {player.FullName}.",
			player.TeamLevel.ToLabel());

	public static PlayerTransaction Injured(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date,
		PlayerInjury injury) =>
		From(
			PlayerTransactionKind.Injury,
			date,
			organizationId,
			player,
			$"{player.FullName} is {injury.StatusLabel.ToLowerInvariant()} with a {injury.Kind.ToLowerInvariant()}.",
			injury.TimelineLabel);

	public static PlayerTransaction Promoted(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date) =>
		From(
			PlayerTransactionKind.Promotion,
			date,
			organizationId,
			player,
			$"{schoolShortName} promoted {player.FullName} to Varsity.",
			"Varsity");

	public static PlayerTransaction Graduated(
		Player player,
		string schoolShortName,
		string organizationId,
		DateOnly date)
	{
		string dest = player.HasCollegeDestination
			? player.CollegeSchool
			: "the next chapter";
		return From(
			PlayerTransactionKind.Graduation,
			date,
			organizationId,
			player,
			$"{schoolShortName} graduated {player.FullName}.",
			dest);
	}

	private static PlayerTransaction Signed(
		string name,
		string position,
		string year,
		string squad,
		string team,
		string date,
		PlayerTransactionKind kind) =>
		new(
			kind,
			date,
			$"{team} signed {name}.",
			$"{position}  ·  {year}  ·  {squad}",
			DedupKey: $"{kind}:{team}:{name}:{date}");

	private static PlayerTransaction From(
		PlayerTransactionKind kind,
		DateOnly date,
		string organizationId,
		Player player,
		string headline,
		string squad)
	{
		string label = FormatDate(date);
		return new(
			kind,
			label,
			headline,
			$"{player.DisplayPosition}  ·  {player.Grade.ToAbbrev()}  ·  {squad}",
			date,
			organizationId,
			player.Id,
			$"{kind}:{organizationId}:{player.Id:N}:{date:yyyyMMdd}");
	}
}
