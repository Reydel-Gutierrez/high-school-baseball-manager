using System.Globalization;

namespace HSBM.Domain;

public sealed class PlayerInjury
{
	public string Kind { get; set; } = "";
	public DateOnly Start { get; set; }
	public DateOnly AvailableOn { get; set; }
	public bool DayToDay { get; set; }

	public bool ActiveOn(DateOnly date) => date < AvailableOn;

	public int RemainingDays(DateOnly today) =>
		Math.Max(0, AvailableOn.DayNumber - today.DayNumber);

	public string StatusLabel => DayToDay ? "Day-to-Day" : "Unavailable";

	public string TimelineLabel
	{
		get
		{
			int days = Math.Max(1, AvailableOn.DayNumber - Start.DayNumber);
			return days == 1 ? "1 day" : $"{days} days";
		}
	}

	public string RemainingLabel(DateOnly today)
	{
		int left = RemainingDays(today);
		if (left <= 0)
		{
			return "Available";
		}

		return left == 1 ? "1 day remaining" : $"{left} days remaining";
	}

	public string FormatAvailableOn() =>
		AvailableOn.ToString("MMM d", CultureInfo.InvariantCulture);
}
