namespace HSBM.Domain;

public enum PlayerTransactionKind
{
	Trade,
	OpenPool,
	Tryout,
	Release,
}

public sealed record PlayerTransaction(
	PlayerTransactionKind Kind,
	string DateLabel,
	string Headline,
	string Detail)
{
	public static PlayerTransaction Trade(
		string name,
		string position,
		string year,
		string squad,
		string toTeam,
		string fromTeam,
		string date) =>
		new(
			PlayerTransactionKind.Trade,
			date,
			$"{name} has been traded to {toTeam} from {fromTeam}.",
			$"{position}  ·  {year}  ·  {squad}");

	public static PlayerTransaction SignedFromOpenPool(
		string name,
		string position,
		string year,
		string squad,
		string team,
		string date) =>
		new(
			PlayerTransactionKind.OpenPool,
			date,
			$"{name} has been added to {team} from the District Open Pool.",
			$"{position}  ·  {year}  ·  {squad}");

	public static PlayerTransaction Tryout(
		string name,
		string position,
		string year,
		string squad,
		string team,
		string date) =>
		new(
			PlayerTransactionKind.Tryout,
			date,
			$"{name} has been selected to {team}'s {squad} roster following freshman tryouts.",
			$"{position}  ·  {year}  ·  {squad}");

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
			$"{name} has been released by {team} and assigned to the District Open Pool.",
			$"{position}  ·  {year}  ·  {squad}");
}
