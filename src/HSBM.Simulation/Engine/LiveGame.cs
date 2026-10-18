using HSBM.Domain;

namespace HSBM.Simulation;

public enum LivePlayKind
{
	HalfInning,
	PlateAppearance,
	StolenBase,
	CaughtStealing,
	Error,
	PitchingChange,
	PinchHitter,
	Bunt,
	IntentionalWalk,
	Final,
}

/// <summary>One line of play-by-play, with the scoreboard after it happened.</summary>
public sealed record LivePlay(
	LivePlayKind Kind,
	int Inning,
	bool TopHalf,
	string Text,
	int Outs,
	int AwayScore,
	int HomeScore,
	int RunsScored = 0,
	PlateAppearanceOutcome? Outcome = null,
	bool HomeTeamBatting = false);

/// <summary>
/// A baseball game played one plate appearance at a time. The automatic
/// path consumes randomness in exactly the same order as the original
/// single-pass simulator, so a game nobody touches produces the same box
/// score whether it is simulated or "watched". Managers can step in on
/// either side: steals, bunts, intentional walks, pitching changes, and
/// pinch hitters. Auto-management can be switched off per team so the
/// assistant stops making calls for the user's club.
/// </summary>
public sealed class LiveGame
{
	private readonly BaseballSimulationConfig _config;
	private readonly ISeededRandom _rng;
	private readonly ScheduledGame _game;
	private readonly TeamGamePlan _homePlan;
	private readonly TeamGamePlan _awayPlan;
	private readonly List<LineupBatter> _homeOrder;
	private readonly List<LineupBatter> _awayOrder;
	private readonly List<LineupBatter> _homeAppeared;
	private readonly List<LineupBatter> _awayAppeared;
	private readonly List<Player> _homeBench;
	private readonly List<Player> _awayBench;
	private readonly Dictionary<Guid, PlayerGameBatting> _batting = new();
	private readonly Dictionary<Guid, PlayerGameFielding> _fielding = new();
	private readonly List<string> _fieldingPlays = new();
	private readonly List<LivePlay> _log = new();
	private readonly PitchingStaff _homeStaff;
	private readonly PitchingStaff _awayStaff;
	private readonly List<int> _homeInningRuns;
	private readonly List<int> _awayInningRuns;
	private readonly BasePaths _bases = new();

	private int _inning = 1;
	private bool _top = true;
	private int _homeBatter;
	private int _awayBatter;
	private int _outs;
	private int _inningRuns;
	private bool _inningError;
	private bool _halfOpen;
	private bool _walkOff;
	private int _inningsPlayed;
	private Guid? _homeLeadPitcher;
	private Guid? _awayLeadPitcher;
	private int _paCount;
	private bool _buntQueued;
	private bool _walkQueued;
	private GameSimulationResult? _result;

	public LiveGame(
		ScheduledGame game,
		TeamGamePlan home,
		TeamGamePlan away,
		ISeededRandom rng,
		BaseballSimulationConfig? config = null,
		IReadOnlyList<Player>? homeBench = null,
		IReadOnlyList<Player>? awayBench = null)
	{
		if (home.BattingOrder.Count == 0 || away.BattingOrder.Count == 0)
		{
			throw new InvalidOperationException("Cannot simulate a game with an empty batting order.");
		}

		_game = game;
		_homePlan = home;
		_awayPlan = away;
		_rng = rng;
		_config = config ?? BaseballSimulationConfig.Default;
		_homeOrder = [.. home.BattingOrder];
		_awayOrder = [.. away.BattingOrder];
		_homeAppeared = [.. home.BattingOrder];
		_awayAppeared = [.. away.BattingOrder];
		_homeBench = BenchFrom(homeBench, home);
		_awayBench = BenchFrom(awayBench, away);
		EnsureBatting(_batting, _homeOrder);
		EnsureBatting(_batting, _awayOrder);
		_homeStaff = new PitchingStaff(home, _config, _batting);
		_awayStaff = new PitchingStaff(away, _config, _batting);
		_homeInningRuns = new List<int>(_config.MaxInnings);
		_awayInningRuns = new List<int>(_config.MaxInnings);
	}

	// ── Scoreboard state ────────────────────────────────────────────────

	public string HomeTeamId => _game.HomeTeamId;
	public string AwayTeamId => _game.AwayTeamId;
	public int Inning => _inning;
	public bool TopHalf => _top;
	public int Outs => _outs;
	public int HomeScore { get; private set; }
	public int AwayScore { get; private set; }
	public bool IsFinal => _result != null;
	public bool HomeBatting => !_top;
	public Player? OnFirst => _halfOpen ? _bases.First : null;
	public Player? OnSecond => _halfOpen ? _bases.Second : null;
	public Player? OnThird => _halfOpen ? _bases.Third : null;

	/// <summary>True between plate appearances inside a half-inning (bases and outs are live).</summary>
	public bool HalfInProgress => _halfOpen;
	public IReadOnlyList<int> HomeInningRuns => _homeInningRuns;
	public IReadOnlyList<int> AwayInningRuns => _awayInningRuns;
	public IReadOnlyList<LivePlay> Log => _log;
	public int RegulationInnings => _config.RegulationInnings;

	/// <summary>When false, the assistant stops steals and bullpen moves for that club.</summary>
	public bool AutoManageHome { get; set; } = true;

	public bool AutoManageAway { get; set; } = true;

	public int HomeHits => SumHits(_homeAppeared);
	public int AwayHits => SumHits(_awayAppeared);
	public int HomeErrors => SumErrors(_homeAppeared, _homePlan.StartingPitcher);
	public int AwayErrors => SumErrors(_awayAppeared, _awayPlan.StartingPitcher);

	public IReadOnlyList<LineupBatter> Lineup(bool home) => home ? _homeOrder : _awayOrder;

	public Player NextBatter
	{
		get
		{
			List<LineupBatter> order = _top ? _awayOrder : _homeOrder;
			int index = _top ? _awayBatter : _homeBatter;
			return order[index % order.Count].Player;
		}
	}

	public int NextBatterSlot => (_top ? _awayBatter : _homeBatter) % (_top ? _awayOrder : _homeOrder).Count;

	public Player Pitcher(bool home) => (home ? _homeStaff : _awayStaff).Current;

	public int PitchCount(bool home) => (home ? _homeStaff : _awayStaff).Pitches;

