using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests.Reference;

// Frozen copy of the single-pass simulator from before LiveGame existed.
// LiveGameParityTests use it to prove the stepwise engine reproduces it.
#pragma warning disable CA1822, CA1859
internal sealed class LegacyBaseballGameSimulator
{
	private readonly BaseballSimulationConfig _config;
	private readonly ILineupProvider _lineups;

	public LegacyBaseballGameSimulator(
		BaseballSimulationConfig? config = null,
		ILineupProvider? lineups = null)
	{
		_config = config ?? BaseballSimulationConfig.Default;
		_lineups = lineups ?? new AutomaticLineupProvider();
	}

	public GameSimulationResult Simulate(
		ScheduledGame game,
		IReadOnlyList<Player> homeRoster,
		IReadOnlyList<Player> awayRoster,
		ISeededRandom rng)
	{
		return Simulate(
			game,
			_lineups.Create(homeRoster),
			_lineups.Create(awayRoster),
			rng);
	}

	public GameSimulationResult Simulate(
		ScheduledGame game,
		TeamGamePlan home,
		TeamGamePlan away,
		ISeededRandom rng)
	{
		if (home.BattingOrder.Count == 0 || away.BattingOrder.Count == 0)
		{
			throw new InvalidOperationException("Cannot simulate a game with an empty batting order.");
		}

		var batting = new Dictionary<Guid, PlayerGameBatting>();
		EnsureBatting(batting, home.BattingOrder);
		EnsureBatting(batting, away.BattingOrder);
		var fielding = new Dictionary<Guid, PlayerGameFielding>();
		var plays = new List<string>();

		var homeStaff = new PitchingStaff(home, _config, batting);
		var awayStaff = new PitchingStaff(away, _config, batting);

		int homeScore = 0;
		int awayScore = 0;
		int inning = 1;
		bool top = true;
		int homeBatter = 0;
		int awayBatter = 0;
		bool walkOff = false;
		int inningsPlayed = 0;
		Guid? homeLeadPitcher = null;
		Guid? awayLeadPitcher = null;
		var homeInningRuns = new List<int>(_config.MaxInnings);
		var awayInningRuns = new List<int>(_config.MaxInnings);

		while (inning <= _config.MaxInnings)
		{
			inningsPlayed = inning;
			if (!top && inning >= _config.RegulationInnings && homeScore > awayScore)
			{
				break;
			}

			EnsureInning(top ? awayInningRuns : homeInningRuns, inning);
			var bases = new BasePaths();
			int outs = 0;
			int inningRuns = 0;
			bool inningError = false;
			IReadOnlyList<LineupBatter> order = top ? away.BattingOrder : home.BattingOrder;
			ref int batterIndex = ref (top ? ref awayBatter : ref homeBatter);
			PitchingStaff defense = top ? homeStaff : awayStaff;
			TeamGamePlan fielders = top ? home : away;
			MaybeChangePitcher(defense, inning, top ? homeScore - awayScore : awayScore - homeScore, inningRuns, startOfInning: true);

			while (outs < 3)
			{
				if (TrySteal(bases, batting, fielders, defense, rng, plays))
				{
					outs++;
					if (outs >= 3)
					{
						break;
					}
				}
				LineupBatter slot = order[batterIndex % order.Count];
				batterIndex++;
				Player batter = slot.Player;
				PlayerGameBatting batterLine = batting[batter.Id];
				double fatigue = Math.Min(_config.MaxFatigue, defense.Pitches * _config.FatiguePerPitch);
				PlateAppearanceOutcome outcome = PlateAppearanceResolver.Resolve(
					batter.Ratings,
					defense.Current.Ratings,
					rng,
					_config,
					fatigue);

				int pitches = PitchesFor(outcome);
				defense.Pitches += pitches;
				ApplyPaToStats(batterLine, defense.Line, outcome);

				if (outcome is PlateAppearanceOutcome.Strikeout or PlateAppearanceOutcome.Out)
				{
					Player? glove = FielderFor(fielders, outcome, rng);
					if (outcome == PlateAppearanceOutcome.Out
						&& glove != null
						&& rng.NextDouble() < ErrorRate(glove.Ratings.Fielding))
					{
						inningError = true;
						Field(fielding, glove.Id).Errors++;
						plays.Add($"E{PositionCode(glove, fielders)}: {glove.LastName}");
						outcome = PlateAppearanceOutcome.Single;
					}
					else
					{
						outs++;
						defense.Line.OutsRecorded++;
						if (glove != null)
						{
							PlayerGameFielding line = Field(fielding, glove.Id);
							if (outcome == PlateAppearanceOutcome.Strikeout || rng.NextDouble() < 0.62)
							{
								line.Putouts++;
							}
							else
							{
								line.Assists++;
								Player? first = FielderAt(fielders, PlayerPosition.FirstBase) ?? glove;
								Field(fielding, first.Id).Putouts++;
							}
						}

						if (outs >= 3)
						{
							break;
						}

						MaybeChangePitcher(defense, inning, top ? homeScore - awayScore : awayScore - homeScore, inningRuns, startOfInning: false);
						continue;
					}
				}

				BaserunningResult advance = BaseRunnerEngine.Apply(bases, batter, outcome, rng, _config);
				foreach (Player scorer in advance.Scorers)
				{
					batting[scorer.Id].Runs++;
					batterLine.RunsBattedIn++;
					defense.Line.RunsAllowed++;
					if (!inningError)
					{
						defense.Line.EarnedRuns++;
					}

					inningRuns++;
					if (top)
					{
						awayScore++;
						awayInningRuns[inning - 1]++;
					}
					else
					{
						homeScore++;
						homeInningRuns[inning - 1]++;
					}

					if (homeScore > awayScore)
					{
						homeLeadPitcher = top ? awayStaff.Current.Id : homeStaff.Current.Id;
						awayLeadPitcher = top ? homeStaff.Current.Id : awayStaff.Current.Id;
					}
					else if (awayScore > homeScore)
					{
						awayLeadPitcher = top ? homeStaff.Current.Id : awayStaff.Current.Id;
						homeLeadPitcher = top ? awayStaff.Current.Id : homeStaff.Current.Id;
					}
				}

				if (!top && inning >= _config.RegulationInnings && homeScore > awayScore)
				{
					walkOff = true;
					break;
				}

				MaybeChangePitcher(defense, inning, top ? homeScore - awayScore : awayScore - homeScore, inningRuns, startOfInning: false);
			}

			if (walkOff)
			{
				break;
			}

			if (top && inning >= _config.RegulationInnings && homeScore > awayScore)
			{
				break;
			}

			if (!top)
			{
				if (inning >= _config.RegulationInnings && homeScore != awayScore)
				{
					break;
				}

				inning++;
			}

			top = !top;
		}

		if (homeScore == awayScore)
		{
			homeScore++;
			EnsureInning(homeInningRuns, inningsPlayed);
			homeInningRuns[^1]++;
			homeLeadPitcher ??= homeStaff.Current.Id;
			awayLeadPitcher ??= awayStaff.Current.Id;
		}

		bool homeWon = homeScore > awayScore;
		bool skippedLast = walkOff || (!top && inningsPlayed >= _config.RegulationInnings && homeWon && homeInningRuns.Count >= inningsPlayed && homeInningRuns[inningsPlayed - 1] == 0);
		AssignDecisions(homeStaff, awayStaff, homeWon, homeLeadPitcher, awayLeadPitcher);

		homeStaff.Seal();
		awayStaff.Seal();
		List<PlayerGamePitching> pitching = [.. homeStaff.Lines, .. awayStaff.Lines];
		int homeHits = SumHits(batting, home.BattingOrder);
		int awayHits = SumHits(batting, away.BattingOrder);
		int homeErrors = SumErrors(fielding, home.BattingOrder, home.StartingPitcher);
		int awayErrors = SumErrors(fielding, away.BattingOrder, away.StartingPitcher);
		Guid winningPitcher = homeWon ? homeStaff.WinnerId() : awayStaff.WinnerId();
		Guid losingPitcher = homeWon ? awayStaff.LoserId() : homeStaff.LoserId();
		Guid? savePitcher = homeWon ? homeStaff.SaveId() : awayStaff.SaveId();

		GameBoxScore box = BuildBoxScore(
			game.Id,
			home,
			away,
			batting,
			homeStaff.Lines,
			awayStaff.Lines,
			homeInningRuns,
			awayInningRuns,
			homeHits,
			awayHits,
			homeErrors,
			awayErrors,
			skippedLast,
			savePitcher,
			plays);

		return new GameSimulationResult
		{
			GameId = game.Id,
			HomeTeamId = game.HomeTeamId,
			AwayTeamId = game.AwayTeamId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			InningsPlayed = Math.Max(_config.RegulationInnings, inningsPlayed),
			HomeHits = homeHits,
			AwayHits = awayHits,
			HomeWalks = Sum(batting, home.BattingOrder, l => l.Walks),
			AwayWalks = Sum(batting, away.BattingOrder, l => l.Walks),
			HomeStrikeouts = Sum(batting, home.BattingOrder, l => l.Strikeouts),
			AwayStrikeouts = Sum(batting, away.BattingOrder, l => l.Strikeouts),
			HomeHomeRuns = Sum(batting, home.BattingOrder, l => l.HomeRuns),
			AwayHomeRuns = Sum(batting, away.BattingOrder, l => l.HomeRuns),
			HomeErrors = homeErrors,
			AwayErrors = awayErrors,
			ExtraInnings = inningsPlayed > _config.RegulationInnings,
			HomeSkippedLast = skippedLast,
			WinningPitcherId = winningPitcher,
			LosingPitcherId = losingPitcher,
			SavePitcherId = savePitcher,
			HomeInningRuns = [.. homeInningRuns],
			AwayInningRuns = [.. awayInningRuns],
			Batting = batting.Values.ToList(),
			Pitching = pitching,
			Fielding = fielding.Values.ToList(),
			FieldingPlays = plays,
			BoxScore = box,
		};
	}

