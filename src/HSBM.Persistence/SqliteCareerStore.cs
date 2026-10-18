using System.Globalization;
using System.Text.Json;
using HSBM.Application;
using HSBM.Domain;
using Microsoft.Data.Sqlite;

namespace HSBM.Persistence;

public sealed class SqliteCareerStore : ICareerStore
{
	private readonly SqliteConnection _connection;

	public SqliteCareerStore(string databasePath)
	{
		if (string.IsNullOrWhiteSpace(databasePath))
		{
			throw new ArgumentException("Database path is required.", nameof(databasePath));
		}

		DatabasePath = Path.GetFullPath(databasePath);
		string? directory = Path.GetDirectoryName(DatabasePath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		_connection = new SqliteConnection(new SqliteConnectionStringBuilder
		{
			DataSource = DatabasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared,
		}.ToString());
		_connection.Open();
		Initialize();
	}

	public string DatabasePath { get; }

	public bool HasCareer()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM career;";
		return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
	}

	public void ReplaceAll(CareerState state)
	{
		using SqliteTransaction tx = _connection.BeginTransaction();
		try
		{
			Execute(tx, "DELETE FROM program_coaches;");
			Execute(tx, "DELETE FROM player_transactions;");
			Execute(tx, "DELETE FROM news_stories;");
			Execute(tx, "DELETE FROM open_play_lines;");
			Execute(tx, "DELETE FROM open_play;");
			Execute(tx, "DELETE FROM recruiting_targets;");
			Execute(tx, "DELETE FROM recruiting_interest;");
			Execute(tx, "DELETE FROM recruiting_offers;");
			Execute(tx, "DELETE FROM player_agreements;");
			Execute(tx, "DELETE FROM game_box_scores;");
			Execute(tx, "DELETE FROM postseason_state;");
			Execute(tx, "DELETE FROM season_awards;");
			Execute(tx, "DELETE FROM historical_seasons;");
			Execute(tx, "DELETE FROM season_profile_commits;");
			Execute(tx, "DELETE FROM team_lineups;");
			Execute(tx, "DELETE FROM team_rotations;");
			Execute(tx, "DELETE FROM team_training;");
			Execute(tx, "DELETE FROM fielding_stats;");
			Execute(tx, "DELETE FROM batting_stats;");
			Execute(tx, "DELETE FROM pitching_stats;");
			Execute(tx, "DELETE FROM games;");
			Execute(tx, "DELETE FROM players;");
			Execute(tx, "DELETE FROM team_season_records;");
			Execute(tx, "DELETE FROM program_records;");
			Execute(tx, "DELETE FROM program_hall_of_fame;");
			Execute(tx, "DELETE FROM program_season_history;");
			Execute(tx, "DELETE FROM teams;");
			Execute(tx, "DELETE FROM organizations;");
			Execute(tx, "DELETE FROM districts;");
			Execute(tx, "DELETE FROM coaching_job_offers;");
			Execute(tx, "DELETE FROM coach_seasons;");
			Execute(tx, "DELETE FROM coach_stints;");
			Execute(tx, "DELETE FROM coach;");
			Execute(tx, "DELETE FROM career;");

			foreach (DistrictIdentity district in state.Districts)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = "INSERT INTO districts(id, name, abbreviation, region) VALUES ($id, $name, $abbr, $region);";
				command.Parameters.AddWithValue("$id", district.Id);
				command.Parameters.AddWithValue("$name", district.Name);
				command.Parameters.AddWithValue("$abbr", district.Abbreviation);
				command.Parameters.AddWithValue("$region", district.Region);
				command.ExecuteNonQuery();
			}

			foreach (OrganizationState org in state.Organizations)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = """
					INSERT INTO organizations(
						id, name, short_name, district_id, roster_cap, talent_tier, competition_level,
						reputation, facilities_grade, baseball_resources, baseball_budget, last_season_wins, last_season_losses,
						district_finish, district_titles, regional_titles, state_titles,
						minimum_expectation, target_expectation, has_opening, vacancy_reason)
					VALUES (
						$id, $name, $short, $district, $cap, $tier, $level,
						$rep, $facilities, $resources, $budget, $wins, $losses,
						$finish, $districtTitles, $regionalTitles, $stateTitles,
						$minExpect, $targetExpect, $opening, $vacancy);
					""";
				command.Parameters.AddWithValue("$id", org.Identity.Id);
				command.Parameters.AddWithValue("$name", org.Identity.Name);
				command.Parameters.AddWithValue("$short", org.Identity.ShortName);
				command.Parameters.AddWithValue("$district", org.DistrictId);
				command.Parameters.AddWithValue("$cap", org.Identity.RosterCap);
				command.Parameters.AddWithValue("$tier", (int)org.TalentTier);
				command.Parameters.AddWithValue("$level", (int)org.CompetitionLevel);
				command.Parameters.AddWithValue("$rep", org.Profile.Reputation);
				command.Parameters.AddWithValue("$facilities", (int)org.Profile.Facilities);
				command.Parameters.AddWithValue("$resources", (int)org.Profile.BaseballResources);
				command.Parameters.AddWithValue("$budget", org.Profile.BaseballBudget);
				command.Parameters.AddWithValue("$wins", org.Profile.LastSeasonWins);
				command.Parameters.AddWithValue("$losses", org.Profile.LastSeasonLosses);
				command.Parameters.AddWithValue("$finish", org.Profile.DistrictFinish);
				command.Parameters.AddWithValue("$districtTitles", org.Profile.DistrictTitles);
				command.Parameters.AddWithValue("$regionalTitles", org.Profile.RegionalTitles);
				command.Parameters.AddWithValue("$stateTitles", org.Profile.StateTitles);
				command.Parameters.AddWithValue("$minExpect", (int)org.Profile.MinimumExpectation);
				command.Parameters.AddWithValue("$targetExpect", (int)org.Profile.TargetExpectation);
				command.Parameters.AddWithValue("$opening", org.Profile.HasOpening ? 1 : 0);
				command.Parameters.AddWithValue("$vacancy", (int)org.Profile.VacancyReason);
				command.ExecuteNonQuery();

				foreach (ProgramSeasonResult season in org.SeasonHistory)
				{
					InsertProgramSeason(tx, season);
				}

				foreach (ProgramRecordHolder holder in org.RecordBook)
				{
					InsertProgramRecord(tx, holder);
				}

				foreach (ProgramHallOfFameInductee inductee in org.HallOfFame)
				{
					InsertHallOfFame(tx, inductee);
				}
			}