	public int PitchLimit(bool home) => (home ? _homeStaff : _awayStaff).PitchLimit;

	/// <summary>
	/// How spent the pitcher is, 0 (fresh) to 1 (fully gassed), as a share of
	/// the maximum fatigue the plate-appearance model applies.
	/// </summary>
	public double Fatigue(bool home) =>
		_config.MaxFatigue <= 0
			? 0
			: Math.Min(_config.MaxFatigue, (home ? _homeStaff : _awayStaff).Pitches * _config.FatiguePerPitch) / _config.MaxFatigue;

	public PlayerGameBatting? BattingLine(Guid playerId) =>
		_batting.TryGetValue(playerId, out PlayerGameBatting? line) ? line : null;

	public PlayerGamePitching? PitchingLine(bool home, Guid playerId) =>
		(home ? _homeStaff : _awayStaff).Find(playerId);

	public IReadOnlyList<Player> AvailableRelievers(bool home)
	{
		PitchingStaff staff = home ? _homeStaff : _awayStaff;
		var list = new List<Player>();
		foreach (Player arm in staff.Bullpen)
		{
			if (!staff.HasPitched(arm.Id))
			{
				list.Add(arm);
			}
		}

		if (staff.Closer != null && !staff.HasPitched(staff.Closer.Id))
		{
			list.Add(staff.Closer);
		}

		return list;
	}

	public IReadOnlyList<Player> AvailableBench(bool home) => home ? _homeBench : _awayBench;

	public bool CanSteal => !IsFinal && _halfOpen && ((_bases.First != null && _bases.Second == null) || (_bases.Second != null && _bases.Third == null));

	public bool CanBunt => !IsFinal && _halfOpen && _outs < 2 && (_bases.First != null || _bases.Second != null || _bases.Third != null);

	public bool BuntQueued => _buntQueued;

	public bool WalkQueued => _walkQueued;

	// ── Manager calls ───────────────────────────────────────────────────

	public void QueueBunt()
	{
		if (CanBunt)
		{
			_buntQueued = true;
			_walkQueued = false;
		}
	}

	public void QueueIntentionalWalk()
	{
		if (!IsFinal)
		{
			_walkQueued = true;
			_buntQueued = false;
		}
	}

	public void ClearQueuedCalls()
	{
		_buntQueued = false;
		_walkQueued = false;
	}

	/// <summary>Send the lead runner who has an open base ahead of him.</summary>
	public IReadOnlyList<LivePlay> Steal()
	{
		var emitted = new List<LivePlay>();
		if (!CanSteal)
		{
			return emitted;
		}

		List<LineupBatter> defenseOrder = _top ? _homeOrder : _awayOrder;
		PitchingStaff defense = _top ? _homeStaff : _awayStaff;
		Player? catcher = FielderAt(defenseOrder, PlayerPosition.Catcher);
		bool third = _bases.Second != null && _bases.Third == null && !(_bases.First != null && _bases.Second == null);
		Player runner = third ? _bases.Second! : _bases.First!;
		double edge = (runner.Ratings.Speed - (catcher?.Ratings.ArmStrength ?? 50)) / 100.0;
		double success = Clamp(0.68 + 0.22 * edge - (third ? 0.08 : 0), 0.38, 0.92);
		if (_rng.NextDouble() < success)
		{
			if (third)
			{
				_bases.Third = runner;
				_bases.Second = null;
			}
			else
			{
				_bases.Second = runner;
				_bases.First = null;
			}

			_batting[runner.Id].StolenBases++;
			string bag = third ? "third" : "second";
			_fieldingPlays.Add($"{runner.LastName} stole {bag}");
			emitted.Add(Emit(LivePlayKind.StolenBase, $"{runner.LastName} steals {bag}!"));
			return emitted;
		}

		if (third)
		{
			_bases.Second = null;
		}
		else
		{
			_bases.First = null;
		}

		_outs++;
		defense.Line.OutsRecorded++;
		if (catcher != null)
		{
			_fieldingPlays.Add($"{catcher.LastName} caught stealing, {runner.LastName} thrown out at {(third ? "third" : "second")}");
		}

		emitted.Add(Emit(LivePlayKind.CaughtStealing, $"{runner.LastName} is thrown out trying to steal {(third ? "third" : "second")}."));
		if (_outs >= 3)
		{
			EndHalf(emitted);
		}

		return emitted;
	}

	public bool ChangePitcher(bool home, Player next)
	{
		if (IsFinal)
		{
			return false;
		}

		PitchingStaff staff = home ? _homeStaff : _awayStaff;
		bool available = false;
		foreach (Player arm in AvailableRelievers(home))
		{
			if (arm.Id == next.Id)
			{
				available = true;
				break;
			}
		}

		if (!available)
		{
			return false;
		}

		int lead = home ? HomeScore - AwayScore : AwayScore - HomeScore;
		string outgoing = staff.Current.LastName;
		staff.ChangeTo(next, lead, _config);
		staff.Bullpen.RemoveAll(p => p.Id == next.Id);
		Emit(LivePlayKind.PitchingChange, $"Pitching change: {next.LastName} replaces {outgoing}.");
		return true;
	}

	/// <summary>Bench bat replaces the next scheduled hitter for the team at the plate.</summary>
	public bool PinchHit(Player bench)
	{
		if (IsFinal)
		{
			return false;
		}

		bool home = HomeBatting;
		List<Player> pool = home ? _homeBench : _awayBench;
		int benchIndex = pool.FindIndex(p => p.Id == bench.Id);
		if (benchIndex < 0)
		{
			return false;
		}

		List<LineupBatter> order = home ? _homeOrder : _awayOrder;
		int slot = NextBatterSlot;
		LineupBatter replaced = order[slot];
		var sub = new LineupBatter(bench, replaced.FieldPosition);
		order[slot] = sub;
		(home ? _homeAppeared : _awayAppeared).Add(sub);
		pool.RemoveAt(benchIndex);
		EnsureBatting(_batting, [sub]);
		Emit(LivePlayKind.PinchHitter, $"{bench.LastName} pinch-hits for {replaced.Player.LastName}.");
		return true;
	}

	// ── Flow ────────────────────────────────────────────────────────────

