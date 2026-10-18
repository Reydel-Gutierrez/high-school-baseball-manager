using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;

namespace HSBM.Persistence.Tests;

public class CareerPersistenceTests
{
	[Fact]
	public void Create_save_reopen_keeps_career_identical()
	{
		string path = NewDb();
		try
		{
			CreateCareerResult created;
			string fingerprint;
			DateOnly date;
			int seed;
			using (CareerService service = Open(path))
			{
				created = service.CreateCareer(new CreateCareerRequest { Name = "Save Test", Seed = 4242 });
				CareerState state = service.LoadCareer();
				fingerprint = Fingerprint(state);
				date = state.Career.CurrentDate;
				seed = state.Career.RandomSeed;
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal(created.CareerId, loaded.Career.Id);
				Assert.Equal("Save Test", loaded.Career.Name);
				Assert.Equal(seed, loaded.Career.RandomSeed);
				Assert.Equal(date, loaded.Career.CurrentDate);
				Assert.Equal(fingerprint, Fingerprint(loaded));
				Assert.Equal(33, loaded.Organizations.Count);
				// 64 program teams + 32 tryout lists + the inactive list.
				Assert.Equal(97, loaded.Teams.Count);
				Assert.Equal(CompetitionLevel.HighSchool, loaded.Career.CompetitionLevel);
				Assert.Equal(CompetitionLevel.HighSchool, loaded.World.CompetitionLevel);
				Assert.Equal(4242, loaded.World.Seed);
				Assert.Equal(date, loaded.World.CurrentDate);
				Assert.Equal(32, loaded.Coaches.Count);
				Assert.All(loaded.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)), org =>
				{
					Assert.Equal(CompetitionLevel.HighSchool, org.CompetitionLevel);
					Assert.NotNull(org.HeadCoach);
					Assert.InRange((int)org.Profile.BaseballResources, (int)LetterGrade.F, (int)LetterGrade.APlus);
					Assert.Equal(org.TalentTier, ProgramReputation.TalentBand(org.Profile.Reputation));
				});
				Assert.All(
					loaded.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)),
					org => Assert.InRange(loaded.TryoutCandidates(org.Id).Count(), 8, 18));
				Assert.All(loaded.Teams.Where(team => team.RosterCap > 0), team =>
					Assert.InRange(loaded.Players.Count(p => p.TeamId == team.Id), 12, team.RosterCap));
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Advance_day_persists_date_results_and_stats()
	{
		string path = NewDb();
		try
		{
			DateOnly previous;
			DateOnly expectedNext;
			int gamesPlayed;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Advance", Seed = 17 });
				CareerState before = service.LoadCareer();
				previous = before.Career.CurrentDate;
				expectedNext = previous.AddDays(1);
				AdvanceDayResult result = service.AdvanceDay();
				gamesPlayed = result.GamesPlayed.Count;
				Assert.Equal(previous, result.PreviousDate);
				Assert.Equal(expectedNext, result.NewDate);
				Assert.All(result.GamesPlayed, game => Assert.NotEqual(game.HomeScore, game.AwayScore));
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal(expectedNext, loaded.Career.CurrentDate);
				Assert.Equal(expectedNext, loaded.World.CurrentDate);
				int completed = loaded.Games.Count(g => g.Date == previous && g.Status == GameStatus.Completed);
				Assert.Equal(gamesPlayed, completed);
				Assert.DoesNotContain(loaded.Games, g => g.Date == previous && g.Status == GameStatus.Scheduled);
				if (gamesPlayed > 0)
				{
					Assert.Contains(loaded.TeamRecords, r => r.Wins + r.Losses > 0);
					Assert.Contains(loaded.BattingStats, s => s.PlateAppearances > 0);
					Assert.Contains(loaded.PitchingStats, s => s.OutsRecorded > 0);
					Assert.Equal(gamesPlayed, loaded.BoxScores.Count);
					Assert.All(loaded.Games.Where(g => g.Date == previous && g.Status == GameStatus.Completed), g =>
					{
						Assert.NotNull(loaded.BoxScoreFor(g.Id));
					});
				}
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Create_career_publishes_open_play_news_and_reopen_keeps_it()
	{
		string path = NewDb();
		try
		{
			Guid storyId;
			string headline;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Wire", Seed = 21 });
				IReadOnlyList<NewsStory> news = service.GetNews();
				Assert.NotEmpty(news);
				Assert.Contains(news, story => story.Headline.Contains("Open Play", StringComparison.OrdinalIgnoreCase));
				NewsStory first = news[0];
				storyId = first.Id;
				headline = first.Headline;
				Assert.False(first.Read);
				service.MarkNewsRead();
			}

			using (CareerService reopened = Open(path))
			{
				IReadOnlyList<NewsStory> loaded = reopened.GetNews();
				Assert.Contains(loaded, story => story.Id == storyId && story.Headline == headline && story.Read);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Advance_day_moves_exactly_one_calendar_day_and_does_not_resimulate()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			service.CreateCareer(new CreateCareerRequest { Name = "Once", Seed = 99 });
			CareerState start = service.LoadCareer();
			DateOnly day = start.Career.CurrentDate;
			AdvanceDayResult first = service.AdvanceDay();
			Assert.Equal(day.AddDays(1), first.NewDate);

			List<ScheduledGame> afterFirst = service.GetSchedule()
				.Where(g => g.Date == day && g.Status == GameStatus.Completed)
				.ToList();
			string scores = string.Join("|", afterFirst.OrderBy(g => g.Id).Select(g => $"{g.Id}:{g.HomeScore}-{g.AwayScore}"));

			AdvanceDayResult second = service.AdvanceDay();
			Assert.Equal(day.AddDays(2), second.NewDate);
			Assert.DoesNotContain(second.GamesPlayed, g => g.Date == day);

			string scoresAgain = string.Join("|", service.GetSchedule()
				.Where(g => g.Date == day && g.Status == GameStatus.Completed)
				.OrderBy(g => g.Id)
				.Select(g => $"{g.Id}:{g.HomeScore}-{g.AwayScore}"));
			Assert.Equal(scores, scoresAgain);
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Simulate_until_next_event_stops_on_signing_week()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			service.CreateCareer(new CreateCareerRequest { Name = "Until", Seed = 11, UserOrganizationId = "cascade" });
			CareerState start = service.LoadCareer();
			DateOnly origin = start.Career.CurrentDate;
			Assert.Equal(SeasonCalendar.TryoutSignupDay(2026), origin);

			string teamId = start.Teams.First(team => team.OrganizationId == "cascade" && team.Level == TeamLevel.Varsity).Id;
			AdvanceDayResult result = service.SimulateUntilNextEvent(teamId, 40);
			Assert.True(result.NewDate > origin);
			Assert.True(result.NewDate <= SeasonCalendar.TryoutWeek(2026).Start);
			CareerState loaded = service.LoadCareer();
			Assert.Equal(result.NewDate, loaded.World.CurrentDate);
			Assert.Equal(loaded.Career.CurrentDate, loaded.World.CurrentDate);
			Assert.Equal(CareerCalendar.PhaseOn(2026, loaded.Career.CurrentDate).ToLabel(), result.Phase.ToLabel());
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Season_rollover_advances_the_baseball_year()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			service.CreateCareer(new CreateCareerRequest { Name = "Rollover", Seed = 5 });
			service.SetCurrentDate(new DateOnly(2027, 1, 4));
			AdvanceDayResult result = service.AdvanceDay();
			Assert.True(result.RolledOver);
			Assert.Equal(new DateOnly(2027, 1, 5), result.NewDate);

			CareerState loaded = service.LoadCareer();
			Assert.Equal(2027, loaded.Career.CurrentSeasonYear);
			Assert.Equal(2027, loaded.World.CurrentSeasonYear);
			Assert.Equal(new DateOnly(2027, 1, 5), loaded.World.CurrentDate);
			Assert.Contains(loaded.Games, game => game.SeasonYear == 2027 && game.Status == GameStatus.Scheduled);
			Assert.Contains(loaded.Players, player => player.IsUnsigned && player.GeneratedSeasonYear == 2027);
			Assert.Contains(loaded.Players, player => player.Alumni);
			Player alum = loaded.Players.First(player => player.Alumni);
			Assert.Equal(2026, alum.GraduatedYear);
			Assert.False(string.IsNullOrWhiteSpace(alum.CollegeSchool));
			Assert.False(string.IsNullOrWhiteSpace(alum.CollegeId));
			Assert.False(string.IsNullOrWhiteSpace(alum.CollegePath));
			IReadOnlyList<ProgramGraduate> graduates = service.GetGraduates(alum.OrganizationId, 2026);
			Assert.Contains(graduates, row => row.PlayerId == alum.Id);
			Assert.All(graduates, row => Assert.True(row.HasDestination));
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Rosters_are_not_regenerated_on_load()
	{
		string path = NewDb();
		try
		{
			Guid firstPlayer;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Stable", Seed = 55 });
				firstPlayer = service.LoadCareer().Players[0].Id;
			}

			using (CareerService reopened = Open(path))
			{
				Assert.Contains(reopened.LoadCareer().Players, p => p.Id == firstPlayer);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void New_career_stays_unsigned_until_a_contract_is_signed()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			CreateCareerResult created = service.CreateCareer(new CreateCareerRequest { Name = "Job Market", Seed = 2026 });
			Assert.Equal("", created.UserOrganizationId);
			Assert.False(service.LoadCareer().Career.HasSignedContract);

			IReadOnlyList<JobListing> board = service.GetJobBoard();
			Assert.InRange(board.Count, 2, 3);
			Assert.Equal(board.Count, board.Select(j => j.OrganizationId).Distinct().Count());
			Assert.All(board, job =>
			{
				Assert.False(string.IsNullOrWhiteSpace(job.SchoolName));
				Assert.False(string.IsNullOrWhiteSpace(job.DistrictName));
				Assert.InRange(job.Reputation, 1, 99);
				Assert.Equal(ProgramProfile.SeasonGames, job.LastSeasonWins + job.LastSeasonLosses);
			});

			CareerState loaded = service.LoadCareer();
			string closed = loaded.Organizations.First(org => !org.Profile.HasOpening).Id;
			Assert.Throws<InvalidOperationException>(() => service.SignContract(closed));

			JobListing pick = board[0];
			SignContractResult signed = service.SignContract(pick.OrganizationId);
			Assert.Equal(pick.OrganizationId, signed.OrganizationId);
			CareerState state = service.LoadCareer();
			Assert.True(state.Career.HasSignedContract);
			Assert.Equal(pick.OrganizationId, state.Career.UserOrganizationId);
			Assert.Throws<InvalidOperationException>(() => service.SignContract(board[0].OrganizationId));
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Coach_dossier_survives_save_and_signing_opens_a_stint()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			var coach = new CoachProfile
			{
				Id = Guid.NewGuid(),
				FirstName = "Reydel",
				LastName = "Gutierrez",
				Age = 34,
				Hometown = "Miami, FL",
			};
			service.CreateCareer(new CreateCareerRequest
			{
				Name = "Dossier",
				Seed = 2026,
				Coach = coach,
			});
			CareerState created = service.LoadCareer();
			Assert.Equal("Reydel Gutierrez", created.Career.DisplayName);
			Assert.Equal(CoachReputation.Starting, created.Career.Reputation);
			Assert.Equal("SEEKING A SEAT", created.Career.SeasonLabel);
			Assert.Empty(created.Career.Stints);
			Assert.All(service.GetJobBoard(), job => Assert.True(job.InReach));

			JobListing pick = service.GetJobBoard()[0];
			service.SignContract(pick.OrganizationId);
			CareerState signed = service.LoadCareer();
			Assert.Equal(1, signed.Career.SeasonsCoached);
			Assert.Equal("1ST SEASON", signed.Career.SeasonLabel);
			Assert.Single(signed.Career.Stints);
			Assert.Equal(pick.SchoolName, signed.Career.Stints[0].SchoolName);
			Assert.Null(signed.Career.Stints[0].EndYear);
			Assert.False(signed.Organizations.First(org => org.Id == pick.OrganizationId).Profile.HasOpening);
			Assert.Empty(service.GetJobBoard());
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Decision_day_posts_every_schools_roster_and_honors_the_coachs_calls()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			service.CreateCareer(new CreateCareerRequest
			{
				Name = "Tryouts",
				Seed = 2026,
				UserOrganizationId = "cascade",
			});
			service.SetCurrentDate(SeasonCalendar.TryoutWeek(2026).Start);
			CareerState state = service.LoadCareer();
			List<Player> mine = state.TryoutCandidates("cascade").OrderByDescending(p => p.Overall).ToList();
			Assert.NotEmpty(mine);
			Player star = mine[0];
			Player cut = mine[^1];
			Assert.True(service.SetTryoutCall(star.Id, TryoutCall.Varsity));
			Assert.True(service.SetTryoutCall(cut.Id, TryoutCall.Cut));

			while (service.LoadCareer().Career.CurrentDate <= SeasonCalendar.TryoutDecisionDay(2026))
			{
				service.AdvanceDay();
			}

			CareerState loaded = service.LoadCareer();
			Assert.DoesNotContain(loaded.Players, p => p.IsTryoutCandidate);
			Player starAfter = loaded.PlayerById(star.Id)!;
			Assert.Equal("cascade", starAfter.OrganizationId);
			Assert.Equal(TeamLevel.Varsity, starAfter.TeamLevel);
			Player cutAfter = loaded.PlayerById(cut.Id)!;
			Assert.Equal(LeagueCatalog.InactiveTeamId, cutAfter.TeamId);
			Assert.True(loaded.Transactions.Count(t => t.Kind == PlayerTransactionKind.Tryout) >= 100);
			Assert.Contains(loaded.Transactions, t => t.Headline.Contains("after tryouts", StringComparison.Ordinal));
			Assert.All(loaded.Teams.Where(t => t.RosterCap > 0), team =>
				Assert.InRange(loaded.Players.Count(p => p.TeamId == team.Id), 9, team.RosterCap));
			Assert.Contains(loaded.News, story => story.Tag == NewsTag.Tryouts);
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Tryout_calls_survive_save()
	{
		string path = NewDb();
		try
		{
			Guid id;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest
				{
					Name = "Calls",
					Seed = 2026,
					UserOrganizationId = "cascade",
				});
				id = service.LoadCareer().TryoutCandidates("cascade").First().Id;
				Assert.True(service.SetTryoutCall(id, TryoutCall.JuniorVarsity));
			}

			using CareerService reopened = Open(path);
			CareerState loaded = reopened.LoadCareer();
			Assert.Equal(TryoutCall.JuniorVarsity, loaded.PlayerById(id)!.TryoutCall);
			Assert.True(loaded.PlayerById(id)!.IsTryoutCandidate);
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Program_archives_round_trip_through_sqlite()
	{
		string path = NewDb();
		try
		{
			int historyYears;
			int recordRows;
			string fingerprint;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Archives", Seed = 88 });
				CareerState state = service.LoadCareer();
				OrganizationState cascade = state.Organizations.Single(org => org.Id == "cascade");
				historyYears = cascade.SeasonHistory.Count;
				recordRows = cascade.RecordBook.Count;
				Assert.Equal(8, historyYears);
				Assert.True(recordRows >= ProgramRecordBook.Categories.Length);
				Assert.Equal(8, cascade.HallOfFame.Count);
				fingerprint = ArchiveFingerprint(state);
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal(fingerprint, ArchiveFingerprint(loaded));
				OrganizationState cascade = loaded.Organizations.Single(org => org.Id == "cascade");
				Assert.Equal(historyYears, cascade.SeasonHistory.Count);
				Assert.Equal(recordRows, cascade.RecordBook.Count);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Archiving_a_season_appends_history_and_can_promote_a_live_record()
	{
		string path = NewDb();
		try
		{
			Guid sluggerId;
			string sluggerName;
			int years;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Year End", Seed = 88, UserOrganizationId = "cascade" });
				CareerState before = service.LoadCareer();
				OrganizationState cascade = before.Organizations.Single(org => org.Id == "cascade");
				years = cascade.SeasonHistory.Count;
				TeamIdentity varsity = before.Teams.Single(team => team.OrganizationId == "cascade" && team.Level == TeamLevel.Varsity);
				Player slugger = before.Players.First(player => player.TeamId == varsity.Id && !player.IsPitcher);
				sluggerId = slugger.Id;
				sluggerName = slugger.FullName;
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState patched = store.Load();
				TeamIdentity varsity = patched.Teams.Single(team => team.OrganizationId == "cascade" && team.Level == TeamLevel.Varsity);
				patched.BattingStats.Add(new BattingSeasonStats
				{
					PlayerId = sluggerId,
					SeasonYear = 2026,
					TeamId = varsity.Id,
					Hits = 400,
					HomeRuns = 80,
					RunsBattedIn = 200,
					Doubles = 60,
				});
				store.ReplaceAll(patched);
			}

			using (CareerService service = Open(path))
			{
				service.ArchiveCompletedSeason(new ProgramSeasonResult
				{
					OrganizationId = "cascade",
					SeasonYear = 2026,
					Wins = 24,
					Losses = 8,
					RegularWins = 20,
					RegularLosses = 6,
					DistrictFinish = 1,
					Postseason = ProgramPostseason.StateChampion,
					WonDistrict = true,
					WonRegional = true,
					WonState = true,
				});

				CareerState after = service.LoadCareer();
				OrganizationState updated = after.Organizations.Single(org => org.Id == "cascade");
				Assert.Equal(years + 1, updated.SeasonHistory.Count);
				ProgramSeasonResult last = updated.SeasonHistory[^1];
				Assert.Equal(2026, last.SeasonYear);
				Assert.Equal(ProgramPostseason.StateChampion, last.Postseason);
				Assert.Equal(sluggerName, updated.RecordBook.Single(row =>
					row.Category == ProgramRecordCategory.Hits && row.Rank == 1).PlayerName);
				Assert.Contains(updated.HallOfFame, row => row.InductedYear == 2026);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Archiving_updates_ai_program_reputation_from_results()
	{
		string path = NewDb();
		try
		{
			int beforeRep;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Prestige", Seed = 88, UserOrganizationId = "cascade" });
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState patched = store.Load();
				OrganizationState lakes = patched.Organizations.Single(org => org.Id == "great-lakes");
				beforeRep = lakes.Profile.Reputation;
				TeamIdentity varsity = patched.Teams.Single(team => team.OrganizationId == "great-lakes" && team.Level == TeamLevel.Varsity);
				TeamSeasonRecord record = patched.RecordFor(varsity.Id);
				record.Wins = 22;
				record.Losses = 4;
				record.RunsFor = 140;
				record.RunsAgainst = 60;
				store.ReplaceAll(patched);
			}

			using (CareerService service = Open(path))
			{
				service.ArchiveCompletedSeason(new ProgramSeasonResult
				{
					OrganizationId = "cascade",
					SeasonYear = 2026,
					Wins = 13,
					Losses = 13,
					RegularWins = 13,
					RegularLosses = 13,
					DistrictFinish = 4,
				});
				CareerState after = service.LoadCareer();
				OrganizationState lakes = after.Organizations.Single(org => org.Id == "great-lakes");
				Assert.True(lakes.Profile.Reputation != beforeRep || lakes.Profile.LastSeasonWins == 22);
				Assert.Equal(22, lakes.Profile.LastSeasonWins);
				Assert.Equal(ProgramReputation.TalentBand(lakes.Profile.Reputation), lakes.TalentTier);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Offseason_carousel_posts_jobs_and_a_coach_can_change_schools()
	{
		string path = NewDb();
		try
		{
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Carousel", Seed = 2026 });
				JobListing first = service.GetJobBoard()[0];
				service.SignContract(first.OrganizationId);
				Assert.Empty(service.GetJobBoard());

				service.ArchiveCompletedSeason(new ProgramSeasonResult
				{
					OrganizationId = first.OrganizationId,
					SeasonYear = 2026,
					Wins = 18,
					Losses = 8,
					RegularWins = 18,
					RegularLosses = 8,
					DistrictFinish = 2,
					Postseason = ProgramPostseason.DistrictSemifinal,
					WonDistrict = false,
					WonRegional = false,
					WonState = false,
				});

				IReadOnlyList<JobListing> board = service.GetJobBoard();
				Assert.InRange(board.Count, 2, 3);
				Assert.DoesNotContain(board, job => job.OrganizationId == first.OrganizationId);
				Assert.All(board, job => Assert.False(string.IsNullOrWhiteSpace(job.VacancyLabel)));
				Assert.False(string.IsNullOrWhiteSpace(board[0].SchoolFullName));
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState state = store.Load();
				state.Career.Reputation = 90;
				store.ReplaceAll(state);
			}

			using (CareerService service = Open(path))
			{
				CareerState before = service.LoadCareer();
				string sitting = before.Career.UserOrganizationId;
				Guid[] playerIds = before.Players.Select(player => player.Id).OrderBy(id => id).ToArray();
				int playerCount = before.Players.Count;
				int gameCount = before.Games.Count;
				JobListing next = service.GetJobBoard().First(job => job.InReach);
				SignContractResult moved = service.ChangeJob(next.OrganizationId);
				Assert.Equal(next.OrganizationId, moved.OrganizationId);

				CareerState after = service.LoadCareer();
				Assert.Equal(next.OrganizationId, after.Career.UserOrganizationId);
				Assert.Equal(2, after.Career.Stints.Count);
				Assert.Equal(sitting, after.Career.Stints[0].OrganizationId);
				Assert.Equal(2026, after.Career.Stints[0].EndYear);
				Assert.Equal(18, after.Career.Stints[0].Wins);
				Assert.Equal(8, after.Career.Stints[0].Losses);
				Assert.Equal(2027, after.Career.Stints[1].StartYear);
				Assert.Null(after.Career.Stints[1].EndYear);
				Assert.Equal(playerCount, after.Players.Count);
				Assert.Equal(playerIds, after.Players.Select(player => player.Id).OrderBy(id => id).ToArray());
				Assert.Equal(gameCount, after.Games.Count);
				Assert.Single(after.Career.Seasons);
				Assert.Equal(sitting, after.Career.Seasons[0].OrganizationId);
				Assert.True(after.Organizations.First(org => org.Id == sitting).Profile.HasOpening);
				Assert.False(after.Organizations.First(org => org.Id == next.OrganizationId).Profile.HasOpening);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Inbound_job_offers_persist_and_accept_switches_schools()
	{
		string path = NewDb();
		try
		{
			string sitting;
			CoachingJobOffer offer;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Offers", Seed = 2026 });
				JobListing first = service.GetJobBoard()[0];
				service.SignContract(first.OrganizationId);
				sitting = first.OrganizationId;
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState state = store.Load();
				state.Career.Reputation = 90;
				Assert.True(state.Career.Contract.IsActive);
				store.ReplaceAll(state);
			}

			using (CareerService service = Open(path))
			{
				service.ArchiveCompletedSeason(new ProgramSeasonResult
				{
					OrganizationId = sitting,
					SeasonYear = 2026,
					Wins = 18,
					Losses = 8,
					RegularWins = 18,
					RegularLosses = 8,
					DistrictFinish = 2,
					Postseason = ProgramPostseason.DistrictSemifinal,
				});

				IReadOnlyList<CoachingJobOffer> pending = service.GetPendingJobOffers();
				Assert.InRange(pending.Count, 1, InboundJobOffers.MaxCount);
				Assert.All(pending, item =>
				{
					Assert.NotEqual(sitting, item.OrganizationId);
					Assert.True(CoachReputation.CanReach(90, item.Reputation));
					Assert.True(item.AnnualSalary > 0);
					Assert.InRange(item.TermYears, 2, 4);
				});
				offer = pending[0];
			}

			using (CareerService reopened = Open(path))
			{
				CoachingJobOffer loaded = Assert.Single(
					reopened.GetPendingJobOffers(),
					item => item.Id == offer.Id);
				Assert.Equal(offer.AnnualSalary, loaded.AnnualSalary);
				Assert.Equal(offer.TermYears, loaded.TermYears);

				SignContractResult moved = reopened.AcceptJobOffer(offer.Id);
				Assert.Equal(offer.OrganizationId, moved.OrganizationId);

				CareerState after = reopened.LoadCareer();
				Assert.Equal(offer.OrganizationId, after.Career.UserOrganizationId);
				Assert.Equal(offer.TermYears, after.Career.Contract.TermYears);
				Assert.Equal(offer.AnnualSalary, after.Career.Contract.AnnualSalary);
				Assert.Empty(after.Career.PendingJobOffers);
				Assert.True(after.Organizations.First(org => org.Id == sitting).Profile.HasOpening);
				Assert.False(after.Organizations.First(org => org.Id == offer.OrganizationId).Profile.HasOpening);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Declining_a_job_offer_leaves_the_sitting_chair()
	{
		string path = NewDb();
		try
		{
			string sitting;
			Guid offerId;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Decline", Seed = 77 });
				JobListing first = service.GetJobBoard()[0];
				service.SignContract(first.OrganizationId);
				sitting = first.OrganizationId;
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState state = store.Load();
				state.Career.Reputation = 90;
				store.ReplaceAll(state);
			}

			using (CareerService service = Open(path))
			{
				service.ArchiveCompletedSeason(new ProgramSeasonResult
				{
					OrganizationId = sitting,
					SeasonYear = 2026,
					Wins = 16,
					Losses = 10,
					RegularWins = 16,
					RegularLosses = 10,
					DistrictFinish = 3,
					Postseason = ProgramPostseason.DistrictQuarterfinal,
				});

				CoachingJobOffer offer = service.GetPendingJobOffers()[0];
				offerId = offer.Id;
				Assert.True(service.DeclineJobOffer(offer.Id));
			}

			using (CareerService reopened = Open(path))
			{
				CareerState after = reopened.LoadCareer();
				Assert.Equal(sitting, after.Career.UserOrganizationId);
				CoachingJobOffer declined = Assert.Single(after.Career.JobOffers, item => item.Id == offerId);
				Assert.Equal(CoachingJobOfferStatus.Declined, declined.Status);
				Assert.DoesNotContain(after.Career.PendingJobOffers, item => item.Id == offerId);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Program_profiles_round_trip_through_sqlite()
	{
		string path = NewDb();
		try
		{
			string fingerprint;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Profiles", Seed = 88 });
				fingerprint = ProfileFingerprint(service.LoadCareer());
			}

			using (CareerService reopened = Open(path))
			{
				Assert.Equal(fingerprint, ProfileFingerprint(reopened.LoadCareer()));
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Player_appearance_physical_and_personality_round_trip()
	{
		string path = NewDb();
		try
		{
			Guid playerId;
			PersonAppearance appearance;
			PlayerPhysical physical;
			PlayerTemperament personality;
			int potential;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Look", Seed = 91 });
				CareerState state = service.LoadCareer();
				Player sample = state.Players.First(p => !p.IsUnsigned);
				playerId = sample.Id;
				appearance = sample.Appearance.Clone();
				physical = sample.Physical;
				personality = sample.Personality;
				potential = sample.Potential;
			}

			using (CareerService reopened = Open(path))
			{
				Player loaded = reopened.LoadCareer().Players.Single(p => p.Id == playerId);
				Assert.Equal(appearance, loaded.Appearance);
				Assert.Equal(physical, loaded.Physical);
				Assert.Equal(personality, loaded.Personality);
				Assert.Equal(potential, loaded.Potential);
				Assert.Equal(PlayerOverall.Calculate(loaded.PrimaryPosition, loaded.Ratings), loaded.Overall);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Assign_player_level_persists_varsity_move()
	{
		string path = NewDb();
		try
		{
			using CareerService service = Open(path);
			service.CreateCareer(new CreateCareerRequest
			{
				Name = "Move",
				Seed = 44,
				UserOrganizationId = "cascade",
			});
			CareerState state = service.LoadCareer();
			Player? jv = state.Players.FirstOrDefault(p =>
				p.OrganizationId == "cascade" && p.TeamLevel == TeamLevel.JuniorVarsity);
			Assert.NotNull(jv);
			Assert.True(service.TryAssignPlayerLevel(jv.Id, TeamLevel.Varsity, out string? error));
			Assert.Null(error);
			Player moved = service.LoadCareer().PlayerById(jv.Id)!;
			Assert.Equal(TeamLevel.Varsity, moved.TeamLevel);
			Assert.Equal("cascade-varsity", moved.TeamId);
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Postseason_awards_and_history_round_trip()
	{
		string path = NewDb();
		try
		{
			int year;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Archive", Seed = 31 });
				year = service.LoadCareer().Career.CurrentSeasonYear;
			}

			using (SqliteCareerStore store = new(path))
			{
				CareerState state = store.Load();
				state.World.Postseason = new PostseasonState
				{
					SeasonYear = year,
					StateChampionTeamId = "cascade-varsity",
					Qualifiers =
					[
						new PostseasonSeed
						{
							TeamId = "cascade-varsity",
							OrganizationId = "cascade",
							Name = "Cascade",
							DistrictId = "great-lakes",
							Region = "East",
							Seed = 1,
							DistrictFinish = 1,
						},
					],
				};
				state.World.Awards.Add(new SeasonAwards
				{
					Scope = AwardScope.State,
					SeasonYear = year,
					Headline = "2026 STATE AWARDS",
					Official = true,
				});
				state.World.HistoricalSeasons.Add(new HistoricalSeason
				{
					SeasonYear = year,
					Champions =
					[
						new HistoricalChampion { Title = "State", TeamId = "cascade-varsity", OrganizationId = "cascade", Name = "Cascade" },
					],
				});
				state.World.SeasonProfileCommits.Add($"cascade:{year}");
				store.ReplaceAll(state);
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal("cascade-varsity", loaded.World.Postseason?.StateChampionTeamId);
				Assert.Single(loaded.World.Awards);
				Assert.Equal(AwardScope.State, loaded.World.Awards[0].Scope);
				Assert.Single(loaded.World.HistoricalSeasons);
				Assert.Equal("State", loaded.World.HistoricalSeasons[0].Champions[0].Title);
				Assert.Contains($"cascade:{year}", loaded.World.SeasonProfileCommits);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	private static CareerService Open(string path) => CareerService.Create(new SqliteCareerStore(path));

	private static string NewDb() =>
		Path.Combine(Path.GetTempPath(), $"hsbm-test-{Guid.NewGuid():N}.db");

	private static void TryDelete(string path)
	{
		foreach (string file in new[] { path, path + "-wal", path + "-shm" })
		{
			try
			{
				if (File.Exists(file))
				{
					File.Delete(file);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	private static string ArchiveFingerprint(CareerState state) =>
		string.Join("|", state.Organizations
			.OrderBy(o => o.Id)
			.Select(o =>
				$"{o.Id}:{string.Join(",", o.SeasonHistory.Select(s => $"{s.SeasonYear}-{s.Wins}-{s.DistrictFinish}-{s.Postseason}"))}:{string.Join(",", o.RecordBook.Select(r => $"{r.Category}:{r.Rank}:{r.PlayerName}:{r.Value}"))}:{string.Join(",", o.HallOfFame.Select(h => $"{h.InductedYear}:{h.PlayerName}:{h.Position}"))}"));

	private static string Fingerprint(CareerState state) =>
		string.Join("|", state.Players
			.OrderBy(p => p.Id)
			.Select(p => $"{p.Id:N}:{p.FullName}:{p.TeamId}:{p.Ratings.Contact}:{p.Ratings.Stuff}"));

	private static string ProfileFingerprint(CareerState state) =>
		string.Join("|", state.Organizations
			.OrderBy(o => o.Id)
			.Select(o => $"{o.Id}:{o.Profile.Reputation}:{o.Profile.Facilities}:{o.Profile.BaseballResources}:{o.Profile.LastSeasonWins}:{o.Profile.DistrictFinish}:{o.Profile.TargetExpectation}:{o.Profile.BaseballBudget}:{o.Profile.HasOpening}:{o.Profile.VacancyReason}:{o.HeadCoach?.FullName}"));
}
