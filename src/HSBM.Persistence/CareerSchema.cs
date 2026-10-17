namespace HSBM.Persistence;

internal static class CareerSchema
{
	public const int Version = 16;

	public const string Sql = """
		PRAGMA foreign_keys = ON;
		PRAGMA journal_mode = WAL;

		CREATE TABLE IF NOT EXISTS meta (
			key TEXT PRIMARY KEY,
			value TEXT NOT NULL
		);

		CREATE TABLE IF NOT EXISTS career (
			id TEXT PRIMARY KEY,
			name TEXT NOT NULL,
			created_at TEXT NOT NULL,
			simulation_date TEXT NOT NULL,
			current_season_year INTEGER NOT NULL,
			user_organization_id TEXT NOT NULL,
			random_seed INTEGER NOT NULL,
			competition_level INTEGER NOT NULL DEFAULT 0,
			save_version INTEGER NOT NULL
		);

		CREATE TABLE IF NOT EXISTS districts (
			id TEXT PRIMARY KEY,
			name TEXT NOT NULL,
			abbreviation TEXT NOT NULL,
			region TEXT NOT NULL
		);

		CREATE TABLE IF NOT EXISTS organizations (
			id TEXT PRIMARY KEY,
			name TEXT NOT NULL,
			short_name TEXT NOT NULL,
			district_id TEXT NOT NULL,
			roster_cap INTEGER NOT NULL,
			talent_tier INTEGER NOT NULL,
			competition_level INTEGER NOT NULL DEFAULT 0,
			reputation INTEGER NOT NULL DEFAULT 50,
			facilities_grade INTEGER NOT NULL DEFAULT 5,
			baseball_resources INTEGER NOT NULL DEFAULT 5,
			baseball_budget INTEGER NOT NULL DEFAULT 40000,
			last_season_wins INTEGER NOT NULL DEFAULT 13,
			last_season_losses INTEGER NOT NULL DEFAULT 13,
			district_finish INTEGER NOT NULL DEFAULT 4,
			district_titles INTEGER NOT NULL DEFAULT 0,
			regional_titles INTEGER NOT NULL DEFAULT 0,
			state_titles INTEGER NOT NULL DEFAULT 0,
			minimum_expectation INTEGER NOT NULL DEFAULT 1,
			target_expectation INTEGER NOT NULL DEFAULT 2,
			has_opening INTEGER NOT NULL DEFAULT 0,
			vacancy_reason INTEGER NOT NULL DEFAULT 0,
			FOREIGN KEY (district_id) REFERENCES districts(id)
		);

		CREATE TABLE IF NOT EXISTS teams (
			id TEXT PRIMARY KEY,
			organization_id TEXT NOT NULL,
			level INTEGER NOT NULL,
			roster_cap INTEGER NOT NULL,
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS players (
			id TEXT PRIMARY KEY,
			first_name TEXT NOT NULL,
			last_name TEXT NOT NULL,
			generated_season_year INTEGER NOT NULL,
			bats INTEGER NOT NULL,
			throws INTEGER NOT NULL,
			primary_position INTEGER NOT NULL,
			secondary_position INTEGER,
			grade INTEGER NOT NULL,
			age INTEGER NOT NULL,
			organization_id TEXT NOT NULL,
			team_id TEXT NOT NULL,
			team_level INTEGER NOT NULL,
			jersey INTEGER NOT NULL,
			contact INTEGER NOT NULL,
			power INTEGER NOT NULL,
			plate_discipline INTEGER NOT NULL,
			speed INTEGER NOT NULL,
			fielding INTEGER NOT NULL,
			arm_strength INTEGER NOT NULL,
			stuff INTEGER NOT NULL,
			control INTEGER NOT NULL,
			movement INTEGER NOT NULL,
			stamina INTEGER NOT NULL,
			potential INTEGER NOT NULL DEFAULT 50,
			recruiting_archetype INTEGER NOT NULL DEFAULT 0,
			appearance_json TEXT NOT NULL DEFAULT '{}',
			height_inches INTEGER NOT NULL DEFAULT 70,
			weight_lbs INTEGER NOT NULL DEFAULT 170,
			strength INTEGER NOT NULL DEFAULT 50,
			personality INTEGER NOT NULL DEFAULT 0,
			injury_kind TEXT,
			injury_start TEXT,
			injury_available_on TEXT,
			injury_day_to_day INTEGER NOT NULL DEFAULT 0,
			alumni INTEGER NOT NULL DEFAULT 0,
			graduated_year INTEGER NOT NULL DEFAULT 0,
			college_id TEXT NOT NULL DEFAULT '',
			college_school TEXT NOT NULL DEFAULT '',
			college_path TEXT NOT NULL DEFAULT '',
			FOREIGN KEY (organization_id) REFERENCES organizations(id),
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS player_agreements (
			id TEXT PRIMARY KEY,
			player_id TEXT NOT NULL,
			organization_id TEXT NOT NULL,
			start_season_year INTEGER NOT NULL,
			term_years INTEGER NOT NULL,
			years_remaining INTEGER NOT NULL,
			annual_sponsorship INTEGER NOT NULL,
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS recruiting_offers (
			id TEXT PRIMARY KEY,
			player_id TEXT NOT NULL,
			organization_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			annual_sponsorship INTEGER NOT NULL,
			term_years INTEGER NOT NULL,
			offered_on TEXT NOT NULL,
			status INTEGER NOT NULL,
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS recruiting_interest (
			player_id TEXT NOT NULL,
			organization_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			noted_on TEXT NOT NULL,
			heat INTEGER NOT NULL,
			PRIMARY KEY (player_id, organization_id, season_year),
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS recruiting_targets (
			player_id TEXT NOT NULL DEFAULT '',
			team_id TEXT NOT NULL,
			player_name TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			PRIMARY KEY (player_id, team_id, player_name, season_year)
		);

		CREATE TABLE IF NOT EXISTS open_play (
			district_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			PRIMARY KEY (district_id, season_year),
			FOREIGN KEY (district_id) REFERENCES districts(id)
		);

		CREATE TABLE IF NOT EXISTS open_play_lines (
			district_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			player_id TEXT NOT NULL,
			hit_games INTEGER NOT NULL,
			at_bats INTEGER NOT NULL,
			runs INTEGER NOT NULL,
			hits INTEGER NOT NULL,
			doubles INTEGER NOT NULL,
			triples INTEGER NOT NULL,
			home_runs INTEGER NOT NULL,
			rbi INTEGER NOT NULL,
			walks INTEGER NOT NULL,
			strikeouts INTEGER NOT NULL,
			stolen_bases INTEGER NOT NULL,
			pitch_games INTEGER,
			games_started INTEGER,
			wins INTEGER,
			losses INTEGER,
			saves INTEGER,
			outs INTEGER,
			hits_allowed INTEGER,
			runs_allowed INTEGER,
			earned_runs INTEGER,
			walks_allowed INTEGER,
			strikeouts_pitch INTEGER,
			home_runs_allowed INTEGER,
			grade INTEGER NOT NULL,
			PRIMARY KEY (district_id, season_year, player_id),
			FOREIGN KEY (player_id) REFERENCES players(id)
		);

		CREATE TABLE IF NOT EXISTS games (
			id TEXT PRIMARY KEY,
			season_year INTEGER NOT NULL,
			game_date TEXT NOT NULL,
			home_team_id TEXT NOT NULL,
			away_team_id TEXT NOT NULL,
			team_level INTEGER NOT NULL,
			game_type INTEGER NOT NULL,
			status INTEGER NOT NULL,
			home_score INTEGER,
			away_score INTEGER,
			innings_played INTEGER,
			winning_team_id TEXT,
			losing_team_id TEXT,
			winning_pitcher_id TEXT,
			losing_pitcher_id TEXT,
			save_pitcher_id TEXT,
			FOREIGN KEY (home_team_id) REFERENCES teams(id),
			FOREIGN KEY (away_team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS team_season_records (
			team_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			wins INTEGER NOT NULL,
			losses INTEGER NOT NULL,
			district_wins INTEGER NOT NULL,
			district_losses INTEGER NOT NULL,
			runs_for INTEGER NOT NULL,
			runs_against INTEGER NOT NULL,
			PRIMARY KEY (team_id, season_year),
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS batting_stats (
			player_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			team_id TEXT NOT NULL,
			games INTEGER NOT NULL,
			plate_appearances INTEGER NOT NULL,
			at_bats INTEGER NOT NULL,
			runs INTEGER NOT NULL,
			hits INTEGER NOT NULL,
			doubles INTEGER NOT NULL,
			triples INTEGER NOT NULL,
			home_runs INTEGER NOT NULL,
			rbi INTEGER NOT NULL,
			walks INTEGER NOT NULL,
			strikeouts INTEGER NOT NULL,
			stolen_bases INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (player_id, season_year),
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS pitching_stats (
			player_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			team_id TEXT NOT NULL,
			games INTEGER NOT NULL,
			games_started INTEGER NOT NULL,
			wins INTEGER NOT NULL,
			losses INTEGER NOT NULL,
			outs_recorded INTEGER NOT NULL,
			hits_allowed INTEGER NOT NULL,
			runs_allowed INTEGER NOT NULL,
			earned_runs INTEGER NOT NULL,
			walks INTEGER NOT NULL,
			strikeouts INTEGER NOT NULL,
			home_runs_allowed INTEGER NOT NULL,
			saves INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (player_id, season_year),
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS fielding_stats (
			player_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			team_id TEXT NOT NULL,
			games INTEGER NOT NULL,
			putouts INTEGER NOT NULL,
			assists INTEGER NOT NULL,
			errors INTEGER NOT NULL,
			PRIMARY KEY (player_id, season_year),
			FOREIGN KEY (player_id) REFERENCES players(id),
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS team_lineups (
			team_id TEXT PRIMARY KEY,
			slots_json TEXT NOT NULL,
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS team_rotations (
			team_id TEXT PRIMARY KEY,
			starters_json TEXT NOT NULL,
			closer_id TEXT,
			setup_id TEXT,
			middle_id TEXT,
			long_relief_id TEXT,
			FOREIGN KEY (team_id) REFERENCES teams(id)
		);

		CREATE TABLE IF NOT EXISTS game_box_scores (
			game_id TEXT PRIMARY KEY,
			payload_json TEXT NOT NULL,
			FOREIGN KEY (game_id) REFERENCES games(id)
		);

		CREATE TABLE IF NOT EXISTS program_season_history (
			organization_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			wins INTEGER NOT NULL,
			losses INTEGER NOT NULL,
			regular_wins INTEGER NOT NULL,
			regular_losses INTEGER NOT NULL,
			district_finish INTEGER NOT NULL,
			postseason TEXT NOT NULL,
			won_district INTEGER NOT NULL,
			won_regional INTEGER NOT NULL,
			won_state INTEGER NOT NULL,
			PRIMARY KEY (organization_id, season_year),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS program_records (
			organization_id TEXT NOT NULL,
			category INTEGER NOT NULL,
			rank INTEGER NOT NULL,
			player_name TEXT NOT NULL,
			player_id TEXT,
			value INTEGER NOT NULL,
			PRIMARY KEY (organization_id, category, rank),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS program_hall_of_fame (
			organization_id TEXT NOT NULL,
			inducted_year INTEGER NOT NULL,
			player_name TEXT NOT NULL,
			player_id TEXT,
			position TEXT NOT NULL,
			first_season_year INTEGER NOT NULL,
			last_season_year INTEGER NOT NULL,
			career_line TEXT NOT NULL,
			honors TEXT NOT NULL,
			PRIMARY KEY (organization_id, inducted_year),
			FOREIGN KEY (organization_id) REFERENCES organizations(id)
		);

		CREATE TABLE IF NOT EXISTS coach (
			career_id TEXT PRIMARY KEY,
			id TEXT NOT NULL,
			first_name TEXT NOT NULL,
			last_name TEXT NOT NULL,
			age INTEGER NOT NULL,
			hometown TEXT NOT NULL,
			reputation INTEGER NOT NULL DEFAULT 42,
			career_wins INTEGER NOT NULL DEFAULT 0,
			career_losses INTEGER NOT NULL DEFAULT 0,
			district_titles INTEGER NOT NULL DEFAULT 0,
			regional_titles INTEGER NOT NULL DEFAULT 0,
			state_titles INTEGER NOT NULL DEFAULT 0,
			seasons_coached INTEGER NOT NULL DEFAULT 0,
			appearance_json TEXT NOT NULL DEFAULT '{}',
			contract_term INTEGER NOT NULL DEFAULT 0,
			contract_years_remaining INTEGER NOT NULL DEFAULT 0,
			annual_salary INTEGER NOT NULL DEFAULT 0
		);

		CREATE TABLE IF NOT EXISTS coach_stints (
			career_id TEXT NOT NULL,
			organization_id TEXT NOT NULL,
			school_name TEXT NOT NULL,
			start_year INTEGER NOT NULL,
			end_year INTEGER,
			wins INTEGER NOT NULL DEFAULT 0,
			losses INTEGER NOT NULL DEFAULT 0,
			district_titles INTEGER NOT NULL DEFAULT 0,
			regional_titles INTEGER NOT NULL DEFAULT 0,
			state_titles INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (career_id, organization_id, start_year)
		);

		CREATE TABLE IF NOT EXISTS coach_seasons (
			career_id TEXT NOT NULL,
			season_year INTEGER NOT NULL,
			organization_id TEXT NOT NULL,
			school_name TEXT NOT NULL,
			wins INTEGER NOT NULL,
			losses INTEGER NOT NULL,
			district_finish INTEGER NOT NULL,
			postseason TEXT NOT NULL,
			won_district INTEGER NOT NULL,
			won_regional INTEGER NOT NULL,
			won_state INTEGER NOT NULL,
			PRIMARY KEY (career_id, season_year)
		);

		CREATE TABLE IF NOT EXISTS coaching_job_offers (
			id TEXT PRIMARY KEY,
			career_id TEXT NOT NULL,
			organization_id TEXT NOT NULL,
			school_name TEXT NOT NULL,
			school_full_name TEXT NOT NULL,
			reputation INTEGER NOT NULL,
			facilities_grade INTEGER NOT NULL,
			target_expectation INTEGER NOT NULL,
			term_years INTEGER NOT NULL,
			annual_salary INTEGER NOT NULL,
			status INTEGER NOT NULL
		);

		CREATE TABLE IF NOT EXISTS program_coaches (
			organization_id TEXT PRIMARY KEY,
			id TEXT NOT NULL,
			first_name TEXT NOT NULL,
			last_name TEXT NOT NULL,
			age INTEGER NOT NULL,
			hometown TEXT NOT NULL,
			appearance_json TEXT NOT NULL DEFAULT '{}'
		);

		CREATE TABLE IF NOT EXISTS news_stories (
			id TEXT PRIMARY KEY,
			story_date TEXT NOT NULL,
			tag TEXT NOT NULL,
			headline TEXT NOT NULL,
			body TEXT NOT NULL,
			pull TEXT NOT NULL,
			dedup_key TEXT NOT NULL UNIQUE,
			is_read INTEGER NOT NULL,
			subject_organization_id TEXT,
			subject_player_name TEXT
		);

		CREATE TABLE IF NOT EXISTS player_transactions (
			id INTEGER PRIMARY KEY AUTOINCREMENT,
			kind INTEGER NOT NULL,
			txn_date TEXT NOT NULL,
			date_label TEXT NOT NULL,
			headline TEXT NOT NULL,
			detail TEXT NOT NULL,
			organization_id TEXT NOT NULL DEFAULT '',
			player_id TEXT NOT NULL DEFAULT '',
			dedup_key TEXT NOT NULL UNIQUE
		);

		CREATE TABLE IF NOT EXISTS postseason_state (
			season_year INTEGER PRIMARY KEY,
			payload_json TEXT NOT NULL
		);

		CREATE TABLE IF NOT EXISTS season_awards (
			season_year INTEGER NOT NULL,
			scope INTEGER NOT NULL,
			payload_json TEXT NOT NULL,
			PRIMARY KEY (season_year, scope)
		);

		CREATE TABLE IF NOT EXISTS historical_seasons (
			season_year INTEGER PRIMARY KEY,
			payload_json TEXT NOT NULL
		);

		CREATE TABLE IF NOT EXISTS season_profile_commits (
			commit_key TEXT PRIMARY KEY
		);

		CREATE INDEX IF NOT EXISTS idx_players_team ON players(team_id);
		CREATE INDEX IF NOT EXISTS idx_games_date ON games(game_date, status);
		""";
}