	/// <summary>Advance one plate appearance (plus any pre-pitch running). Returns what happened.</summary>
	public IReadOnlyList<LivePlay> Step()
	{
		var emitted = new List<LivePlay>();
		if (IsFinal)
		{
			return emitted;
		}

		EnsureHalfOpen(emitted);
		if (IsFinal)
		{
			return emitted;
		}

		PlayOne(emitted);
		return emitted;
	}

	/// <summary>Play until the current half-inning ends (or the game does).</summary>
	public IReadOnlyList<LivePlay> StepHalfInning()
	{
		var emitted = new List<LivePlay>();
		if (IsFinal)
		{
			return emitted;
		}

		EnsureHalfOpen(emitted);
		bool startedTop = _top;
		int startedInning = _inning;
		while (!IsFinal && _halfOpen && _top == startedTop && _inning == startedInning)
		{
			PlayOne(emitted);
		}

		return emitted;
	}

	public void RunToEnd()
	{
		while (!IsFinal)
		{
			Step();
		}
	}

	public GameSimulationResult Finish()
	{
		RunToEnd();
		return _result!;
	}

	// ── Engine (ported verbatim from the single-pass simulator) ───────

	private void EnsureHalfOpen(List<LivePlay> emitted)
	{
		while (!_halfOpen && !IsFinal)
		{
			if (_inning > _config.MaxInnings)
			{
				Seal(emitted);
				return;
			}

			_inningsPlayed = _inning;
			if (!_top && _inning >= _config.RegulationInnings && HomeScore > AwayScore)
			{
				Seal(emitted);
				return;
			}

			EnsureInning(_top ? _awayInningRuns : _homeInningRuns, _inning);
			_bases.Clear();
			_outs = 0;
			_inningRuns = 0;
			_inningError = false;
			_halfOpen = true;
			emitted.Add(Emit(LivePlayKind.HalfInning, $"{(_top ? "Top" : "Bottom")} of the {Ordinal(_inning)}."));
			PitchingStaff defense = _top ? _homeStaff : _awayStaff;
			MaybeChangePitcher(defense, emitted, startOfInning: true);
		}
	}

	private void PlayOne(List<LivePlay> emitted)
	{
		bool offenseAuto = _top ? AutoManageAway : AutoManageHome;
		List<LineupBatter> order = _top ? _awayOrder : _homeOrder;
		List<LineupBatter> defenseOrder = _top ? _homeOrder : _awayOrder;
		PitchingStaff defense = _top ? _homeStaff : _awayStaff;

		if (offenseAuto && TryAutoSteal(defenseOrder, defense, emitted))
		{
			_outs++;
			if (_outs >= 3)
			{
				EndHalf(emitted);
				return;
			}
		}

		ref int batterIndex = ref (_top ? ref _awayBatter : ref _homeBatter);
		LineupBatter slot = order[batterIndex % order.Count];
		batterIndex++;
		Player batter = slot.Player;
		PlayerGameBatting batterLine = _batting[batter.Id];
		_paCount++;

		if (_walkQueued)
		{
			_walkQueued = false;
			_buntQueued = false;
			IntentionalWalk(batter, batterLine, defense, emitted);
			return;
		}

		if (_buntQueued)
		{
			_buntQueued = false;
			Bunt(batter, batterLine, defenseOrder, defense, emitted);
			return;
		}

		double fatigue = Math.Min(_config.MaxFatigue, defense.Pitches * _config.FatiguePerPitch);
		PlateAppearanceOutcome outcome = PlateAppearanceResolver.Resolve(
			batter.Ratings,
			defense.Current.Ratings,
			_rng,
			_config,
			fatigue);

		int pitches = PitchesFor(outcome);
		defense.Pitches += pitches;
		ApplyPaToStats(batterLine, defense.Line, outcome);

		if (outcome is PlateAppearanceOutcome.Strikeout or PlateAppearanceOutcome.Out)
		{
			Player? glove = FielderFor(defenseOrder, defense.Current, outcome, _rng);
			if (outcome == PlateAppearanceOutcome.Out
				&& glove != null
				&& _rng.NextDouble() < ErrorRate(glove.Ratings.Fielding))
			{
				_inningError = true;
				Field(glove.Id).Errors++;
				_fieldingPlays.Add($"E{PositionCode(glove, defenseOrder)}: {glove.LastName}");
				emitted.Add(Emit(LivePlayKind.Error, $"{batter.LastName} reaches on an error by {PositionName(glove, defenseOrder)} {glove.LastName}."));
				outcome = PlateAppearanceOutcome.Single;
				Advance(batter, batterLine, defense, outcome, emitted, describe: false);
				return;
			}

			_outs++;
			defense.Line.OutsRecorded++;
			if (glove != null)
			{
				PlayerGameFielding line = Field(glove.Id);
				if (outcome == PlateAppearanceOutcome.Strikeout || _rng.NextDouble() < 0.62)
				{
					line.Putouts++;
				}
				else
				{
					line.Assists++;
					Player first = FielderAt(defenseOrder, PlayerPosition.FirstBase) ?? glove;
					Field(first.Id).Putouts++;
				}
			}

			emitted.Add(Emit(LivePlayKind.PlateAppearance, DescribeOut(batter, outcome, glove, defenseOrder), 0, outcome));
			if (_outs >= 3)
			{
				EndHalf(emitted);
				return;
			}

			MaybeChangePitcher(defense, emitted, startOfInning: false);
			return;
		}

		Advance(batter, batterLine, defense, outcome, emitted, describe: true);
	}

	private void Advance(
		Player batter,
		PlayerGameBatting batterLine,
		PitchingStaff defense,
		PlateAppearanceOutcome outcome,
		List<LivePlay> emitted,
		bool describe)
	{
		BaserunningResult advance = BaseRunnerEngine.Apply(_bases, batter, outcome, _rng, _config);
		Score(advance.Scorers, batterLine, defense);
		if (describe)
		{
			emitted.Add(Emit(LivePlayKind.PlateAppearance, DescribeHit(batter, outcome, advance.Scorers), advance.Scorers.Count, outcome));
		}
		else if (advance.Scorers.Count > 0)
		{
			emitted.Add(Emit(LivePlayKind.PlateAppearance, RunsText(advance.Scorers), advance.Scorers.Count, outcome));
		}

		if (!_top && _inning >= _config.RegulationInnings && HomeScore > AwayScore)
		{
			_walkOff = true;
			EndHalf(emitted);
			return;
		}

		MaybeChangePitcher(defense, emitted, startOfInning: false);
	}

