using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class AutosavePreferencesTests
{
	[Fact]
	public void Defaults_save_after_every_listed_trigger()
	{
		var prefs = new AutosavePreferences();
		Assert.True(prefs.Enabled);
		Assert.True(prefs.ShouldSave(AutosaveTrigger.Game));
		Assert.True(prefs.ShouldSave(AutosaveTrigger.MajorTransaction));
		Assert.True(prefs.ShouldSave(AutosaveTrigger.WeekAdvance));
		Assert.True(prefs.ShouldSave(AutosaveTrigger.PostseasonRound));
		Assert.True(prefs.ShouldSave(AutosaveTrigger.OffseasonTransition));
	}

	[Fact]
	public void Master_switch_silences_every_trigger()
	{
		var prefs = new AutosavePreferences { Enabled = false };
		Assert.False(prefs.ShouldSave(AutosaveTrigger.Game));
		Assert.False(prefs.ShouldSaveAny(
			AutosaveTrigger.WeekAdvance,
			AutosaveTrigger.OffseasonTransition));
	}

	[Fact]
	public void Per_trigger_flags_can_be_turned_off()
	{
		var prefs = new AutosavePreferences
		{
			AfterGame = false,
			AfterWeekAdvance = false,
		};
		Assert.False(prefs.ShouldSave(AutosaveTrigger.Game));
		Assert.False(prefs.ShouldSave(AutosaveTrigger.WeekAdvance));
		Assert.True(prefs.ShouldSave(AutosaveTrigger.MajorTransaction));
		Assert.True(prefs.ShouldSaveAny(AutosaveTrigger.Game, AutosaveTrigger.PostseasonRound));
	}

	[Fact]
	public void Trades_releases_and_pool_signings_are_major()
	{
		Assert.True(AutosavePreferences.IsMajorTransaction(PlayerTransactionKind.Trade));
		Assert.True(AutosavePreferences.IsMajorTransaction(PlayerTransactionKind.Release));
		Assert.True(AutosavePreferences.IsMajorTransaction(PlayerTransactionKind.OpenPool));
		Assert.False(AutosavePreferences.IsMajorTransaction(PlayerTransactionKind.Tryout));
		Assert.False(AutosavePreferences.IsMajorTransaction(PlayerTransactionKind.Promotion));
	}

	[Fact]
	public void Tournament_games_count_as_postseason()
	{
		Assert.True(GameType.DistrictTournament.IsPostseason());
		Assert.True(GameType.StateTournament.IsPostseason());
		Assert.True(GameType.CountyTournament.IsPostseason());
		Assert.False(GameType.District.IsPostseason());
		Assert.False(GameType.NonDistrict.IsPostseason());
	}
}
