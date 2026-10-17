namespace HSBM.Domain;

public readonly record struct PlayerRatings(
	int Contact,
	int Power,
	int PlateDiscipline,
	int Speed,
	int Fielding,
	int ArmStrength,
	int Stuff,
	int Control,
	int Movement,
	int Stamina)
{
	public const int Min = 1;
	public const int Max = 100;

	public static PlayerRatings Clamp(
		int contact,
		int power,
		int plateDiscipline,
		int speed,
		int fielding,
		int armStrength,
		int stuff,
		int control,
		int movement,
		int stamina) =>
		new(
			ClampValue(contact),
			ClampValue(power),
			ClampValue(plateDiscipline),
			ClampValue(speed),
			ClampValue(fielding),
			ClampValue(armStrength),
			ClampValue(stuff),
			ClampValue(control),
			ClampValue(movement),
			ClampValue(stamina));

	public static int ClampValue(int value) =>
		value < Min ? Min : value > Max ? Max : value;
}