	private void Score(List<Player> scorers, PlayerGameBatting batterLine, PitchingStaff defense, bool creditRbi = true)
	{
		foreach (Player scorer in scorers)
		{
			_batting[scorer.Id].Runs++;
			if (creditRbi)
			{
				batterLine.RunsBattedIn++;
			}

			defense.Line.RunsAllowed++;
			if (!_inningError)
			{
				defense.Line.EarnedRuns++;
			}

			_inningRuns++;
			if (_top)
			{
				AwayScore++;
				_awayInningRuns[_inning - 1]++;
			}
			else
			{
				HomeScore++;
				_homeInningRuns[_inning - 1]++;
			}

			if (HomeScore > AwayScore)
			{
				_homeLeadPitcher = _top ? _awayStaff.Current.Id : _homeStaff.Current.Id;
				_awayLeadPitcher = _top ? _homeStaff.Current.Id : _awayStaff.Current.Id;
			}
			else if (AwayScore > HomeScore)
			{
				_awayLeadPitcher = _top ? _homeStaff.Current.Id : _awayStaff.Current.Id;
				_homeLeadPitcher = _top ? _awayStaff.Current.Id : _homeStaff.Current.Id;
			}
		}
	}

	private void EndHalf(List<LivePlay> emitted)
	{
		_halfOpen = false;
		_buntQueued = false;
		_walkQueued = false;
		if (_walkOff)
		{
			Seal(emitted);
			return;
		}

		if (_top && _inning >= _config.RegulationInnings && HomeScore > AwayScore)
		{
			Seal(emitted);
			return;
		}

		if (!_top)
		{
			if (_inning >= _config.RegulationInnings && HomeScore != AwayScore)
			{
				Seal(emitted);
				return;
			}

			_inning++;
		}

		_top = !_top;
	}

	private bool TryAutoSteal(List<LineupBatter> defenseOrder, PitchingStaff staff, List<LivePlay> emitted)
	{
		if (_bases.First == null || _bases.Second != null)
		{
			return false;
		}

		Player runner = _bases.First;
		Player? catcher = FielderAt(defenseOrder, PlayerPosition.Catcher);
		double attempt = _config.StealAttemptBase
			+ _config.StealRatingWeight * ((runner.Ratings.Speed - (catcher?.Ratings.ArmStrength ?? 50)) / 100.0);
		if (_rng.NextDouble() >= Clamp(attempt, 0.01, 0.22))
		{
			return false;
		}

		double success = 0.68 + 0.22 * ((runner.Ratings.Speed - (catcher?.Ratings.ArmStrength ?? 50)) / 100.0);
		if (_rng.NextDouble() < Clamp(success, 0.42, 0.92))
		{
			_bases.Second = runner;
			_bases.First = null;
			_batting[runner.Id].StolenBases++;
			_fieldingPlays.Add($"{runner.LastName} stole second");
			emitted.Add(Emit(LivePlayKind.StolenBase, $"{runner.LastName} steals second."));
			return false;
		}

		_bases.First = null;
		staff.Line.OutsRecorded++;
		if (catcher != null)
		{
			_fieldingPlays.Add($"{catcher.LastName} caught stealing, {runner.LastName} thrown out at second");
		}

		emitted.Add(Emit(LivePlayKind.CaughtStealing, $"{runner.LastName} is caught stealing second.", outsDelta: 1));
		return true;
	}

	private void IntentionalWalk(Player batter, PlayerGameBatting batterLine, PitchingStaff defense, List<LivePlay> emitted)
	{
		ApplyPaToStats(batterLine, defense.Line, PlateAppearanceOutcome.Walk);
		BaserunningResult advance = BaseRunnerEngine.Apply(_bases, batter, PlateAppearanceOutcome.Walk, _rng, _config);
		Score(advance.Scorers, batterLine, defense);
		emitted.Add(Emit(
			LivePlayKind.IntentionalWalk,
			$"{batter.LastName} is intentionally walked.{(advance.Scorers.Count > 0 ? " " + RunsText(advance.Scorers) : "")}",
			advance.Scorers.Count,
			PlateAppearanceOutcome.Walk));
		if (!_top && _inning >= _config.RegulationInnings && HomeScore > AwayScore)
		{
			_walkOff = true;
			EndHalf(emitted);
			return;
		}

		MaybeChangePitcher(defense, emitted, startOfInning: false);
	}

	private void Bunt(
		Player batter,
		PlayerGameBatting batterLine,
		List<LineupBatter> defenseOrder,
		PitchingStaff defense,
		List<LivePlay> emitted)
	{
		batterLine.PlateAppearances++;
		defense.Pitches += 2;
		double contact = batter.Ratings.Contact / 100.0;
		double speed = batter.Ratings.Speed / 100.0;
		double roll = _rng.NextDouble();
		double hitChance = Clamp(0.04 + 0.10 * speed, 0.03, 0.14);
		double sacChance = Clamp(0.50 + 0.38 * contact, 0.48, 0.90);

		if (roll < hitChance)
		{
			batterLine.AtBats++;
			batterLine.Hits++;
			defense.Line.HitsAllowed++;
			var scorers = AdvanceAllOne(batter);
			Score(scorers, batterLine, defense);
			emitted.Add(Emit(LivePlayKind.Bunt, $"{batter.LastName} beats out a bunt for a hit!{(scorers.Count > 0 ? " " + RunsText(scorers) : "")}", scorers.Count, PlateAppearanceOutcome.Single));
		}
		else if (roll < hitChance + sacChance)
		{
			var scorers = AdvanceAllOne(null);
			Score(scorers, batterLine, defense);
			_outs++;
			defense.Line.OutsRecorded++;
			Player? first = FielderAt(defenseOrder, PlayerPosition.FirstBase);
			if (first != null)
			{
				Field(first.Id).Putouts++;
			}

			emitted.Add(Emit(LivePlayKind.Bunt, $"{batter.LastName} lays down the sacrifice. Runners move up.{(scorers.Count > 0 ? " " + RunsText(scorers) : "")}", scorers.Count, PlateAppearanceOutcome.Out));
		}
		else
		{
			batterLine.AtBats++;
			_outs++;
			defense.Line.OutsRecorded++;
			string lead = LeadRunnerOut(batter);
			emitted.Add(Emit(LivePlayKind.Bunt, $"{batter.LastName}'s bunt is fielded cleanly — {lead}.", 0, PlateAppearanceOutcome.Out));
		}

		if (_outs >= 3)
		{
			EndHalf(emitted);
			return;
		}

		if (!_top && _inning >= _config.RegulationInnings && HomeScore > AwayScore)
		{
			_walkOff = true;
			EndHalf(emitted);
			return;
		}

		MaybeChangePitcher(defense, emitted, startOfInning: false);
	}