			foreach (TeamIdentity team in state.Teams)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = "INSERT INTO teams(id, organization_id, level, roster_cap) VALUES ($id, $org, $level, $cap);";
				command.Parameters.AddWithValue("$id", team.Id);
				command.Parameters.AddWithValue("$org", team.OrganizationId);
				command.Parameters.AddWithValue("$level", (int)team.Level);
				command.Parameters.AddWithValue("$cap", team.RosterCap);
				command.ExecuteNonQuery();
			}

			using (SqliteCommand command = _connection.CreateCommand())
			{
				command.Transaction = tx;
				command.CommandText = """
					INSERT INTO career(id, name, created_at, simulation_date, current_season_year, user_organization_id, random_seed, competition_level, save_version)
					VALUES ($id, $name, $created, $date, $year, $org, $seed, $level, $version);
					""";
				command.Parameters.AddWithValue("$id", state.Career.Id.ToString());
				command.Parameters.AddWithValue("$name", state.Career.Name);
				command.Parameters.AddWithValue("$created", state.Career.CreatedAt.ToString("O"));
				command.Parameters.AddWithValue("$date", state.Career.CurrentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				command.Parameters.AddWithValue("$year", state.Career.CurrentSeasonYear);
				command.Parameters.AddWithValue("$org", state.Career.UserOrganizationId);
				command.Parameters.AddWithValue("$seed", state.Career.RandomSeed);
				command.Parameters.AddWithValue("$level", (int)state.Career.CompetitionLevel);
				command.Parameters.AddWithValue("$version", state.Career.SaveVersion);
				command.ExecuteNonQuery();
			}

			InsertCoach(tx, state.Career);

			foreach (ProgramCoach coach in state.Coaches)
			{
				InsertProgramCoach(tx, coach);
			}

			foreach (Player player in state.Players)
			{
				InsertPlayer(tx, player);
			}

			foreach (ScheduledGame game in state.Games)
			{
				InsertGame(tx, game);
			}

			foreach (GameBoxScore sheet in state.BoxScores)
			{
				InsertBoxScore(tx, sheet);
			}

			InsertPostseasonArchive(tx, state);

			foreach (TeamSeasonRecord record in state.TeamRecords)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = """
					INSERT INTO team_season_records(team_id, season_year, wins, losses, district_wins, district_losses, runs_for, runs_against)
					VALUES ($team, $year, $w, $l, $dw, $dl, $rf, $ra);
					""";
				command.Parameters.AddWithValue("$team", record.TeamId);
				command.Parameters.AddWithValue("$year", record.SeasonYear);
				command.Parameters.AddWithValue("$w", record.Wins);
				command.Parameters.AddWithValue("$l", record.Losses);
				command.Parameters.AddWithValue("$dw", record.DistrictWins);
				command.Parameters.AddWithValue("$dl", record.DistrictLosses);
				command.Parameters.AddWithValue("$rf", record.RunsFor);
				command.Parameters.AddWithValue("$ra", record.RunsAgainst);
				command.ExecuteNonQuery();
			}

			foreach (BattingSeasonStats stats in state.BattingStats)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = """
					INSERT INTO batting_stats(player_id, season_year, team_id, games, plate_appearances, at_bats, runs, hits, doubles, triples, home_runs, rbi, walks, strikeouts, stolen_bases)
					VALUES ($player, $year, $team, $g, $pa, $ab, $r, $h, $d, $t, $hr, $rbi, $bb, $so, $sb);
					""";
				command.Parameters.AddWithValue("$player", stats.PlayerId.ToString());
				command.Parameters.AddWithValue("$year", stats.SeasonYear);
				command.Parameters.AddWithValue("$team", stats.TeamId);
				command.Parameters.AddWithValue("$g", stats.Games);
				command.Parameters.AddWithValue("$pa", stats.PlateAppearances);
				command.Parameters.AddWithValue("$ab", stats.AtBats);
				command.Parameters.AddWithValue("$r", stats.Runs);
				command.Parameters.AddWithValue("$h", stats.Hits);
				command.Parameters.AddWithValue("$d", stats.Doubles);
				command.Parameters.AddWithValue("$t", stats.Triples);
				command.Parameters.AddWithValue("$hr", stats.HomeRuns);
				command.Parameters.AddWithValue("$rbi", stats.RunsBattedIn);
				command.Parameters.AddWithValue("$bb", stats.Walks);
				command.Parameters.AddWithValue("$so", stats.Strikeouts);
				command.Parameters.AddWithValue("$sb", stats.StolenBases);
				command.ExecuteNonQuery();
			}

			foreach (PitchingSeasonStats stats in state.PitchingStats)
			{
				using SqliteCommand command = _connection.CreateCommand();
				command.Transaction = tx;
				command.CommandText = """
					INSERT INTO pitching_stats(player_id, season_year, team_id, games, games_started, wins, losses, outs_recorded, hits_allowed, runs_allowed, earned_runs, walks, strikeouts, home_runs_allowed, saves)
					VALUES ($player, $year, $team, $g, $gs, $w, $l, $outs, $h, $r, $er, $bb, $so, $hr, $sv);
					""";
				command.Parameters.AddWithValue("$player", stats.PlayerId.ToString());
				command.Parameters.AddWithValue("$year", stats.SeasonYear);
				command.Parameters.AddWithValue("$team", stats.TeamId);
				command.Parameters.AddWithValue("$g", stats.Games);
				command.Parameters.AddWithValue("$gs", stats.GamesStarted);
				command.Parameters.AddWithValue("$w", stats.Wins);
				command.Parameters.AddWithValue("$l", stats.Losses);
				command.Parameters.AddWithValue("$outs", stats.OutsRecorded);
				command.Parameters.AddWithValue("$h", stats.HitsAllowed);
				command.Parameters.AddWithValue("$r", stats.RunsAllowed);
				command.Parameters.AddWithValue("$er", stats.EarnedRuns);
				command.Parameters.AddWithValue("$bb", stats.Walks);
				command.Parameters.AddWithValue("$so", stats.Strikeouts);
				command.Parameters.AddWithValue("$hr", stats.HomeRunsAllowed);
				command.Parameters.AddWithValue("$sv", stats.Saves);
				command.ExecuteNonQuery();
			}

			foreach (FieldingSeasonStats stats in state.FieldingStats)
			{
				InsertFielding(tx, stats);
			}

			foreach (TeamLineup lineup in state.Lineups)
			{
				InsertLineup(tx, lineup);
			}

			foreach (TeamRotation rotation in state.Rotations)
			{
				InsertRotation(tx, rotation);
			}

			foreach (TeamTraining plan in state.Training)
			{
				InsertTraining(tx, plan);
			}

			foreach (PlayerAgreement agreement in state.Agreements)
			{
				InsertAgreement(tx, agreement);
			}

			foreach (RecruitingOffer offer in state.Offers)
			{
				InsertOffer(tx, offer);
			}

			foreach (RecruitingInterest interest in state.RecruitingInterest)
			{
				InsertInterest(tx, interest);
			}

			foreach (RecruitingTarget target in state.RecruitingTargets)
			{
				InsertTarget(tx, target);
			}

			foreach (DistrictOpenPlay session in state.OpenPlay)
			{
				InsertOpenPlay(tx, session);
			}

			foreach (NewsStory story in state.News)
			{
				InsertNews(tx, story);
			}

			foreach (PlayerTransaction entry in state.Transactions)
			{
				InsertTransaction(tx, entry);
			}

			tx.Commit();
		}
		catch
		{
			tx.Rollback();
			throw;
		}
	}

	public CareerState Load()
	{
		if (!HasCareer())
		{
			throw new InvalidOperationException($"No career exists in '{DatabasePath}'.");
		}

		var state = new CareerState
		{
			Career = ReadCareer(),
			Districts = ReadDistricts(),
			Organizations = ReadOrganizations(),
			Teams = ReadTeams(),
			Players = ReadPlayers(),
			Games = ReadGames(),
			TeamRecords = ReadRecords(),
			BattingStats = ReadBatting(),
			PitchingStats = ReadPitching(),
			FieldingStats = ReadFielding(),
			Lineups = ReadLineups(),
			Rotations = ReadRotations(),
			Training = ReadTraining(),
			BoxScores = ReadBoxScores(),
			Agreements = ReadAgreements(),
			Offers = ReadOffers(),
			RecruitingInterest = ReadInterest(),
			RecruitingTargets = ReadTargets(),
			OpenPlay = ReadOpenPlay(),
			News = ReadNews(),
			Transactions = ReadTransactions(),
		};
		AttachProgramArchives(state);
		AttachProgramCoaches(state);
		AttachPostseasonArchive(state);
		state.SyncClockFromCareer();
		RepairResume(state);
		return state;
	}

	private static void RepairResume(CareerState state)
	{
		OrganizationState? org = state.Organizations.FirstOrDefault(item =>
			item.Id == state.Career.UserOrganizationId);
		string school = org?.DisplayName ?? state.Career.Contract.SchoolName;
		state.Career.EnsureCurrentStint(school);
	}

	public void Dispose() => _connection.Dispose();

	private void Initialize()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = CareerSchema.Sql;
		command.ExecuteNonQuery();
		MigrateOrganizations();
		EnsureProgramArchiveTables();
		EnsureCoachTables();
		EnsurePostseasonArchiveTables();
		using SqliteCommand meta = _connection.CreateCommand();
		meta.CommandText = "INSERT OR REPLACE INTO meta(key, value) VALUES ('schema_version', $v);";
		meta.Parameters.AddWithValue("$v", CareerSchema.Version.ToString(CultureInfo.InvariantCulture));
		meta.ExecuteNonQuery();
	}

	private void MigrateOrganizations()
	{
		AddColumnIfMissing("organizations", "reputation", "INTEGER NOT NULL DEFAULT 50");
		AddColumnIfMissing("organizations", "facilities_grade", "INTEGER NOT NULL DEFAULT 5");
		AddColumnIfMissing("organizations", "baseball_budget", "INTEGER NOT NULL DEFAULT 40000");
		AddColumnIfMissing("organizations", "last_season_wins", "INTEGER NOT NULL DEFAULT 13");
		AddColumnIfMissing("organizations", "last_season_losses", "INTEGER NOT NULL DEFAULT 13");
		AddColumnIfMissing("organizations", "district_finish", "INTEGER NOT NULL DEFAULT 4");
		AddColumnIfMissing("organizations", "district_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "regional_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "state_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "minimum_expectation", "INTEGER NOT NULL DEFAULT 1");
		AddColumnIfMissing("organizations", "target_expectation", "INTEGER NOT NULL DEFAULT 2");
		AddColumnIfMissing("organizations", "has_opening", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "vacancy_reason", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "competition_level", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("organizations", "baseball_resources", "INTEGER NOT NULL DEFAULT 5");
		AddColumnIfMissing("career", "competition_level", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "potential", "INTEGER NOT NULL DEFAULT 50");
		AddColumnIfMissing("players", "recruiting_archetype", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "appearance_json", "TEXT NOT NULL DEFAULT '{}'");
		AddColumnIfMissing("players", "height_inches", "INTEGER NOT NULL DEFAULT 70");
		AddColumnIfMissing("players", "weight_lbs", "INTEGER NOT NULL DEFAULT 170");
		AddColumnIfMissing("players", "strength", "INTEGER NOT NULL DEFAULT 50");
		AddColumnIfMissing("players", "personality", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "injury_kind", "TEXT");
		AddColumnIfMissing("players", "injury_start", "TEXT");
		AddColumnIfMissing("players", "injury_available_on", "TEXT");
		AddColumnIfMissing("players", "injury_day_to_day", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "alumni", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "graduated_year", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "college_id", "TEXT NOT NULL DEFAULT ''");
		AddColumnIfMissing("players", "college_school", "TEXT NOT NULL DEFAULT ''");
		AddColumnIfMissing("players", "college_path", "TEXT NOT NULL DEFAULT ''");
		AddColumnIfMissing("players", "development_progress", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("players", "season_start_rating", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach_stints", "wins", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach_stints", "losses", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach_stints", "district_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach_stints", "regional_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach_stints", "state_titles", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach", "contract_term", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach", "contract_years_remaining", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("coach", "annual_salary", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("batting_stats", "stolen_bases", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("pitching_stats", "saves", "INTEGER NOT NULL DEFAULT 0");
		AddColumnIfMissing("games", "save_pitcher_id", "TEXT");
		EnsureRecruitingTables();
	}

	private void AddColumnIfMissing(string table, string column, string definition)
	{
		if (ColumnExists(table, column))
		{
			return;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
		command.ExecuteNonQuery();
	}

	private bool ColumnExists(string table, string column)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = $"PRAGMA table_info({table});";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private void InsertPlayer(SqliteTransaction tx, Player player)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO players(
				id, first_name, last_name, generated_season_year, bats, throws, primary_position, secondary_position,
				grade, age, organization_id, team_id, team_level, jersey, contact, power, plate_discipline, speed,
				fielding, arm_strength, stuff, control, movement, stamina, potential, recruiting_archetype,
				appearance_json, height_inches, weight_lbs, strength, personality,
				injury_kind, injury_start, injury_available_on, injury_day_to_day, alumni,
				graduated_year, college_id, college_school, college_path, development_progress, season_start_rating)
			VALUES (
				$id, $first, $last, $year, $bats, $throws, $pos, $sec, $grade, $age, $org, $team, $level, $jersey,
				$contact, $power, $disc, $speed, $field, $arm, $stuff, $control, $move, $stam, $pot, $arch,
				$appearance, $height, $weight, $strength, $personality,
				$injuryKind, $injuryStart, $injuryUntil, $injuryDtd, $alumni,
				$gradYear, $collegeId, $collegeSchool, $collegePath, $devProgress, $seasonStart);
			""";
		command.Parameters.AddWithValue("$id", player.Id.ToString());
		command.Parameters.AddWithValue("$first", player.FirstName);
		command.Parameters.AddWithValue("$last", player.LastName);
		command.Parameters.AddWithValue("$year", player.GeneratedSeasonYear);
		command.Parameters.AddWithValue("$bats", (int)player.Bats);
		command.Parameters.AddWithValue("$throws", (int)player.Throws);
		command.Parameters.AddWithValue("$pos", (int)player.PrimaryPosition);
		command.Parameters.AddWithValue("$sec", player.SecondaryPosition.HasValue ? (object)(int)player.SecondaryPosition.Value : DBNull.Value);
		command.Parameters.AddWithValue("$grade", (int)player.Grade);
		command.Parameters.AddWithValue("$age", player.Age);
		command.Parameters.AddWithValue("$org", player.OrganizationId);
		command.Parameters.AddWithValue("$team", player.TeamId);
		command.Parameters.AddWithValue("$level", (int)player.TeamLevel);
		command.Parameters.AddWithValue("$jersey", player.JerseyNumber);
		command.Parameters.AddWithValue("$contact", player.Ratings.Contact);
		command.Parameters.AddWithValue("$power", player.Ratings.Power);
		command.Parameters.AddWithValue("$disc", player.Ratings.PlateDiscipline);
		command.Parameters.AddWithValue("$speed", player.Ratings.Speed);
		command.Parameters.AddWithValue("$field", player.Ratings.Fielding);
		command.Parameters.AddWithValue("$arm", player.Ratings.ArmStrength);
		command.Parameters.AddWithValue("$stuff", player.Ratings.Stuff);
		command.Parameters.AddWithValue("$control", player.Ratings.Control);
		command.Parameters.AddWithValue("$move", player.Ratings.Movement);
		command.Parameters.AddWithValue("$stam", player.Ratings.Stamina);
		command.Parameters.AddWithValue("$pot", player.Potential > 0 ? player.Potential : player.Overall);
		command.Parameters.AddWithValue("$arch", (int)player.Priorities.Archetype);
		command.Parameters.AddWithValue("$appearance", JsonSerializer.Serialize(player.Appearance ?? new PersonAppearance()));
		command.Parameters.AddWithValue("$height", player.Physical.HeightInches > 0 ? player.Physical.HeightInches : 70);
		command.Parameters.AddWithValue("$weight", player.Physical.WeightLbs > 0 ? player.Physical.WeightLbs : 170);
		command.Parameters.AddWithValue("$strength", player.Physical.Strength > 0 ? player.Physical.Strength : 50);
		command.Parameters.AddWithValue("$personality", (int)player.Personality);
		command.Parameters.AddWithValue("$injuryKind", (object?)player.Injury?.Kind ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$injuryStart",
			player.Injury == null
				? DBNull.Value
				: player.Injury.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue(
			"$injuryUntil",
			player.Injury == null
				? DBNull.Value
				: player.Injury.AvailableOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$injuryDtd", player.Injury is { DayToDay: true } ? 1 : 0);
		command.Parameters.AddWithValue("$alumni", player.Alumni ? 1 : 0);
		command.Parameters.AddWithValue("$gradYear", player.GraduatedYear);
		command.Parameters.AddWithValue("$collegeId", player.CollegeId ?? "");
		command.Parameters.AddWithValue("$collegeSchool", player.CollegeSchool ?? "");
		command.Parameters.AddWithValue("$collegePath", player.CollegePath ?? "");
		command.Parameters.AddWithValue("$devProgress", player.DevelopmentProgress);
		command.Parameters.AddWithValue("$seasonStart", player.SeasonStartRating);
		command.ExecuteNonQuery();
	}

	private void InsertGame(SqliteTransaction tx, ScheduledGame game)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO games(id, season_year, game_date, home_team_id, away_team_id, team_level, game_type, status,
				home_score, away_score, innings_played, winning_team_id, losing_team_id, winning_pitcher_id, losing_pitcher_id, save_pitcher_id)
			VALUES ($id, $year, $date, $home, $away, $level, $type, $status, $hs, $as, $inn, $win, $lose, $wp, $lp, $sp);
			""";
		command.Parameters.AddWithValue("$id", game.Id.ToString());
		command.Parameters.AddWithValue("$year", game.SeasonYear);
		command.Parameters.AddWithValue("$date", game.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$home", game.HomeTeamId);
		command.Parameters.AddWithValue("$away", game.AwayTeamId);
		command.Parameters.AddWithValue("$level", (int)game.TeamLevel);
		command.Parameters.AddWithValue("$type", (int)game.GameType);
		command.Parameters.AddWithValue("$status", (int)game.Status);
		command.Parameters.AddWithValue("$hs", (object?)game.HomeScore ?? DBNull.Value);
		command.Parameters.AddWithValue("$as", (object?)game.AwayScore ?? DBNull.Value);
		command.Parameters.AddWithValue("$inn", (object?)game.InningsPlayed ?? DBNull.Value);
		command.Parameters.AddWithValue("$win", (object?)game.WinningTeamId ?? DBNull.Value);
		command.Parameters.AddWithValue("$lose", (object?)game.LosingTeamId ?? DBNull.Value);
		command.Parameters.AddWithValue("$wp", game.WinningPitcherId.HasValue ? game.WinningPitcherId.Value.ToString() : DBNull.Value);
		command.Parameters.AddWithValue("$lp", game.LosingPitcherId.HasValue ? game.LosingPitcherId.Value.ToString() : DBNull.Value);
		command.Parameters.AddWithValue("$sp", game.SavePitcherId.HasValue ? game.SavePitcherId.Value.ToString() : DBNull.Value);
		command.ExecuteNonQuery();
	}

	private void InsertFielding(SqliteTransaction tx, FieldingSeasonStats stats)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO fielding_stats(player_id, season_year, team_id, games, putouts, assists, errors)
			VALUES ($player, $year, $team, $g, $po, $a, $e);
			""";
		command.Parameters.AddWithValue("$player", stats.PlayerId.ToString());
		command.Parameters.AddWithValue("$year", stats.SeasonYear);
		command.Parameters.AddWithValue("$team", stats.TeamId);
		command.Parameters.AddWithValue("$g", stats.Games);
		command.Parameters.AddWithValue("$po", stats.Putouts);
		command.Parameters.AddWithValue("$a", stats.Assists);
		command.Parameters.AddWithValue("$e", stats.Errors);
		command.ExecuteNonQuery();
	}

	private void InsertLineup(SqliteTransaction tx, TeamLineup lineup)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = "INSERT INTO team_lineups(team_id, slots_json) VALUES ($team, $slots);";
		command.Parameters.AddWithValue("$team", lineup.TeamId);
		command.Parameters.AddWithValue("$slots", JsonSerializer.Serialize(lineup.Slots));
		command.ExecuteNonQuery();
	}

	private void InsertRotation(SqliteTransaction tx, TeamRotation rotation)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO team_rotations(team_id, starters_json, closer_id, setup_id, middle_id, long_relief_id)
			VALUES ($team, $starters, $closer, $setup, $middle, $long);
			""";
		command.Parameters.AddWithValue("$team", rotation.TeamId);
		command.Parameters.AddWithValue("$starters", JsonSerializer.Serialize(rotation.Starters));
		command.Parameters.AddWithValue("$closer", GuidOrDb(rotation.Closer));
		command.Parameters.AddWithValue("$setup", GuidOrDb(rotation.Setup));
		command.Parameters.AddWithValue("$middle", GuidOrDb(rotation.Middle));
		command.Parameters.AddWithValue("$long", GuidOrDb(rotation.LongRelief));
		command.ExecuteNonQuery();
	}

	private void InsertBoxScore(SqliteTransaction tx, GameBoxScore sheet)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = "INSERT INTO game_box_scores(game_id, payload_json) VALUES ($id, $json);";
		command.Parameters.AddWithValue("$id", sheet.GameId.ToString());
		command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(sheet));
		command.ExecuteNonQuery();
	}

	private void InsertPostseasonArchive(SqliteTransaction tx, CareerState state)
	{
		if (state.World.Postseason != null)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = "INSERT INTO postseason_state(season_year, payload_json) VALUES ($year, $json);";
			command.Parameters.AddWithValue("$year", state.World.Postseason.SeasonYear);
			command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(state.World.Postseason));
			command.ExecuteNonQuery();
		}

		foreach (SeasonAwards slate in state.World.Awards)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = "INSERT INTO season_awards(season_year, scope, payload_json) VALUES ($year, $scope, $json);";
			command.Parameters.AddWithValue("$year", slate.SeasonYear);
			command.Parameters.AddWithValue("$scope", (int)slate.Scope);
			command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(slate));
			command.ExecuteNonQuery();
		}

		foreach (HistoricalSeason season in state.World.HistoricalSeasons)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = "INSERT INTO historical_seasons(season_year, payload_json) VALUES ($year, $json);";
			command.Parameters.AddWithValue("$year", season.SeasonYear);
			command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(season));
			command.ExecuteNonQuery();
		}

		foreach (string key in state.World.SeasonProfileCommits)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = "INSERT INTO season_profile_commits(commit_key) VALUES ($key);";
			command.Parameters.AddWithValue("$key", key);
			command.ExecuteNonQuery();
		}
	}

	private static object GuidOrDb(Guid? id) => id.HasValue ? id.Value.ToString() : DBNull.Value;

	private Career ReadCareer()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT id, name, created_at, simulation_date, current_season_year, user_organization_id, random_seed, save_version, competition_level FROM career LIMIT 1;";
		using SqliteDataReader reader = command.ExecuteReader();
		if (!reader.Read())
		{
			throw new InvalidOperationException("Career row is missing.");
		}

		Career career = new()
		{
			Id = Guid.Parse(reader.GetString(0)),
			Name = reader.GetString(1),
			CreatedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
			CurrentDate = DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
			CurrentSeasonYear = reader.GetInt32(4),
			UserOrganizationId = reader.GetString(5),
			RandomSeed = reader.GetInt32(6),
			SaveVersion = reader.GetInt32(7),
			CompetitionLevel = reader.FieldCount > 8 && !reader.IsDBNull(8)
				? (CompetitionLevel)reader.GetInt32(8)
				: CompetitionLevel.HighSchool,
		};
		BindCoach(career);
		return career;
	}

	private List<DistrictIdentity> ReadDistricts()
	{
		var list = new List<DistrictIdentity>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT id, name, abbreviation, region FROM districts ORDER BY name;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new DistrictIdentity(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
		}

		return list;
	}

	private List<OrganizationState> ReadOrganizations()
	{
		var list = new List<OrganizationState>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, name, short_name, district_id, roster_cap, talent_tier, competition_level,
				reputation, facilities_grade, baseball_resources, baseball_budget, last_season_wins, last_season_losses,
				district_finish, district_titles, regional_titles, state_titles,
				minimum_expectation, target_expectation, has_opening, vacancy_reason
			FROM organizations ORDER BY name;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new OrganizationState
			{
				Identity = new OrganizationIdentity(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(4)),
				DistrictId = reader.GetString(3),
				TalentTier = (ProgramTalentTier)reader.GetInt32(5),
				CompetitionLevel = reader.FieldCount > 6 && !reader.IsDBNull(6)
					? (CompetitionLevel)reader.GetInt32(6)
					: CompetitionLevel.HighSchool,
				Profile = new ProgramProfile
				{
					Reputation = reader.GetInt32(7),
					Facilities = (LetterGrade)reader.GetInt32(8),
					BaseballResources = (LetterGrade)reader.GetInt32(9),
					BaseballBudget = reader.GetInt32(10),
					LastSeasonWins = reader.GetInt32(11),
					LastSeasonLosses = reader.GetInt32(12),
					DistrictFinish = reader.GetInt32(13),
					DistrictTitles = reader.GetInt32(14),
					RegionalTitles = reader.GetInt32(15),
					StateTitles = reader.GetInt32(16),
					MinimumExpectation = (BoardExpectation)reader.GetInt32(17),
					TargetExpectation = (BoardExpectation)reader.GetInt32(18),
					HasOpening = reader.GetInt32(19) != 0,
					VacancyReason = (VacancyReason)reader.GetInt32(20),
				},
			});
		}

		return list;
	}

	private List<TeamIdentity> ReadTeams()
	{
		var list = new List<TeamIdentity>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT id, organization_id, level, roster_cap FROM teams ORDER BY id;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new TeamIdentity(reader.GetString(0), reader.GetString(1), (TeamLevel)reader.GetInt32(2), reader.GetInt32(3)));
		}

		return list;
	}

	private List<Player> ReadPlayers()
	{
		var list = new List<Player>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, first_name, last_name, generated_season_year, bats, throws, primary_position, secondary_position,
				grade, age, organization_id, team_id, team_level, jersey, contact, power, plate_discipline, speed,
				fielding, arm_strength, stuff, control, movement, stamina, potential, recruiting_archetype,
				appearance_json, height_inches, weight_lbs, strength, personality,
				injury_kind, injury_start, injury_available_on, injury_day_to_day, alumni,
				graduated_year, college_id, college_school, college_path, development_progress, season_start_rating
			FROM players ORDER BY id;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new Player
			{
				Id = Guid.Parse(reader.GetString(0)),
				FirstName = reader.GetString(1),
				LastName = reader.GetString(2),
				GeneratedSeasonYear = reader.GetInt32(3),
				Bats = (BattingSide)reader.GetInt32(4),
				Throws = (ThrowingHand)reader.GetInt32(5),
				PrimaryPosition = (PlayerPosition)reader.GetInt32(6),
				SecondaryPosition = reader.IsDBNull(7) ? null : (PlayerPosition)reader.GetInt32(7),
				Grade = (PlayerGrade)reader.GetInt32(8),
				Age = reader.GetInt32(9),
				OrganizationId = reader.GetString(10),
				TeamId = reader.GetString(11),
				TeamLevel = (TeamLevel)reader.GetInt32(12),
				JerseyNumber = reader.GetInt32(13),
				Ratings = PlayerRatings.Clamp(
					reader.GetInt32(14),
					reader.GetInt32(15),
					reader.GetInt32(16),
					reader.GetInt32(17),
					reader.GetInt32(18),
					reader.GetInt32(19),
					reader.GetInt32(20),
					reader.GetInt32(21),
					reader.GetInt32(22),
					reader.GetInt32(23)),
				Potential = reader.FieldCount > 24 && !reader.IsDBNull(24) ? reader.GetInt32(24) : 0,
				Priorities = RecruitingPriorities.For(
					reader.FieldCount > 25 && !reader.IsDBNull(25)
						? (RecruitingArchetype)reader.GetInt32(25)
						: RecruitingArchetype.Balanced),
				Appearance = reader.FieldCount > 26 && !reader.IsDBNull(26)
					? ReadAppearance(reader.GetString(26))
					: new PersonAppearance(),
				Physical = new PlayerPhysical(
					reader.FieldCount > 27 && !reader.IsDBNull(27) ? reader.GetInt32(27) : 70,
					reader.FieldCount > 28 && !reader.IsDBNull(28) ? reader.GetInt32(28) : 170,
					reader.FieldCount > 29 && !reader.IsDBNull(29) ? reader.GetInt32(29) : 50,
					BodyBuild.Average),
				Personality = reader.FieldCount > 30 && !reader.IsDBNull(30)
					? (PlayerTemperament)reader.GetInt32(30)
					: PlayerTemperament.Composed,
				Injury = ReadInjury(reader),
				Alumni = reader.FieldCount > 35 && !reader.IsDBNull(35) && reader.GetInt32(35) != 0,
				GraduatedYear = reader.FieldCount > 36 && !reader.IsDBNull(36) ? reader.GetInt32(36) : 0,
				CollegeId = reader.FieldCount > 37 && !reader.IsDBNull(37) ? reader.GetString(37) : "",
				CollegeSchool = reader.FieldCount > 38 && !reader.IsDBNull(38) ? reader.GetString(38) : "",
				CollegePath = reader.FieldCount > 39 && !reader.IsDBNull(39) ? reader.GetString(39) : "",
				DevelopmentProgress = reader.FieldCount > 40 && !reader.IsDBNull(40) ? reader.GetInt32(40) : 0,
				SeasonStartRating = reader.FieldCount > 41 && !reader.IsDBNull(41) ? reader.GetInt32(41) : 0,
			});
			Player restored = list[^1];
			if (restored.Physical.HeightInches > 0)
			{
				restored.Physical = restored.Physical with
				{
					Build = restored.Appearance?.BodyBuild ?? BodyBuild.Average,
				};
			}
			if (list[^1].Potential <= 0)
			{
				list[^1].Potential = list[^1].Overall;
			}
		}

		return list;
	}

	private List<ScheduledGame> ReadGames()
	{
		var list = new List<ScheduledGame>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, season_year, game_date, home_team_id, away_team_id, team_level, game_type, status,
				home_score, away_score, innings_played, winning_team_id, losing_team_id, winning_pitcher_id, losing_pitcher_id, save_pitcher_id
			FROM games ORDER BY game_date, team_level, home_team_id;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new ScheduledGame
			{
				Id = Guid.Parse(reader.GetString(0)),
				SeasonYear = reader.GetInt32(1),
				Date = DateOnly.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
				HomeTeamId = reader.GetString(3),
				AwayTeamId = reader.GetString(4),
				TeamLevel = (TeamLevel)reader.GetInt32(5),
				GameType = (GameType)reader.GetInt32(6),
				Status = (GameStatus)reader.GetInt32(7),
				HomeScore = reader.IsDBNull(8) ? null : reader.GetInt32(8),
				AwayScore = reader.IsDBNull(9) ? null : reader.GetInt32(9),
				InningsPlayed = reader.IsDBNull(10) ? null : reader.GetInt32(10),
				WinningTeamId = reader.IsDBNull(11) ? null : reader.GetString(11),
				LosingTeamId = reader.IsDBNull(12) ? null : reader.GetString(12),
				WinningPitcherId = reader.IsDBNull(13) ? null : Guid.Parse(reader.GetString(13)),
				LosingPitcherId = reader.IsDBNull(14) ? null : Guid.Parse(reader.GetString(14)),
				SavePitcherId = reader.FieldCount > 15 && !reader.IsDBNull(15) ? Guid.Parse(reader.GetString(15)) : null,
			});
		}

		return list;
	}

	private List<TeamSeasonRecord> ReadRecords()
	{
		var list = new List<TeamSeasonRecord>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT team_id, season_year, wins, losses, district_wins, district_losses, runs_for, runs_against FROM team_season_records;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new TeamSeasonRecord
			{
				TeamId = reader.GetString(0),
				SeasonYear = reader.GetInt32(1),
				Wins = reader.GetInt32(2),
				Losses = reader.GetInt32(3),
				DistrictWins = reader.GetInt32(4),
				DistrictLosses = reader.GetInt32(5),
				RunsFor = reader.GetInt32(6),
				RunsAgainst = reader.GetInt32(7),
			});
		}

		return list;
	}

	private List<BattingSeasonStats> ReadBatting()
	{
		var list = new List<BattingSeasonStats>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT player_id, season_year, team_id, games, plate_appearances, at_bats, runs, hits, doubles, triples, home_runs, rbi, walks, strikeouts, stolen_bases
			FROM batting_stats;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new BattingSeasonStats
			{
				PlayerId = Guid.Parse(reader.GetString(0)),
				SeasonYear = reader.GetInt32(1),
				TeamId = reader.GetString(2),
				Games = reader.GetInt32(3),
				PlateAppearances = reader.GetInt32(4),
				AtBats = reader.GetInt32(5),
				Runs = reader.GetInt32(6),
				Hits = reader.GetInt32(7),
				Doubles = reader.GetInt32(8),
				Triples = reader.GetInt32(9),
				HomeRuns = reader.GetInt32(10),
				RunsBattedIn = reader.GetInt32(11),
				Walks = reader.GetInt32(12),
				Strikeouts = reader.GetInt32(13),
				StolenBases = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetInt32(14) : 0,
			});
		}

		return list;
	}

	private List<PitchingSeasonStats> ReadPitching()
	{
		var list = new List<PitchingSeasonStats>();
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT player_id, season_year, team_id, games, games_started, wins, losses, outs_recorded, hits_allowed, runs_allowed, earned_runs, walks, strikeouts, home_runs_allowed, saves
			FROM pitching_stats;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new PitchingSeasonStats
			{
				PlayerId = Guid.Parse(reader.GetString(0)),
				SeasonYear = reader.GetInt32(1),
				TeamId = reader.GetString(2),
				Games = reader.GetInt32(3),
				GamesStarted = reader.GetInt32(4),
				Wins = reader.GetInt32(5),
				Losses = reader.GetInt32(6),
				OutsRecorded = reader.GetInt32(7),
				HitsAllowed = reader.GetInt32(8),
				RunsAllowed = reader.GetInt32(9),
				EarnedRuns = reader.GetInt32(10),
				Walks = reader.GetInt32(11),
				Strikeouts = reader.GetInt32(12),
				HomeRunsAllowed = reader.GetInt32(13),
				Saves = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetInt32(14) : 0,
			});
		}

		return list;
	}

	private List<FieldingSeasonStats> ReadFielding()
	{
		var list = new List<FieldingSeasonStats>();
		if (!TableExists("fielding_stats"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT player_id, season_year, team_id, games, putouts, assists, errors FROM fielding_stats;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new FieldingSeasonStats
			{
				PlayerId = Guid.Parse(reader.GetString(0)),
				SeasonYear = reader.GetInt32(1),
				TeamId = reader.GetString(2),
				Games = reader.GetInt32(3),
				Putouts = reader.GetInt32(4),
				Assists = reader.GetInt32(5),
				Errors = reader.GetInt32(6),
			});
		}

		return list;
	}

	private List<TeamLineup> ReadLineups()
	{
		var list = new List<TeamLineup>();
		if (!TableExists("team_lineups"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT team_id, slots_json FROM team_lineups;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new TeamLineup
			{
				TeamId = reader.GetString(0),
				Slots = JsonSerializer.Deserialize<List<TeamLineupSlot>>(reader.GetString(1)) ?? [],
			});
		}

		return list;
	}

	private List<TeamTraining> ReadTraining()
	{
		var list = new List<TeamTraining>();
		if (!TableExists("team_training"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT team_id, focus, intensity FROM team_training;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new TeamTraining
			{
				TeamId = reader.GetString(0),
				Focus = (TrainingFocus)reader.GetInt32(1),
				Intensity = (TrainingIntensity)reader.GetInt32(2),
			});
		}

		return list;
	}

	private void InsertTraining(SqliteTransaction tx, TeamTraining plan)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = "INSERT OR REPLACE INTO team_training(team_id, focus, intensity) VALUES ($team, $focus, $intensity);";
		command.Parameters.AddWithValue("$team", plan.TeamId);
		command.Parameters.AddWithValue("$focus", (int)plan.Focus);
		command.Parameters.AddWithValue("$intensity", (int)plan.Intensity);
		command.ExecuteNonQuery();
	}

	private List<TeamRotation> ReadRotations()
	{
		var list = new List<TeamRotation>();
		if (!TableExists("team_rotations"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT team_id, starters_json, closer_id, setup_id, middle_id, long_relief_id FROM team_rotations;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new TeamRotation
			{
				TeamId = reader.GetString(0),
				Starters = JsonSerializer.Deserialize<List<Guid?>>(reader.GetString(1)) ?? [null, null, null, null, null],
				Closer = ReadGuid(reader, 2),
				Setup = ReadGuid(reader, 3),
				Middle = ReadGuid(reader, 4),
				LongRelief = ReadGuid(reader, 5),
			});
		}

		return list;
	}

	private List<GameBoxScore> ReadBoxScores()
	{
		var list = new List<GameBoxScore>();
		if (!TableExists("game_box_scores"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT payload_json FROM game_box_scores;";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			GameBoxScore? sheet = JsonSerializer.Deserialize<GameBoxScore>(reader.GetString(0));
			if (sheet != null)
			{
				list.Add(sheet);
			}
		}

		return list;
	}

	private static Guid? ReadGuid(SqliteDataReader reader, int ordinal) =>
		reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));

	private List<PlayerAgreement> ReadAgreements()
	{
		var list = new List<PlayerAgreement>();
		if (!TableExists("player_agreements"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, player_id, organization_id, start_season_year, term_years, years_remaining, annual_sponsorship
			FROM player_agreements;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new PlayerAgreement
			{
				Id = Guid.Parse(reader.GetString(0)),
				PlayerId = Guid.Parse(reader.GetString(1)),
				OrganizationId = reader.GetString(2),
				StartSeasonYear = reader.GetInt32(3),
				TermYears = reader.GetInt32(4),
				YearsRemaining = reader.GetInt32(5),
				AnnualSponsorship = reader.GetInt32(6),
			});
		}

		return list;
	}

	private List<RecruitingOffer> ReadOffers()
	{
		var list = new List<RecruitingOffer>();
		if (!TableExists("recruiting_offers"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, player_id, organization_id, season_year, annual_sponsorship, term_years, offered_on, status
			FROM recruiting_offers;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new RecruitingOffer
			{
				Id = Guid.Parse(reader.GetString(0)),
				PlayerId = Guid.Parse(reader.GetString(1)),
				OrganizationId = reader.GetString(2),
				SeasonYear = reader.GetInt32(3),
				AnnualSponsorship = reader.GetInt32(4),
				TermYears = reader.GetInt32(5),
				OfferedOn = DateOnly.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
				Status = (RecruitingOfferStatus)reader.GetInt32(7),
			});
		}

		return list;
	}

	private List<DistrictOpenPlay> ReadOpenPlay()
	{
		var list = new List<DistrictOpenPlay>();
		if (!TableExists("open_play"))
		{
			return list;
		}

		using SqliteCommand sessions = _connection.CreateCommand();
		sessions.CommandText = "SELECT district_id, season_year FROM open_play;";
		using SqliteDataReader sessionReader = sessions.ExecuteReader();
		var headers = new List<(string DistrictId, int SeasonYear)>();
		while (sessionReader.Read())
		{
			headers.Add((sessionReader.GetString(0), sessionReader.GetInt32(1)));
		}

		foreach ((string districtId, int seasonYear) in headers)
		{
			var playerIds = new List<Guid>();
			var lines = new List<OpenPlayLine>();
			using SqliteCommand linesCmd = _connection.CreateCommand();
			linesCmd.CommandText = """
				SELECT player_id, hit_games, at_bats, runs, hits, doubles, triples, home_runs, rbi, walks, strikeouts, stolen_bases,
					pitch_games, games_started, wins, losses, saves, outs, hits_allowed, runs_allowed, earned_runs, walks_allowed,
					strikeouts_pitch, home_runs_allowed, grade
				FROM open_play_lines WHERE district_id = $d AND season_year = $y;
				""";
			linesCmd.Parameters.AddWithValue("$d", districtId);
			linesCmd.Parameters.AddWithValue("$y", seasonYear);
			using SqliteDataReader reader = linesCmd.ExecuteReader();
			while (reader.Read())
			{
				Guid playerId = Guid.Parse(reader.GetString(0));
				playerIds.Add(playerId);
				TryoutPitchingLine? pitching = reader.IsDBNull(12)
					? null
					: new TryoutPitchingLine
					{
						Games = reader.GetInt32(12),
						GamesStarted = reader.GetInt32(13),
						Wins = reader.GetInt32(14),
						Losses = reader.GetInt32(15),
						Saves = reader.GetInt32(16),
						Outs = reader.GetInt32(17),
						Hits = reader.GetInt32(18),
						Runs = reader.GetInt32(19),
						EarnedRuns = reader.GetInt32(20),
						Walks = reader.GetInt32(21),
						Strikeouts = reader.GetInt32(22),
						HomeRuns = reader.GetInt32(23),
					};
				lines.Add(new OpenPlayLine
				{
					PlayerId = playerId,
					Hitting = new TryoutHittingLine
					{
						Games = reader.GetInt32(1),
						AtBats = reader.GetInt32(2),
						Runs = reader.GetInt32(3),
						Hits = reader.GetInt32(4),
						Doubles = reader.GetInt32(5),
						Triples = reader.GetInt32(6),
						HomeRuns = reader.GetInt32(7),
						Rbi = reader.GetInt32(8),
						Walks = reader.GetInt32(9),
						Strikeouts = reader.GetInt32(10),
						StolenBases = reader.GetInt32(11),
					},
					Pitching = pitching,
					Grade = (LetterGrade)reader.GetInt32(24),
				});
			}

			list.Add(new DistrictOpenPlay(districtId, seasonYear, SeasonCalendar.OpenPlay(seasonYear), playerIds, lines));
		}

		return list;
	}

	private void InsertAgreement(SqliteTransaction tx, PlayerAgreement agreement)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO player_agreements(id, player_id, organization_id, start_season_year, term_years, years_remaining, annual_sponsorship)
			VALUES ($id, $player, $org, $start, $term, $left, $annual);
			""";
		command.Parameters.AddWithValue("$id", agreement.Id.ToString());
		command.Parameters.AddWithValue("$player", agreement.PlayerId.ToString());
		command.Parameters.AddWithValue("$org", agreement.OrganizationId);
		command.Parameters.AddWithValue("$start", agreement.StartSeasonYear);
		command.Parameters.AddWithValue("$term", agreement.TermYears);
		command.Parameters.AddWithValue("$left", agreement.YearsRemaining);
		command.Parameters.AddWithValue("$annual", agreement.AnnualSponsorship);
		command.ExecuteNonQuery();
	}

	private void InsertNews(SqliteTransaction tx, NewsStory story)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO news_stories(
				id, story_date, tag, headline, body, pull, dedup_key, is_read,
				subject_organization_id, subject_player_name)
			VALUES (
				$id, $date, $tag, $headline, $body, $pull, $key, $read,
				$org, $player);
			""";
		command.Parameters.AddWithValue("$id", story.Id.ToString());
		command.Parameters.AddWithValue("$date", story.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$tag", story.Tag);
		command.Parameters.AddWithValue("$headline", story.Headline);
		command.Parameters.AddWithValue("$body", story.Body);
		command.Parameters.AddWithValue("$pull", story.Pull);
		command.Parameters.AddWithValue("$key", story.DedupKey);
		command.Parameters.AddWithValue("$read", story.Read ? 1 : 0);
		command.Parameters.AddWithValue("$org", (object?)story.SubjectOrganizationId ?? DBNull.Value);
		command.Parameters.AddWithValue("$player", (object?)story.SubjectPlayerName ?? DBNull.Value);
		command.ExecuteNonQuery();
	}

	private List<NewsStory> ReadNews()
	{
		var list = new List<NewsStory>();
		if (!TableExists("news_stories"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, story_date, tag, headline, body, pull, dedup_key, is_read,
				subject_organization_id, subject_player_name
			FROM news_stories
			ORDER BY story_date DESC, id DESC;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new NewsStory
			{
				Id = Guid.Parse(reader.GetString(0)),
				Date = DateOnly.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
				Tag = reader.GetString(2),
				Headline = reader.GetString(3),
				Body = reader.GetString(4),
				Pull = reader.GetString(5),
				DedupKey = reader.GetString(6),
				Read = reader.GetInt32(7) != 0,
				SubjectOrganizationId = reader.IsDBNull(8) ? null : reader.GetString(8),
				SubjectPlayerName = reader.IsDBNull(9) ? null : reader.GetString(9),
			});
		}

		return list;
	}

	private void InsertTransaction(SqliteTransaction tx, PlayerTransaction entry)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO player_transactions(
				kind, txn_date, date_label, headline, detail, organization_id, player_id, dedup_key)
			VALUES ($kind, $date, $label, $headline, $detail, $org, $player, $key);
			""";
		command.Parameters.AddWithValue("$kind", (int)entry.Kind);
		command.Parameters.AddWithValue(
			"$date",
			entry.Date == default
				? ""
				: entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$label", entry.DateLabel);
		command.Parameters.AddWithValue("$headline", entry.Headline);
		command.Parameters.AddWithValue("$detail", entry.Detail);
		command.Parameters.AddWithValue("$org", entry.OrganizationId ?? "");
		command.Parameters.AddWithValue(
			"$player",
			entry.PlayerId == Guid.Empty ? "" : entry.PlayerId.ToString());
		command.Parameters.AddWithValue(
			"$key",
			string.IsNullOrWhiteSpace(entry.DedupKey)
				? $"{entry.Kind}:{entry.Headline}:{entry.DateLabel}"
				: entry.DedupKey);
		command.ExecuteNonQuery();
	}

	private List<PlayerTransaction> ReadTransactions()
	{
		var list = new List<PlayerTransaction>();
		if (!TableExists("player_transactions"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT kind, txn_date, date_label, headline, detail, organization_id, player_id, dedup_key
			FROM player_transactions
			ORDER BY id ASC;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			string dateText = reader.GetString(1);
			DateOnly date = DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
				? parsed
				: default;
			string playerText = reader.GetString(6);
			Guid playerId = Guid.TryParse(playerText, out Guid parsedPlayer) ? parsedPlayer : Guid.Empty;
			list.Add(new PlayerTransaction(
				(PlayerTransactionKind)reader.GetInt32(0),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				date,
				reader.GetString(5),
				playerId,
				reader.GetString(7)));
		}

		return list;
	}

	private void InsertOffer(SqliteTransaction tx, RecruitingOffer offer)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO recruiting_offers(id, player_id, organization_id, season_year, annual_sponsorship, term_years, offered_on, status)
			VALUES ($id, $player, $org, $year, $annual, $term, $date, $status);
			""";
		command.Parameters.AddWithValue("$id", offer.Id.ToString());
		command.Parameters.AddWithValue("$player", offer.PlayerId.ToString());
		command.Parameters.AddWithValue("$org", offer.OrganizationId);
		command.Parameters.AddWithValue("$year", offer.SeasonYear);
		command.Parameters.AddWithValue("$annual", offer.AnnualSponsorship);
		command.Parameters.AddWithValue("$term", offer.TermYears);
		command.Parameters.AddWithValue("$date", offer.OfferedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$status", (int)offer.Status);
		command.ExecuteNonQuery();
	}

	private void InsertInterest(SqliteTransaction tx, RecruitingInterest interest)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO recruiting_interest(player_id, organization_id, season_year, noted_on, heat)
			VALUES ($player, $org, $year, $date, $heat);
			""";
		command.Parameters.AddWithValue("$player", interest.PlayerId.ToString());
		command.Parameters.AddWithValue("$org", interest.OrganizationId);
		command.Parameters.AddWithValue("$year", interest.SeasonYear);
		command.Parameters.AddWithValue("$date", interest.NotedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$heat", interest.Heat);
		command.ExecuteNonQuery();
	}

	private void InsertTarget(SqliteTransaction tx, RecruitingTarget target)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO recruiting_targets(player_id, team_id, player_name, season_year)
			VALUES ($player, $team, $name, $year);
			""";
		command.Parameters.AddWithValue(
			"$player",
			target.PlayerId == Guid.Empty ? "" : target.PlayerId.ToString());
		command.Parameters.AddWithValue("$team", target.TeamId);
		command.Parameters.AddWithValue("$name", target.PlayerName);
		command.Parameters.AddWithValue("$year", target.SeasonYear);
		command.ExecuteNonQuery();
	}

	private List<RecruitingInterest> ReadInterest()
	{
		var list = new List<RecruitingInterest>();
		if (!TableExists("recruiting_interest"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT player_id, organization_id, season_year, noted_on, heat
			FROM recruiting_interest;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new RecruitingInterest
			{
				PlayerId = Guid.Parse(reader.GetString(0)),
				OrganizationId = reader.GetString(1),
				SeasonYear = reader.GetInt32(2),
				NotedOn = DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
				Heat = reader.GetInt32(4),
			});
		}

		return list;
	}

	private List<RecruitingTarget> ReadTargets()
	{
		var list = new List<RecruitingTarget>();
		if (!TableExists("recruiting_targets"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT player_id, team_id, player_name, season_year
			FROM recruiting_targets;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			string playerText = reader.GetString(0);
			list.Add(new RecruitingTarget
			{
				PlayerId = Guid.TryParse(playerText, out Guid parsed) ? parsed : Guid.Empty,
				TeamId = reader.GetString(1),
				PlayerName = reader.GetString(2),
				SeasonYear = reader.GetInt32(3),
			});
		}

		return list;
	}

	private void InsertOpenPlay(SqliteTransaction tx, DistrictOpenPlay session)
	{
		using (SqliteCommand command = _connection.CreateCommand())
		{
			command.Transaction = tx;
			command.CommandText = "INSERT INTO open_play(district_id, season_year) VALUES ($d, $y);";
			command.Parameters.AddWithValue("$d", session.DistrictId);
			command.Parameters.AddWithValue("$y", session.SeasonYear);
			command.ExecuteNonQuery();
		}

		foreach (OpenPlayLine line in session.Lines)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = """
				INSERT INTO open_play_lines(
					district_id, season_year, player_id, hit_games, at_bats, runs, hits, doubles, triples, home_runs, rbi, walks, strikeouts, stolen_bases,
					pitch_games, games_started, wins, losses, saves, outs, hits_allowed, runs_allowed, earned_runs, walks_allowed, strikeouts_pitch, home_runs_allowed, grade)
				VALUES (
					$dist, $y, $player, $hg, $ab, $r, $h, $d2, $t, $hr, $rbi, $bb, $so, $sb,
					$pg, $gs, $w, $l, $sv, $outs, $ha, $ra, $er, $bbp, $sop, $hra, $grade);
				""";
			command.Parameters.AddWithValue("$dist", session.DistrictId);
			command.Parameters.AddWithValue("$y", session.SeasonYear);
			command.Parameters.AddWithValue("$player", line.PlayerId.ToString());
			command.Parameters.AddWithValue("$hg", line.Hitting.Games);
			command.Parameters.AddWithValue("$ab", line.Hitting.AtBats);
			command.Parameters.AddWithValue("$r", line.Hitting.Runs);
			command.Parameters.AddWithValue("$h", line.Hitting.Hits);
			command.Parameters.AddWithValue("$d2", line.Hitting.Doubles);
			command.Parameters.AddWithValue("$t", line.Hitting.Triples);
			command.Parameters.AddWithValue("$hr", line.Hitting.HomeRuns);
			command.Parameters.AddWithValue("$rbi", line.Hitting.Rbi);
			command.Parameters.AddWithValue("$bb", line.Hitting.Walks);
			command.Parameters.AddWithValue("$so", line.Hitting.Strikeouts);
			command.Parameters.AddWithValue("$sb", line.Hitting.StolenBases);
			command.Parameters.AddWithValue("$pg", (object?)line.Pitching?.Games ?? DBNull.Value);
			command.Parameters.AddWithValue("$gs", (object?)line.Pitching?.GamesStarted ?? DBNull.Value);
			command.Parameters.AddWithValue("$w", (object?)line.Pitching?.Wins ?? DBNull.Value);
			command.Parameters.AddWithValue("$l", (object?)line.Pitching?.Losses ?? DBNull.Value);
			command.Parameters.AddWithValue("$sv", (object?)line.Pitching?.Saves ?? DBNull.Value);
			command.Parameters.AddWithValue("$outs", (object?)line.Pitching?.Outs ?? DBNull.Value);
			command.Parameters.AddWithValue("$ha", (object?)line.Pitching?.Hits ?? DBNull.Value);
			command.Parameters.AddWithValue("$ra", (object?)line.Pitching?.Runs ?? DBNull.Value);
			command.Parameters.AddWithValue("$er", (object?)line.Pitching?.EarnedRuns ?? DBNull.Value);
			command.Parameters.AddWithValue("$bbp", (object?)line.Pitching?.Walks ?? DBNull.Value);
			command.Parameters.AddWithValue("$sop", (object?)line.Pitching?.Strikeouts ?? DBNull.Value);
			command.Parameters.AddWithValue("$hra", (object?)line.Pitching?.HomeRuns ?? DBNull.Value);
			command.Parameters.AddWithValue("$grade", (int)line.Grade);
			command.ExecuteNonQuery();
		}
	}

	private void InsertProgramSeason(SqliteTransaction tx, ProgramSeasonResult season)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO program_season_history(
				organization_id, season_year, wins, losses, regular_wins, regular_losses,
				district_finish, postseason, won_district, won_regional, won_state)
			VALUES ($org, $year, $w, $l, $rw, $rl, $finish, $post, $district, $regional, $state);
			""";
		command.Parameters.AddWithValue("$org", season.OrganizationId);
		command.Parameters.AddWithValue("$year", season.SeasonYear);
		command.Parameters.AddWithValue("$w", season.Wins);
		command.Parameters.AddWithValue("$l", season.Losses);
		command.Parameters.AddWithValue("$rw", season.RegularWins);
		command.Parameters.AddWithValue("$rl", season.RegularLosses);
		command.Parameters.AddWithValue("$finish", season.DistrictFinish);
		command.Parameters.AddWithValue("$post", season.Postseason);
		command.Parameters.AddWithValue("$district", season.WonDistrict ? 1 : 0);
		command.Parameters.AddWithValue("$regional", season.WonRegional ? 1 : 0);
		command.Parameters.AddWithValue("$state", season.WonState ? 1 : 0);
		command.ExecuteNonQuery();
	}

	private void InsertProgramRecord(SqliteTransaction tx, ProgramRecordHolder holder)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO program_records(organization_id, category, rank, player_name, player_id, value)
			VALUES ($org, $cat, $rank, $name, $player, $value);
			""";
		command.Parameters.AddWithValue("$org", holder.OrganizationId);
		command.Parameters.AddWithValue("$cat", (int)holder.Category);
		command.Parameters.AddWithValue("$rank", holder.Rank);
		command.Parameters.AddWithValue("$name", holder.PlayerName);
		command.Parameters.AddWithValue("$player", holder.PlayerId.HasValue ? holder.PlayerId.Value.ToString() : DBNull.Value);
		command.Parameters.AddWithValue("$value", holder.Value);
		command.ExecuteNonQuery();
	}

	private void InsertHallOfFame(SqliteTransaction tx, ProgramHallOfFameInductee inductee)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO program_hall_of_fame(
				organization_id, inducted_year, player_name, player_id, position,
				first_season_year, last_season_year, career_line, honors)
			VALUES ($org, $year, $name, $player, $pos, $first, $last, $line, $honors);
			""";
		command.Parameters.AddWithValue("$org", inductee.OrganizationId);
		command.Parameters.AddWithValue("$year", inductee.InductedYear);
		command.Parameters.AddWithValue("$name", inductee.PlayerName);
		command.Parameters.AddWithValue("$player", inductee.PlayerId.HasValue ? inductee.PlayerId.Value.ToString() : DBNull.Value);
		command.Parameters.AddWithValue("$pos", inductee.Position);
		command.Parameters.AddWithValue("$first", inductee.FirstSeasonYear);
		command.Parameters.AddWithValue("$last", inductee.LastSeasonYear);
		command.Parameters.AddWithValue("$line", inductee.CareerLine);
		command.Parameters.AddWithValue("$honors", inductee.Honors);
		command.ExecuteNonQuery();
	}

	private void AttachProgramArchives(CareerState state)
	{
		Dictionary<string, OrganizationState> orgs = state.Organizations
			.ToDictionary(org => org.Id, StringComparer.Ordinal);
		foreach (ProgramSeasonResult season in ReadProgramSeasons())
		{
			if (orgs.TryGetValue(season.OrganizationId, out OrganizationState? org))
			{
				org.SeasonHistory.Add(season);
			}
		}

		foreach (ProgramRecordHolder holder in ReadProgramRecords())
		{
			if (orgs.TryGetValue(holder.OrganizationId, out OrganizationState? org))
			{
				org.RecordBook.Add(holder);
			}
		}

		foreach (ProgramHallOfFameInductee inductee in ReadHallOfFame())
		{
			if (orgs.TryGetValue(inductee.OrganizationId, out OrganizationState? org))
			{
				org.HallOfFame.Add(inductee);
			}
		}

		foreach (OrganizationState org in state.Organizations)
		{
			org.SeasonHistory.Sort((a, b) => a.SeasonYear.CompareTo(b.SeasonYear));
			org.RecordBook.Sort((a, b) =>
			{
				int category = a.Category.CompareTo(b.Category);
				return category != 0 ? category : a.Rank.CompareTo(b.Rank);
			});
			org.HallOfFame.Sort((a, b) => a.InductedYear.CompareTo(b.InductedYear));
		}
	}

	private List<ProgramSeasonResult> ReadProgramSeasons()
	{
		var list = new List<ProgramSeasonResult>();
		if (!TableExists("program_season_history"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT organization_id, season_year, wins, losses, regular_wins, regular_losses,
				district_finish, postseason, won_district, won_regional, won_state
			FROM program_season_history
			ORDER BY organization_id, season_year;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new ProgramSeasonResult
			{
				OrganizationId = reader.GetString(0),
				SeasonYear = reader.GetInt32(1),
				Wins = reader.GetInt32(2),
				Losses = reader.GetInt32(3),
				RegularWins = reader.GetInt32(4),
				RegularLosses = reader.GetInt32(5),
				DistrictFinish = reader.GetInt32(6),
				Postseason = reader.GetString(7),
				WonDistrict = reader.GetInt32(8) != 0,
				WonRegional = reader.GetInt32(9) != 0,
				WonState = reader.GetInt32(10) != 0,
			});
		}

		return list;
	}

	private List<ProgramRecordHolder> ReadProgramRecords()
	{
		var list = new List<ProgramRecordHolder>();
		if (!TableExists("program_records"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT organization_id, category, rank, player_name, player_id, value
			FROM program_records
			ORDER BY organization_id, category, rank;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new ProgramRecordHolder
			{
				OrganizationId = reader.GetString(0),
				Category = (ProgramRecordCategory)reader.GetInt32(1),
				Rank = reader.GetInt32(2),
				PlayerName = reader.GetString(3),
				PlayerId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
				Value = reader.GetInt32(5),
			});
		}

		return list;
	}

	private List<ProgramHallOfFameInductee> ReadHallOfFame()
	{
		var list = new List<ProgramHallOfFameInductee>();
		if (!TableExists("program_hall_of_fame"))
		{
			return list;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT organization_id, inducted_year, player_name, player_id, position,
				first_season_year, last_season_year, career_line, honors
			FROM program_hall_of_fame
			ORDER BY organization_id, inducted_year;
			""";
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new ProgramHallOfFameInductee
			{
				OrganizationId = reader.GetString(0),
				InductedYear = reader.GetInt32(1),
				PlayerName = reader.GetString(2),
				PlayerId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
				Position = reader.GetString(4),
				FirstSeasonYear = reader.GetInt32(5),
				LastSeasonYear = reader.GetInt32(6),
				CareerLine = reader.GetString(7),
				Honors = reader.GetString(8),
			});
		}

		return list;
	}

	private void EnsureCoachTables()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = CareerSchema.Sql;
		command.ExecuteNonQuery();
	}

	private void InsertProgramCoach(SqliteTransaction tx, ProgramCoach coach)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO program_coaches(organization_id, id, first_name, last_name, age, hometown, appearance_json)
			VALUES ($org, $id, $first, $last, $age, $hometown, $appearance);
			""";
		CoachProfile profile = coach.Profile;
		command.Parameters.AddWithValue("$org", coach.OrganizationId);
		command.Parameters.AddWithValue("$id", (coach.Id == Guid.Empty ? profile.Id : coach.Id).ToString());
		command.Parameters.AddWithValue("$first", profile.FirstName ?? "");
		command.Parameters.AddWithValue("$last", profile.LastName ?? "");
		command.Parameters.AddWithValue("$age", CoachIdentity.ClampAge(profile.Age <= 0 ? CoachIdentity.DefaultAge : profile.Age));
		command.Parameters.AddWithValue("$hometown", profile.Hometown ?? "");
		command.Parameters.AddWithValue("$appearance", JsonSerializer.Serialize(profile.Appearance ?? new PersonAppearance()));
		command.ExecuteNonQuery();
	}

	private void AttachProgramCoaches(CareerState state)
	{
		if (!TableExists("program_coaches"))
		{
			return;
		}

		var coaches = new List<ProgramCoach>();
		using (SqliteCommand command = _connection.CreateCommand())
		{
			command.CommandText = """
				SELECT organization_id, id, first_name, last_name, age, hometown, appearance_json
				FROM program_coaches ORDER BY last_name, first_name;
				""";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				coaches.Add(new ProgramCoach
				{
					OrganizationId = reader.GetString(0),
					Id = Guid.Parse(reader.GetString(1)),
					Profile = new CoachProfile
					{
						Id = Guid.Parse(reader.GetString(1)),
						FirstName = reader.GetString(2),
						LastName = reader.GetString(3),
						Age = reader.GetInt32(4),
						Hometown = reader.GetString(5),
						Appearance = ReadAppearance(reader.GetString(6)),
					},
				});
			}
		}

		state.Coaches = coaches;
		Dictionary<string, OrganizationState> orgs = state.Organizations
			.ToDictionary(org => org.Id, StringComparer.Ordinal);
		foreach (ProgramCoach coach in coaches)
		{
			if (orgs.TryGetValue(coach.OrganizationId, out OrganizationState? org))
			{
				org.HeadCoach = coach;
			}
		}
	}

	private void InsertCoach(SqliteTransaction tx, Career career)
	{
		using (SqliteCommand command = _connection.CreateCommand())
		{
			command.Transaction = tx;
			command.CommandText = """
				INSERT INTO coach(
					career_id, id, first_name, last_name, age, hometown, reputation,
					career_wins, career_losses, district_titles, regional_titles, state_titles,
					seasons_coached, appearance_json, contract_term, contract_years_remaining, annual_salary)
				VALUES (
					$career, $id, $first, $last, $age, $hometown, $rep,
					$wins, $losses, $district, $regional, $state,
					$seasons, $appearance, $term, $remain, $salary);
				""";
			CoachProfile coach = career.Coach;
			command.Parameters.AddWithValue("$career", career.Id.ToString());
			command.Parameters.AddWithValue("$id", (coach.Id == Guid.Empty ? career.Id : coach.Id).ToString());
			command.Parameters.AddWithValue("$first", coach.FirstName ?? "");
			command.Parameters.AddWithValue("$last", coach.LastName ?? "");
			command.Parameters.AddWithValue("$age", CoachIdentity.ClampAge(coach.Age <= 0 ? CoachIdentity.DefaultAge : coach.Age));
			command.Parameters.AddWithValue("$hometown", coach.Hometown ?? "");
			command.Parameters.AddWithValue("$rep", CoachReputation.Clamp(career.Reputation));
			command.Parameters.AddWithValue("$wins", career.CareerWins);
			command.Parameters.AddWithValue("$losses", career.CareerLosses);
			command.Parameters.AddWithValue("$district", career.DistrictTitles);
			command.Parameters.AddWithValue("$regional", career.RegionalTitles);
			command.Parameters.AddWithValue("$state", career.StateTitles);
			command.Parameters.AddWithValue("$seasons", career.SeasonsCoached);
			command.Parameters.AddWithValue("$appearance", JsonSerializer.Serialize(coach.Appearance ?? new PersonAppearance()));
			command.Parameters.AddWithValue("$term", career.Contract.TermYears);
			command.Parameters.AddWithValue("$remain", career.Contract.YearsRemaining);
			command.Parameters.AddWithValue("$salary", career.Contract.AnnualSalary);
			command.ExecuteNonQuery();
		}

		foreach (CoachStint stint in career.Stints)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.Transaction = tx;
			command.CommandText = """
				INSERT INTO coach_stints(
					career_id, organization_id, school_name, start_year, end_year,
					wins, losses, district_titles, regional_titles, state_titles)
				VALUES ($career, $org, $school, $start, $end, $wins, $losses, $district, $regional, $state);
				""";
			command.Parameters.AddWithValue("$career", career.Id.ToString());
			command.Parameters.AddWithValue("$org", stint.OrganizationId);
			command.Parameters.AddWithValue("$school", stint.SchoolName);
			command.Parameters.AddWithValue("$start", stint.StartYear);
			command.Parameters.AddWithValue("$end", stint.EndYear is null or 0 ? DBNull.Value : stint.EndYear.Value);
			command.Parameters.AddWithValue("$wins", stint.Wins);
			command.Parameters.AddWithValue("$losses", stint.Losses);
			command.Parameters.AddWithValue("$district", stint.DistrictTitles);
			command.Parameters.AddWithValue("$regional", stint.RegionalTitles);
			command.Parameters.AddWithValue("$state", stint.StateTitles);
			command.ExecuteNonQuery();
		}

		foreach (CoachSeason season in career.Seasons)
		{
			InsertCoachSeason(tx, career.Id, season);
		}

		foreach (CoachingJobOffer offer in career.JobOffers)
		{
			InsertJobOffer(tx, career.Id, offer);
		}
	}

	private void InsertJobOffer(SqliteTransaction tx, Guid careerId, CoachingJobOffer offer)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO coaching_job_offers(
				id, career_id, organization_id, school_name, school_full_name,
				reputation, facilities_grade, target_expectation, term_years, annual_salary, status)
			VALUES (
				$id, $career, $org, $school, $full,
				$rep, $facilities, $target, $term, $salary, $status);
			""";
		command.Parameters.AddWithValue("$id", offer.Id.ToString());
		command.Parameters.AddWithValue("$career", careerId.ToString());
		command.Parameters.AddWithValue("$org", offer.OrganizationId);
		command.Parameters.AddWithValue("$school", offer.SchoolName);
		command.Parameters.AddWithValue("$full", offer.SchoolFullName);
		command.Parameters.AddWithValue("$rep", offer.Reputation);
		command.Parameters.AddWithValue("$facilities", (int)offer.Facilities);
		command.Parameters.AddWithValue("$target", (int)offer.TargetExpectation);
		command.Parameters.AddWithValue("$term", offer.TermYears);
		command.Parameters.AddWithValue("$salary", offer.AnnualSalary);
		command.Parameters.AddWithValue("$status", (int)offer.Status);
		command.ExecuteNonQuery();
	}

	private void BindCoach(Career career)
	{
		if (!TableExists("coach"))
		{
			return;
		}

		using (SqliteCommand command = _connection.CreateCommand())
		{
			command.CommandText = """
				SELECT id, first_name, last_name, age, hometown, reputation,
					career_wins, career_losses, district_titles, regional_titles, state_titles,
					seasons_coached, appearance_json, contract_term, contract_years_remaining, annual_salary
				FROM coach WHERE career_id = $id LIMIT 1;
				""";
			command.Parameters.AddWithValue("$id", career.Id.ToString());
			using SqliteDataReader reader = command.ExecuteReader();
			if (!reader.Read())
			{
				return;
			}

			career.Coach = new CoachProfile
			{
				Id = Guid.Parse(reader.GetString(0)),
				FirstName = reader.GetString(1),
				LastName = reader.GetString(2),
				Age = reader.GetInt32(3),
				Hometown = reader.GetString(4),
				Appearance = ReadAppearance(reader.GetString(12)),
			};
			career.Reputation = reader.GetInt32(5);
			career.CareerWins = reader.GetInt32(6);
			career.CareerLosses = reader.GetInt32(7);
			career.DistrictTitles = reader.GetInt32(8);
			career.RegionalTitles = reader.GetInt32(9);
			career.StateTitles = reader.GetInt32(10);
			career.SeasonsCoached = reader.GetInt32(11);
			career.Contract = new CoachContract
			{
				OrganizationId = career.UserOrganizationId,
				StartYear = career.CurrentSeasonYear,
				TermYears = reader.FieldCount > 13 ? reader.GetInt32(13) : 0,
				YearsRemaining = reader.FieldCount > 14 ? reader.GetInt32(14) : 0,
				AnnualSalary = reader.FieldCount > 15 ? reader.GetInt32(15) : 0,
			};
		}

		if (!TableExists("coach_stints"))
		{
			BindJobOffers(career);
			return;
		}

		using (SqliteCommand command = _connection.CreateCommand())
		{
			command.CommandText = """
				SELECT organization_id, school_name, start_year, end_year,
					wins, losses, district_titles, regional_titles, state_titles
				FROM coach_stints WHERE career_id = $id ORDER BY start_year, school_name;
				""";
			command.Parameters.AddWithValue("$id", career.Id.ToString());
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				career.Stints.Add(new CoachStint
				{
					OrganizationId = reader.GetString(0),
					SchoolName = reader.GetString(1),
					StartYear = reader.GetInt32(2),
					EndYear = reader.IsDBNull(3) ? null : reader.GetInt32(3),
					Wins = reader.FieldCount > 4 && !reader.IsDBNull(4) ? reader.GetInt32(4) : 0,
					Losses = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetInt32(5) : 0,
					DistrictTitles = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetInt32(6) : 0,
					RegionalTitles = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetInt32(7) : 0,
					StateTitles = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetInt32(8) : 0,
				});
			}
		}

		BindCoachSeasons(career);

		CoachStint? current = career.Stints.LastOrDefault(stint => stint.EndYear is null or 0);
		if (current != null)
		{
			career.Contract.SchoolName = current.SchoolName;
			career.Contract.OrganizationId = current.OrganizationId;
		}

		BindJobOffers(career);
	}

	private void InsertCoachSeason(SqliteTransaction tx, Guid careerId, CoachSeason season)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = """
			INSERT INTO coach_seasons(
				career_id, season_year, organization_id, school_name, wins, losses,
				district_finish, postseason, won_district, won_regional, won_state)
			VALUES (
				$career, $year, $org, $school, $wins, $losses,
				$finish, $post, $district, $regional, $state);
			""";
		command.Parameters.AddWithValue("$career", careerId.ToString());
		command.Parameters.AddWithValue("$year", season.SeasonYear);
		command.Parameters.AddWithValue("$org", season.OrganizationId);
		command.Parameters.AddWithValue("$school", season.SchoolName);
		command.Parameters.AddWithValue("$wins", season.Wins);
		command.Parameters.AddWithValue("$losses", season.Losses);
		command.Parameters.AddWithValue("$finish", season.DistrictFinish);
		command.Parameters.AddWithValue("$post", season.Postseason ?? "");
		command.Parameters.AddWithValue("$district", season.WonDistrict ? 1 : 0);
		command.Parameters.AddWithValue("$regional", season.WonRegional ? 1 : 0);
		command.Parameters.AddWithValue("$state", season.WonState ? 1 : 0);
		command.ExecuteNonQuery();
	}

	private void BindCoachSeasons(Career career)
	{
		if (!TableExists("coach_seasons"))
		{
			return;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT season_year, organization_id, school_name, wins, losses,
				district_finish, postseason, won_district, won_regional, won_state
			FROM coach_seasons WHERE career_id = $id ORDER BY season_year, school_name;
			""";
		command.Parameters.AddWithValue("$id", career.Id.ToString());
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			career.Seasons.Add(new CoachSeason
			{
				SeasonYear = reader.GetInt32(0),
				OrganizationId = reader.GetString(1),
				SchoolName = reader.GetString(2),
				Wins = reader.GetInt32(3),
				Losses = reader.GetInt32(4),
				DistrictFinish = reader.GetInt32(5),
				Postseason = reader.GetString(6),
				WonDistrict = reader.GetInt32(7) != 0,
				WonRegional = reader.GetInt32(8) != 0,
				WonState = reader.GetInt32(9) != 0,
			});
		}
	}

	private void BindJobOffers(Career career)
	{
		if (!TableExists("coaching_job_offers"))
		{
			return;
		}

		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = """
			SELECT id, organization_id, school_name, school_full_name, reputation, facilities_grade,
				target_expectation, term_years, annual_salary, status
			FROM coaching_job_offers WHERE career_id = $id ORDER BY reputation DESC, school_name;
			""";
		command.Parameters.AddWithValue("$id", career.Id.ToString());
		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			career.JobOffers.Add(new CoachingJobOffer
			{
				Id = Guid.Parse(reader.GetString(0)),
				OrganizationId = reader.GetString(1),
				SchoolName = reader.GetString(2),
				SchoolFullName = reader.GetString(3),
				Reputation = reader.GetInt32(4),
				Facilities = (LetterGrade)reader.GetInt32(5),
				TargetExpectation = (BoardExpectation)reader.GetInt32(6),
				TermYears = reader.GetInt32(7),
				AnnualSalary = reader.GetInt32(8),
				Status = (CoachingJobOfferStatus)reader.GetInt32(9),
			});
		}
	}

	private static PlayerInjury? ReadInjury(SqliteDataReader reader)
	{
		if (reader.FieldCount <= 33 || reader.IsDBNull(31) || reader.IsDBNull(32) || reader.IsDBNull(33))
		{
			return null;
		}

		string kind = reader.GetString(31);
		if (string.IsNullOrWhiteSpace(kind))
		{
			return null;
		}

		return new PlayerInjury
		{
			Kind = kind,
			Start = DateOnly.Parse(reader.GetString(32), CultureInfo.InvariantCulture),
			AvailableOn = DateOnly.Parse(reader.GetString(33), CultureInfo.InvariantCulture),
			DayToDay = reader.FieldCount > 34 && !reader.IsDBNull(34) && reader.GetInt32(34) != 0,
		};
	}

	private static PersonAppearance ReadAppearance(string json)
	{
		if (string.IsNullOrWhiteSpace(json) || json == "{}")
		{
			return new PersonAppearance();
		}

		try
		{
			return JsonSerializer.Deserialize<PersonAppearance>(json) ?? new PersonAppearance();
		}
		catch (JsonException)
		{
			return new PersonAppearance();
		}
	}

	private void EnsurePostseasonArchiveTables()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = CareerSchema.Sql;
		command.ExecuteNonQuery();
	}

	private void AttachPostseasonArchive(CareerState state)
	{
		if (TableExists("postseason_state"))
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT payload_json FROM postseason_state ORDER BY season_year DESC LIMIT 1;";
			using SqliteDataReader reader = command.ExecuteReader();
			if (reader.Read())
			{
				state.World.Postseason = JsonSerializer.Deserialize<PostseasonState>(reader.GetString(0));
			}
		}

		if (TableExists("season_awards"))
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT payload_json FROM season_awards;";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				SeasonAwards? slate = JsonSerializer.Deserialize<SeasonAwards>(reader.GetString(0));
				if (slate != null)
				{
					state.World.Awards.Add(slate);
				}
			}
		}

		if (TableExists("historical_seasons"))
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT payload_json FROM historical_seasons ORDER BY season_year;";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				HistoricalSeason? season = JsonSerializer.Deserialize<HistoricalSeason>(reader.GetString(0));
				if (season != null)
				{
					state.World.HistoricalSeasons.Add(season);
				}
			}
		}

		if (TableExists("season_profile_commits"))
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT commit_key FROM season_profile_commits;";
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				state.World.SeasonProfileCommits.Add(reader.GetString(0));
			}
		}
	}

	private void EnsureProgramArchiveTables()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = CareerSchema.Sql;
		command.ExecuteNonQuery();
	}

	private void EnsureRecruitingTables()
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = CareerSchema.Sql;
		command.ExecuteNonQuery();
	}

	private bool TableExists(string table)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n;";
		command.Parameters.AddWithValue("$n", table);
		return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
	}

	private void Execute(SqliteTransaction tx, string sql)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.Transaction = tx;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}
