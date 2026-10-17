namespace HSBM.Domain;

/// <summary>
/// When the clubhouse writes the active career after simulation
/// events. Master switch plus per-trigger flags; all default on.
/// </summary>
public enum AutosaveTrigger
{
	Game,
	MajorTransaction,
	WeekAdvance,
	PostseasonRound,
	OffseasonTransition,
}

public sealed class AutosavePreferences
{
	public bool Enabled { get; set; } = true;

	public bool AfterGame { get; set; } = true;

	public bool AfterMajorTransaction { get; set; } = true;

	public bool AfterWeekAdvance { get; set; } = true;

	public bool AfterPostseasonRound { get; set; } = true;

	public bool AfterOffseasonTransition { get; set; } = true;

	public bool ShouldSave(AutosaveTrigger trigger)
	{
		if (!Enabled)
		{
			return false;
		}

		return trigger switch
		{
			AutosaveTrigger.Game => AfterGame,
			AutosaveTrigger.MajorTransaction => AfterMajorTransaction,
			AutosaveTrigger.WeekAdvance => AfterWeekAdvance,
			AutosaveTrigger.PostseasonRound => AfterPostseasonRound,
			AutosaveTrigger.OffseasonTransition => AfterOffseasonTransition,
			_ => false,
		};
	}

	public bool ShouldSaveAny(params AutosaveTrigger[] triggers)
	{
		foreach (AutosaveTrigger trigger in triggers)
		{
			if (ShouldSave(trigger))
			{
				return true;
			}
		}

		return false;
	}

	public static bool IsMajorTransaction(PlayerTransactionKind kind) =>
		kind is PlayerTransactionKind.Trade
			or PlayerTransactionKind.Release
			or PlayerTransactionKind.OpenPool;
}