	private List<Player> AdvanceAllOne(Player? batterToFirst)
	{
		var scorers = new List<Player>();
		if (_bases.Third != null)
		{
			scorers.Add(_bases.Third);
		}

		_bases.Third = _bases.Second;
		_bases.Second = _bases.First;
		_bases.First = batterToFirst;
		return scorers;
	}

	private string LeadRunnerOut(Player batter)
	{
		if (_bases.Third != null && _bases.Second != null && _bases.First != null)
		{
			string name = _bases.Third.LastName;
			_bases.Third = _bases.Second;
			_bases.Second = _bases.First;
			_bases.First = batter;
			return $"{name} forced at home";
		}

		if (_bases.Second != null && _bases.First != null)
		{
			string name = _bases.Second.LastName;
			_bases.Second = _bases.First;
			_bases.First = batter;
			return $"{name} forced at third";
		}

		if (_bases.First != null)
		{
			string name = _bases.First.LastName;
			_bases.First = batter;
			return $"{name} forced at second";
		}

		return "batter retired at first";
	}

	private void MaybeChangePitcher(PitchingStaff staff, List<LivePlay> emitted, bool startOfInning)
	{
		bool defenseAuto = ReferenceEquals(staff, _homeStaff) ? AutoManageHome : AutoManageAway;
		if (!defenseAuto)
		{
			return;
		}

		int lead = _top ? HomeScore - AwayScore : AwayScore - HomeScore;
		if (staff.Bullpen.Count == 0 && staff.Closer == null)
		{
			return;
		}

		bool saveSpot = startOfInning
			&& _inning >= _config.RegulationInnings
			&& lead is >= 1 and <= 3
			&& staff.Closer != null
			&& staff.Current.Id != staff.Closer.Id
			&& staff.CloserUnused;
		bool tired = staff.Pitches >= staff.PitchLimit;
		bool blown = _inningRuns >= 4 && staff.Pitches >= 40;
		bool deep = _inning >= 7 && tired;
		if (!saveSpot && !tired && !blown && !deep)
		{
			return;
		}

		Player? next = null;
		if (saveSpot)
		{
			next = staff.Closer;
		}
		else if (staff.Bullpen.Count > 0)
		{
			next = staff.Bullpen[0];
			staff.Bullpen.RemoveAt(0);
		}
		else if (staff.CloserUnused)
		{
			next = staff.Closer;
		}

		if (next == null || next.Id == staff.Current.Id)
		{
			return;
		}

		string outgoing = staff.Current.LastName;
		staff.ChangeTo(next, lead, _config);
		emitted.Add(Emit(LivePlayKind.PitchingChange, $"Pitching change: {next.LastName} replaces {outgoing}."));
	}

	private void Seal(List<LivePlay> emitted)
	{
		if (_result != null)
		{
			return;
		}

		_halfOpen = false;
		if (HomeScore == AwayScore)
		{
			HomeScore++;
			EnsureInning(_homeInningRuns, _inningsPlayed);
			_homeInningRuns[^1]++;
			_homeLeadPitcher ??= _homeStaff.Current.Id;
			_awayLeadPitcher ??= _awayStaff.Current.Id;
		}

		bool homeWon = HomeScore > AwayScore;
		bool skippedLast = _walkOff || (!_top && _inningsPlayed >= _config.RegulationInnings && homeWon && _homeInningRuns.Count >= _inningsPlayed && _homeInningRuns[_inningsPlayed - 1] == 0);
		AssignDecisions(homeWon);

		_homeStaff.Seal();
		_awayStaff.Seal();
		List<PlayerGamePitching> pitching = [.. _homeStaff.Lines, .. _awayStaff.Lines];
		int homeHits = SumHits(_homeAppeared);
		int awayHits = SumHits(_awayAppeared);
		int homeErrors = SumErrors(_homeAppeared, _homePlan.StartingPitcher);
		int awayErrors = SumErrors(_awayAppeared, _awayPlan.StartingPitcher);
		Guid winningPitcher = homeWon ? _homeStaff.WinnerId() : _awayStaff.WinnerId();
		Guid losingPitcher = homeWon ? _awayStaff.LoserId() : _homeStaff.LoserId();
		Guid? savePitcher = homeWon ? _homeStaff.SaveId() : _awayStaff.SaveId();

		GameBoxScore box = new()
		{
			GameId = _game.Id,
			AwayInningRuns = [.. _awayInningRuns],
			HomeInningRuns = [.. _homeInningRuns],
			AwayHits = awayHits,
			HomeHits = homeHits,
			AwayErrors = awayErrors,
			HomeErrors = homeErrors,
			HomeSkippedLast = skippedLast,
			SavePitcherId = savePitcher,
			AwayBatting = BattingLines(_awayAppeared),
			HomeBatting = BattingLines(_homeAppeared),
			AwayPitching = PitchingLines(_awayPlan, _awayAppeared, _awayStaff.Lines),
			HomePitching = PitchingLines(_homePlan, _homeAppeared, _homeStaff.Lines),
			FieldingPlays = [.. _fieldingPlays],
		};

		_result = new GameSimulationResult
		{
			GameId = _game.Id,
			HomeTeamId = _game.HomeTeamId,
			AwayTeamId = _game.AwayTeamId,
			HomeScore = HomeScore,
			AwayScore = AwayScore,
			InningsPlayed = Math.Max(_config.RegulationInnings, _inningsPlayed),
			HomeHits = homeHits,
			AwayHits = awayHits,
			HomeWalks = Sum(_homeAppeared, l => l.Walks),
			AwayWalks = Sum(_awayAppeared, l => l.Walks),
			HomeStrikeouts = Sum(_homeAppeared, l => l.Strikeouts),
			AwayStrikeouts = Sum(_awayAppeared, l => l.Strikeouts),
			HomeHomeRuns = Sum(_homeAppeared, l => l.HomeRuns),
			AwayHomeRuns = Sum(_awayAppeared, l => l.HomeRuns),
			HomeErrors = homeErrors,
			AwayErrors = awayErrors,
			ExtraInnings = _inningsPlayed > _config.RegulationInnings,
			HomeSkippedLast = skippedLast,
			WinningPitcherId = winningPitcher,
			LosingPitcherId = losingPitcher,
			SavePitcherId = savePitcher,
			HomeInningRuns = [.. _homeInningRuns],
			AwayInningRuns = [.. _awayInningRuns],
			Batting = _batting.Values.ToList(),
			Pitching = pitching,
			Fielding = _fielding.Values.ToList(),
			FieldingPlays = _fieldingPlays,
			BoxScore = box,
		};

		emitted.Add(Emit(LivePlayKind.Final, $"Final: {AwayScore}–{HomeScore}."));
	}

