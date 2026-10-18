using System.Globalization;

namespace HSBM.Domain;

public static class Money
{
	public static string Format(int amount)
	{
		if (amount <= 0)
		{
			return "$0";
		}

		return "$" + amount.ToString("N0", CultureInfo.InvariantCulture);
	}
}