	private void MaybeChangePitcher(PitchingStaff staff, int inning, int lead, int inningRuns, bool startOfInning)
	{
		if (staff.Bullpen.Count == 0 && staff.Closer == null)
		{
			return;
		}

		bool saveSpot = startOfInning
			&& inning >= _config.RegulationInnings
			&& lead is >= 1 and <= 3
			&& staff.Closer != null
			&& staff.Current.Id != staff.Closer.Id
			&& staff.CloserUnused;
		bool tired = staff.Pitches >= staff.PitchLimit;
		bool blown = inningRuns >= 4 && staff.Pitches >= 40;
		bool deep = inning >= 7 && tired;
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

		staff.ChangeTo(next, lead, _config);
	}

	private bool TrySteal(
		BasePaths bases,
		Dictionary<Guid, PlayerGameBatting> batting,
		TeamGamePlan defense,
		PitchingStaff staff,
		ISeededRandom rng,
		List<string> plays)
	{
		if (bases.First == null || bases.Second != null)
		{
			return false;
		}

		Player runner = bases.First;
		Player? catcher = FielderAt(defense, PlayerPosition.Catcher);
		double attempt = _config.StealAttemptBase
			+ _config.StealRatingWeight * ((runner.Ratings.Speed - (catcher?.Ratings.ArmStrength ?? 50)) / 100.0);
		if (rng.NextDouble() >= Clamp(attempt, 0.01, 0.22))
		{
			return false;
		}

		double success = 0.68 + 0.22 * ((runner.Ratings.Speed - (catcher?.Ratings.ArmStrength ?? 50)) / 100.0);
		if (rng.NextDouble() < Clamp(success, 0.42, 0.92))
		{
			bases.Second = runner;
			bases.First = null;
			batting[runner.Id].StolenBases++;
			plays.Add($"{runner.LastName} stole second");
			return false;
		}

		bases.First = null;
		staff.Line.OutsRecorded++;
		if (catcher != null)
		{
			plays.Add($"{catcher.LastName} caught stealing, {runner.LastName} thrown out at second");
		}

		return true;
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

	private void AssignDecisions(
		PitchingStaff home,
		PitchingStaff away,
		bool homeWon,
		Guid? homeLead,
		Guid? awayLead)
	{
		PitchingStaff winner = homeWon ? home : away;
		PitchingStaff loser = homeWon ? away : home;
		Guid winId = QualifyWinner(winner, homeWon ? homeLead : awayLead);
		Guid loseId = homeWon ? awayLead ?? loser.StarterId : homeLead ?? loser.StarterId;
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

	private static void ApplyPaToStats(
		PlayerGameBatting batter,
		PlayerGamePitching pitcher,
		PlateAppearanceOutcome outcome)
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

	private static PlayerGameFielding Field(Dictionary<Guid, PlayerGameFielding> fielding, Guid id)
	{
		if (!fielding.TryGetValue(id, out PlayerGameFielding? line))
		{
			line = new PlayerGameFielding { PlayerId = id };
			fielding[id] = line;
		}

		return line;
	}

	private static Player? FielderFor(TeamGamePlan plan, PlateAppearanceOutcome outcome, ISeededRandom rng)
	{
		if (outcome == PlateAppearanceOutcome.Strikeout)
		{
			return FielderAt(plan, PlayerPosition.Catcher) ?? plan.StartingPitcher;
		}

		var gloves = new List<LineupBatter>();
		foreach (LineupBatter slot in plan.BattingOrder)
		{
			if (slot.FieldPosition != PlayerPosition.StartingPitcher
				&& slot.FieldPosition != PlayerPosition.ReliefPitcher)
			{
				gloves.Add(slot);
			}
		}

		return gloves.Count == 0 ? plan.StartingPitcher : rng.Pick(gloves).Player;
	}

	private static Player? FielderAt(TeamGamePlan plan, PlayerPosition position)
	{
		foreach (LineupBatter slot in plan.BattingOrder)
		{
			if (slot.FieldPosition == position)
			{
				return slot.Player;
			}
		}

		return null;
	}

	private static string PositionCode(Player player, TeamGamePlan plan)
	{
		foreach (LineupBatter slot in plan.BattingOrder)
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

	private static int SumHits(Dictionary<Guid, PlayerGameBatting> batting, IReadOnlyList<LineupBatter> order) =>
		Sum(batting, order, l => l.Hits);

	private static int SumErrors(
		Dictionary<Guid, PlayerGameFielding> fielding,
		IReadOnlyList<LineupBatter> order,
		Player starter)
	{
		int total = 0;
		var seen = new HashSet<Guid>();
		foreach (LineupBatter slot in order)
		{
			if (seen.Add(slot.Player.Id) && fielding.TryGetValue(slot.Player.Id, out PlayerGameFielding? line))
			{
				total += line.Errors;
			}
		}

		if (seen.Add(starter.Id) && fielding.TryGetValue(starter.Id, out PlayerGameFielding? arm))
		{
			total += arm.Errors;
		}

		return total;
	}

	private static int Sum(
		Dictionary<Guid, PlayerGameBatting> batting,
		IReadOnlyList<LineupBatter> order,
		Func<PlayerGameBatting, int> selector)
	{
		int total = 0;
		var seen = new HashSet<Guid>();
		foreach (LineupBatter slot in order)
		{
			if (seen.Add(slot.Player.Id) && batting.TryGetValue(slot.Player.Id, out PlayerGameBatting? line))
			{
				total += selector(line);
			}
		}

		return total;
	}

	private static GameBoxScore BuildBoxScore(
		Guid gameId,
		TeamGamePlan home,
		TeamGamePlan away,
		Dictionary<Guid, PlayerGameBatting> batting,
		IReadOnlyList<PlayerGamePitching> homePitch,
		IReadOnlyList<PlayerGamePitching> awayPitch,
		List<int> homeInnings,
		List<int> awayInnings,
		int homeHits,
		int awayHits,
		int homeErrors,
		int awayErrors,
		bool skippedLast,
		Guid? saveId,
		List<string> plays)
	{
		return new GameBoxScore
		{
			GameId = gameId,
			AwayInningRuns = [.. awayInnings],
			HomeInningRuns = [.. homeInnings],
			AwayHits = awayHits,
			HomeHits = homeHits,
			AwayErrors = awayErrors,
			HomeErrors = homeErrors,
			HomeSkippedLast = skippedLast,
			SavePitcherId = saveId,
			AwayBatting = BattingLines(away, batting),
			HomeBatting = BattingLines(home, batting),
			AwayPitching = PitchingLines(away, awayPitch),
			HomePitching = PitchingLines(home, homePitch),
			FieldingPlays = [.. plays],
		};
	}

	private static List<BoxScoreBattingLine> BattingLines(TeamGamePlan plan, Dictionary<Guid, PlayerGameBatting> batting)
	{
		var lines = new List<BoxScoreBattingLine>(plan.BattingOrder.Count);
		foreach (LineupBatter slot in plan.BattingOrder)
		{
			if (!batting.TryGetValue(slot.Player.Id, out PlayerGameBatting? line))
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

	private static List<BoxScorePitchingLine> PitchingLines(TeamGamePlan plan, IReadOnlyList<PlayerGamePitching> pitching)
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

		foreach (LineupBatter slot in plan.BattingOrder)
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

		public bool CloserUnused => Closer != null && Current.Id != Closer.Id && !Contains(Closer.Id);

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

		private bool Contains(Guid id)
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
#pragma warning restore CA1822, CA1859