	private void AssignDecisions(bool homeWon)
	{
		PitchingStaff winner = homeWon ? _homeStaff : _awayStaff;
		PitchingStaff loser = homeWon ? _awayStaff : _homeStaff;
		Guid winId = QualifyWinner(winner, homeWon ? _homeLeadPitcher : _awayLeadPitcher);
		Guid loseId = homeWon ? _awayLeadPitcher ?? loser.StarterId : _homeLeadPitcher ?? loser.StarterId;
		winner.MarkWin(winId);
		loser.MarkLoss(loseId);
		winner.TrySave(winId);
	}

	private Guid QualifyWinner(PitchingStaff staff, Guid? ofRecord)
	{
		Guid id = ofRecord ?? staff.StarterId;
		PlayerGamePitching? line = staff.Find(id);
		if (line != null && line.GamesStarted == 1 && line.OutsRecorded < _config.StarterWinOuts && staff.Lines.Count > 1)
		{
			foreach (PlayerGamePitching reliever in staff.Lines)
			{
				if (reliever.PlayerId != id && reliever.OutsRecorded > 0)
				{
					return reliever.PlayerId;
				}
			}
		}

		return id;
	}

	private LivePlay Emit(
		LivePlayKind kind,
		string text,
		int runs = 0,
		PlateAppearanceOutcome? outcome = null,
		int outsDelta = 0)
	{
		var play = new LivePlay(kind, _inning, _top, text, Math.Min(3, _outs + outsDelta), AwayScore, HomeScore, runs, outcome, !_top);
		_log.Add(play);
		return play;
	}

	// ── Play-by-play copy (deterministic: never touches the RNG) ──────

	private int Flavor(Player batter, int choices) =>
		(int)((uint)HashCode.Combine(batter.Id, _paCount) % (uint)choices);

	private string DescribeOut(Player batter, PlateAppearanceOutcome outcome, Player? glove, List<LineupBatter> defenseOrder)
	{
		if (outcome == PlateAppearanceOutcome.Strikeout)
		{
			return Flavor(batter, 3) switch
			{
				0 => $"{batter.LastName} strikes out looking.",
				1 => $"{batter.LastName} strikes out swinging.",
				_ => $"{batter.LastName} goes down on strikes.",
			};
		}

		if (glove == null)
		{
			return $"{batter.LastName} is retired.";
		}

		PlayerPosition position = PositionOf(glove, defenseOrder);
		string fielder = PositionLong(position);
		bool outfield = position is PlayerPosition.LeftField or PlayerPosition.CenterField or PlayerPosition.RightField;
		if (outfield)
		{
			return Flavor(batter, 2) == 0
				? $"{batter.LastName} flies out to {fielder}."
				: $"{batter.LastName} lines out to {fielder}.";
		}

		return Flavor(batter, 3) switch
		{
			0 => $"{batter.LastName} grounds out to {fielder}.",
			1 => $"{batter.LastName} pops up to {fielder}.",
			_ => $"{batter.LastName} grounds out, {fielder} to first.",
		};
	}

	private string DescribeHit(Player batter, PlateAppearanceOutcome outcome, List<Player> scorers)
	{
		string[] fields = ["left", "center", "right", "left-center", "right-center"];
		string field = fields[Flavor(batter, fields.Length)];
		string text = outcome switch
		{
			PlateAppearanceOutcome.Walk => $"{batter.LastName} draws a walk.",
			PlateAppearanceOutcome.Single => $"{batter.LastName} singles to {field}.",
			PlateAppearanceOutcome.Double => $"{batter.LastName} doubles into the {field} gap.",
			PlateAppearanceOutcome.Triple => $"{batter.LastName} triples to {field}!",
			PlateAppearanceOutcome.HomeRun => scorers.Count switch
			{
				4 => $"GRAND SLAM! {batter.LastName} clears the {field} field wall!",
				1 => $"{batter.LastName} homers to {field}!",
				_ => $"{batter.LastName} hits a {scorers.Count}-run homer to {field}!",
			},
			_ => $"{batter.LastName} reaches.",
		};

		if (outcome == PlateAppearanceOutcome.HomeRun || scorers.Count == 0)
		{
			return text;
		}

		return $"{text} {RunsText(scorers)}";
	}

	private static string RunsText(List<Player> scorers)
	{
		if (scorers.Count == 0)
		{
			return "";
		}

		var names = new List<string>(scorers.Count);
		foreach (Player scorer in scorers)
		{
			names.Add(scorer.LastName);
		}

		return scorers.Count == 1 ? $"{names[0]} scores." : $"{string.Join(" and ", names)} score.";
	}

	private static string Ordinal(int n) => n switch
	{
		1 => "1st",
		2 => "2nd",
		3 => "3rd",
		_ => $"{n}th",
	};

	private static PlayerPosition PositionOf(Player player, List<LineupBatter> order)
	{
		foreach (LineupBatter slot in order)
		{
			if (slot.Player.Id == player.Id)
			{
				return slot.FieldPosition;
			}
		}

		return player.PrimaryPosition;
	}

	private static string PositionName(Player player, List<LineupBatter> order) => PositionLong(PositionOf(player, order));

	private static string PositionLong(PlayerPosition position) => position switch
	{
		PlayerPosition.Catcher => "the catcher",
		PlayerPosition.FirstBase => "first",
		PlayerPosition.SecondBase => "second",
		PlayerPosition.ThirdBase => "third",
		PlayerPosition.Shortstop => "short",
		PlayerPosition.LeftField => "left",
		PlayerPosition.CenterField => "center",
		PlayerPosition.RightField => "right",
		_ => "the mound",
	};

	// ── Helpers (ported) ────────────────────────────────────────────────

	private static List<Player> BenchFrom(IReadOnlyList<Player>? bench, TeamGamePlan plan)
	{
		var used = new HashSet<Guid> { plan.StartingPitcher.Id };
		foreach (LineupBatter slot in plan.BattingOrder)
		{
			used.Add(slot.Player.Id);
		}

		foreach (Player arm in plan.Bullpen)
		{
			used.Add(arm.Id);
		}

		if (plan.Closer != null)
		{
			used.Add(plan.Closer.Id);
		}

		var list = new List<Player>();
		if (bench == null)
		{
			return list;
		}

		foreach (Player player in bench)
		{
			if (used.Add(player.Id) && !player.PrimaryPosition.IsPitcher())
			{
				list.Add(player);
			}
		}

		return list;
	}

	private double ErrorRate(int fielding) =>
		Clamp(_config.ErrorBaseRate - _config.ErrorFieldingWeight * ((fielding / 100.0) - 0.50), 0.008, 0.07);

	private static int PitchesFor(PlateAppearanceOutcome outcome) => outcome switch
	{
		PlateAppearanceOutcome.Strikeout => 5,
		PlateAppearanceOutcome.Walk => 5,
		PlateAppearanceOutcome.HomeRun => 2,
		PlateAppearanceOutcome.Out => 3,
		_ => 4,
	};

	private static void EnsureInning(List<int> innings, int inning)
	{
		while (innings.Count < inning)
		{
			innings.Add(0);
		}
	}

	private static void EnsureBatting(Dictionary<Guid, PlayerGameBatting> batting, IReadOnlyList<LineupBatter> order)
	{
		foreach (LineupBatter slot in order)
		{
			if (!batting.ContainsKey(slot.Player.Id))
			{
				batting[slot.Player.Id] = new PlayerGameBatting
				{
					PlayerId = slot.Player.Id,
					Position = PositionCode(slot.FieldPosition),
				};
			}
		}
	}

	private static void ApplyPaToStats(PlayerGameBatting batter, PlayerGamePitching pitcher, PlateAppearanceOutcome outcome)
	{
		batter.PlateAppearances++;
		switch (outcome)
		{
			case PlateAppearanceOutcome.Strikeout:
				batter.AtBats++;
				batter.Strikeouts++;
				pitcher.Strikeouts++;
				break;
			case PlateAppearanceOutcome.Walk:
				batter.Walks++;
				pitcher.Walks++;
				break;
			case PlateAppearanceOutcome.Single:
				batter.AtBats++;
				batter.Hits++;
				pitcher.HitsAllowed++;
				break;
			case PlateAppearanceOutcome.Double:
				batter.AtBats++;
				batter.Hits++;
				batter.Doubles++;
				pitcher.HitsAllowed++;
				break;
			case PlateAppearanceOutcome.Triple:
				batter.AtBats++;
				batter.Hits++;
				batter.Triples++;
				pitcher.HitsAllowed++;
				break;
			case PlateAppearanceOutcome.HomeRun:
				batter.AtBats++;
				batter.Hits++;
				batter.HomeRuns++;
				pitcher.HitsAllowed++;
				pitcher.HomeRunsAllowed++;
				break;
			default:
				batter.AtBats++;
				break;
		}
	}

	private PlayerGameFielding Field(Guid id)
	{
		if (!_fielding.TryGetValue(id, out PlayerGameFielding? line))
		{
			line = new PlayerGameFielding { PlayerId = id };
			_fielding[id] = line;
		}

		return line;
	}

	private static Player? FielderFor(List<LineupBatter> order, Player pitcher, PlateAppearanceOutcome outcome, ISeededRandom rng)
	{
		if (outcome == PlateAppearanceOutcome.Strikeout)
		{
			return FielderAt(order, PlayerPosition.Catcher) ?? pitcher;
		}

		var gloves = new List<LineupBatter>();
		foreach (LineupBatter slot in order)
		{
			if (slot.FieldPosition != PlayerPosition.StartingPitcher
				&& slot.FieldPosition != PlayerPosition.ReliefPitcher)
			{
				gloves.Add(slot);
			}
		}

		return gloves.Count == 0 ? pitcher : rng.Pick(gloves).Player;
	}

	private static Player? FielderAt(List<LineupBatter> order, PlayerPosition position)
	{
		foreach (LineupBatter slot in order)
		{
			if (slot.FieldPosition == position)
			{
				return slot.Player;
			}
		}

		return null;
	}

	private static string PositionCode(Player player, List<LineupBatter> order)
	{
		foreach (LineupBatter slot in order)
		{
			if (slot.Player.Id == player.Id)
			{
				return PositionCode(slot.FieldPosition);
			}
		}

		return player.PrimaryPosition.ToCode();
	}

	private static string PositionCode(PlayerPosition position) => position switch
	{
		PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher => "P",
		_ => position.ToCode(),
	};

	private int SumHits(List<LineupBatter> appeared) => Sum(appeared, l => l.Hits);

	private int SumErrors(List<LineupBatter> appeared, Player starter)
	{
		int total = 0;
		var seen = new HashSet<Guid>();
		foreach (LineupBatter slot in appeared)
		{
			if (seen.Add(slot.Player.Id) && _fielding.TryGetValue(slot.Player.Id, out PlayerGameFielding? line))
			{
				total += line.Errors;
			}
		}

		if (seen.Add(starter.Id) && _fielding.TryGetValue(starter.Id, out PlayerGameFielding? arm))
		{
			total += arm.Errors;
		}

		return total;
	}

	private int Sum(List<LineupBatter> appeared, Func<PlayerGameBatting, int> selector)
	{
		int total = 0;
		var seen = new HashSet<Guid>();
		foreach (LineupBatter slot in appeared)
		{
			if (seen.Add(slot.Player.Id) && _batting.TryGetValue(slot.Player.Id, out PlayerGameBatting? line))
			{
				total += selector(line);
			}
		}

		return total;
	}

	private List<BoxScoreBattingLine> BattingLines(List<LineupBatter> appeared)
	{
		var lines = new List<BoxScoreBattingLine>(appeared.Count);
		var seen = new HashSet<Guid>();
		foreach (LineupBatter slot in appeared)
		{
			if (!seen.Add(slot.Player.Id) || !_batting.TryGetValue(slot.Player.Id, out PlayerGameBatting? line))
			{
				continue;
			}

			lines.Add(new BoxScoreBattingLine
			{
				PlayerId = slot.Player.Id,
				Name = slot.Player.FullName,
				LastName = slot.Player.LastName.ToUpperInvariant(),
				Position = PositionCode(slot.FieldPosition),
				AtBats = line.AtBats,
				Runs = line.Runs,
				Hits = line.Hits,
				HomeRuns = line.HomeRuns,
				RunsBattedIn = line.RunsBattedIn,
			});
		}

		return lines;
	}

	private static List<BoxScorePitchingLine> PitchingLines(TeamGamePlan plan, List<LineupBatter> appeared, IReadOnlyList<PlayerGamePitching> pitching)
	{
		var names = new Dictionary<Guid, Player> { [plan.StartingPitcher.Id] = plan.StartingPitcher };
		if (plan.Closer != null)
		{
			names[plan.Closer.Id] = plan.Closer;
		}

		foreach (Player arm in plan.Bullpen)
		{
			names[arm.Id] = arm;
		}

		foreach (LineupBatter slot in appeared)
		{
			names.TryAdd(slot.Player.Id, slot.Player);
		}

		var lines = new List<BoxScorePitchingLine>(pitching.Count);
		foreach (PlayerGamePitching line in pitching)
		{
			if (line.OutsRecorded <= 0 && line.HitsAllowed <= 0 && line.Walks <= 0)
			{
				continue;
			}

			names.TryGetValue(line.PlayerId, out Player? player);
			lines.Add(new BoxScorePitchingLine
			{
				PlayerId = line.PlayerId,
				Name = player?.FullName ?? "Pitcher",
				LastName = player?.LastName.ToUpperInvariant() ?? "PITCHER",
				OutsRecorded = line.OutsRecorded,
				Hits = line.HitsAllowed,
				Walks = line.Walks,
				Strikeouts = line.Strikeouts,
			});
		}

		return lines;
	}

	private static double Clamp(double value, double min, double max) =>
		value < min ? min : value > max ? max : value;

	private sealed class PitchingStaff
	{
		public Player Current { get; private set; }
		public PlayerGamePitching Line { get; private set; }
		public List<PlayerGamePitching> Lines { get; } = [];
		public int Pitches { get; set; }
		public int PitchLimit { get; private set; }
		public Player? Closer { get; }
		public List<Player> Bullpen { get; }
		public Guid StarterId { get; }
		public int LeadWhenEntered { get; private set; }

		public bool CloserUnused => Closer != null && Current.Id != Closer.Id && !HasPitched(Closer.Id);

		public PitchingStaff(TeamGamePlan plan, BaseballSimulationConfig config, Dictionary<Guid, PlayerGameBatting> batting)
		{
			Current = plan.StartingPitcher;
			StarterId = plan.StartingPitcher.Id;
			Closer = plan.Closer != null && plan.Closer.Id != plan.StartingPitcher.Id ? plan.Closer : null;
			Bullpen = [.. plan.Bullpen];
			Line = NewLine(Current.Id, started: true);
			Lines.Add(Line);
			PitchLimit = LimitFor(Current, started: true, config);
			if (!batting.ContainsKey(Current.Id))
			{
				batting[Current.Id] = new PlayerGameBatting
				{
					PlayerId = Current.Id,
					Position = "P",
				};
			}
		}

		public void ChangeTo(Player next, int lead, BaseballSimulationConfig config)
		{
			Current = next;
			Line = NewLine(next.Id, started: false);
			Lines.Add(Line);
			Pitches = 0;
			PitchLimit = LimitFor(next, started: false, config);
			LeadWhenEntered = lead;
		}

		public void Seal()
		{
			for (int i = Lines.Count - 1; i >= 0; i--)
			{
				PlayerGamePitching line = Lines[i];
				if (line.OutsRecorded == 0 && line.HitsAllowed == 0 && line.Walks == 0 && line.GamesStarted == 0)
				{
					Lines.RemoveAt(i);
				}
			}
		}

		public void MarkWin(Guid id)
		{
			foreach (PlayerGamePitching line in Lines)
			{
				line.Win = line.PlayerId == id;
			}
		}

		public void MarkLoss(Guid id)
		{
			foreach (PlayerGamePitching line in Lines)
			{
				line.Loss = line.PlayerId == id;
			}
		}

		public void TrySave(Guid winnerId)
		{
			if (Lines.Count < 2)
			{
				return;
			}

			PlayerGamePitching last = Lines[^1];
			if (last.PlayerId == winnerId || last.OutsRecorded <= 0)
			{
				return;
			}

			if (LeadWhenEntered is >= 1 and <= 3 || (Closer != null && last.PlayerId == Closer.Id && LeadWhenEntered > 0))
			{
				last.Save = true;
			}
		}

		public Guid WinnerId()
		{
			foreach (PlayerGamePitching line in Lines)
			{
				if (line.Win)
				{
					return line.PlayerId;
				}
			}

			return StarterId;
		}

		public Guid LoserId()
		{
			foreach (PlayerGamePitching line in Lines)
			{
				if (line.Loss)
				{
					return line.PlayerId;
				}
			}

			return StarterId;
		}

		public Guid? SaveId()
		{
			foreach (PlayerGamePitching line in Lines)
			{
				if (line.Save)
				{
					return line.PlayerId;
				}
			}

			return null;
		}

		public PlayerGamePitching? Find(Guid id)
		{
			foreach (PlayerGamePitching line in Lines)
			{
				if (line.PlayerId == id)
				{
					return line;
				}
			}

			return null;
		}

		public bool HasPitched(Guid id)
		{
			foreach (PlayerGamePitching line in Lines)
			{
				if (line.PlayerId == id)
				{
					return true;
				}
			}

			return false;
		}

		private static PlayerGamePitching NewLine(Guid id, bool started) =>
			new()
			{
				PlayerId = id,
				GamesStarted = started ? 1 : 0,
			};

		private static int LimitFor(Player pitcher, bool started, BaseballSimulationConfig config)
		{
			double stamina = pitcher.Ratings.Stamina;
			return started
				? (int)Math.Round(config.StarterPitchBase + config.StarterPitchPerStamina * stamina)
				: (int)Math.Round(config.RelieverPitchBase + config.RelieverPitchPerStamina * stamina);
		}
	}
}
