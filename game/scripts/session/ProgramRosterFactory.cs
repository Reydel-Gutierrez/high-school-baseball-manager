using System;
using System.Collections.Generic;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Builds the prototype final-roster board and open pool from the Cascade
/// clubhouse and tryout camp. Replace with career save records later.
/// </summary>
public static class ProgramRosterFactory
{
	public static FinalRosterBoard CreateBoard() => CreateReturningBoard();

	public static FinalRosterBoard CreateReturningBoard()
	{
		var board = new FinalRosterBoard();
		var players = new List<FinalRosterPlayer>();
		foreach (ClubhousePlayer player in ClubhouseSquad.Players)
		{
			if (string.Equals(player.Year, "FR", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			players.Add(FromClubhouse(player));
		}

		board.Seed(players);
		return board;
	}

	public static FinalRosterBoard FromPlayers(
		IReadOnlyList<Player> returning,
		RecruitingMarket? market = null,
		string? organizationId = null)
	{
		var board = new FinalRosterBoard();
		var players = new List<FinalRosterPlayer>(returning.Count + 8);
		foreach (Player player in returning)
		{
			players.Add(FromIncoming(
				player,
				player.TeamLevel == TeamLevel.JuniorVarsity
					? RosterAssignment.JuniorVarsity
					: RosterAssignment.Varsity,
				fromOpenPlay: false));
		}

		if (market != null && !string.IsNullOrEmpty(organizationId))
		{
			foreach (PlayerAgreement agreement in market.Agreements)
			{
				if (agreement.OrganizationId != organizationId)
				{
					continue;
				}

				Player? signed = null;
				foreach (Player prospect in market.Prospects)
				{
					if (prospect.Id == agreement.PlayerId)
					{
						signed = prospect;
						break;
					}
				}

				if (signed == null)
				{
					continue;
				}

				bool exists = false;
				foreach (FinalRosterPlayer existing in players)
				{
					if (existing.Id == signed.Id)
					{
						exists = true;
						break;
					}
				}

				if (!exists)
				{
					players.Add(FromIncoming(
						signed,
						signed.TeamLevel == TeamLevel.JuniorVarsity
							? RosterAssignment.JuniorVarsity
							: RosterAssignment.Varsity));
				}
			}
		}

		board.Seed(players);
		return board;
	}

	public static OpenPlayerPool CreatePool(TryoutCamp camp, FinalRosterBoard board)
	{
		var pool = new OpenPlayerPool();
		var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (FinalRosterPlayer player in board.Players)
		{
			taken.Add(player.FullName);
		}

		var unsigned = new List<TryoutProspect>();
		foreach (TryoutProspect prospect in camp.Prospects)
		{
			if (taken.Contains(prospect.FullName) || prospect.Decision != TryoutDecision.Pending)
			{
				continue;
			}

			unsigned.Add(prospect);
		}

		unsigned.Sort((a, b) =>
		{
			int grade = a.CompositeGrade().CompareTo(b.CompositeGrade());
			return grade != 0 ? grade : a.TrueOverall.CompareTo(b.TrueOverall);
		});

		int take = Math.Min(10, unsigned.Count);
		for (int i = 0; i < take; i++)
		{
			pool.TryAdd(OpenPoolPlayer.FromProspect(unsigned[i], OpenPoolSource.MissedDeadline));
		}

		return pool;
	}

	public static FinalRosterPlayer FromProspect(TryoutProspect prospect, RosterAssignment assignment) =>
		new()
		{
			Id = prospect.Id,
			FirstName = prospect.FirstName,
			LastName = prospect.LastName,
			Position = prospect.Position,
			Throws = prospect.Throws,
			Bats = prospect.Bats,
			Year = prospect.Year,
			Overall = prospect.TrueOverall,
			Potential = prospect.TruePotential,
			FromTryout = true,
			Assignment = assignment,
		};

	public static FinalRosterPlayer FromIncoming(Player player, RosterAssignment assignment, bool fromOpenPlay = true) =>
		new()
		{
			Id = player.Id,
			FirstName = player.FirstName,
			LastName = player.LastName,
			Position = player.PrimaryPosition,
			Throws = player.Throws,
			Bats = player.Bats,
			Year = player.Grade,
			Overall = player.Overall,
			Potential = player.Potential,
			Jersey = player.JerseyNumber,
			FromTryout = fromOpenPlay,
			Assignment = assignment,
		};

	public static List<Player> ToPlayers(
		FinalRosterBoard board,
		string organizationId,
		TeamIdentity varsity,
		TeamIdentity jv)
	{
		var players = new List<Player>();
		foreach (FinalRosterPlayer player in board.Players)
		{
			TeamIdentity team = player.Assignment == RosterAssignment.JuniorVarsity ? jv : varsity;
			int bump = player.Potential - player.Overall;
			players.Add(new Player
			{
				Id = player.Id,
				FirstName = player.FirstName,
				LastName = player.LastName,
				GeneratedSeasonYear = GameSession.SeasonYear,
				Bats = player.Bats,
				Throws = player.Throws,
				PrimaryPosition = player.Position,
				Grade = player.Year,
				Age = player.Year.TypicalAge(),
				OrganizationId = organizationId,
				TeamId = team.Id,
				TeamLevel = team.Level,
				JerseyNumber = player.Jersey,
				Ratings = RatingsFromOverall(player.Overall, player.Position),
				Potential = player.Potential,
				Physical = PlayerPhysical.Typical(player.Position),
				Personality = PlayerTemperament.Composed,
				Appearance = PersonAppearanceGenerator.GeneratePlayer(
					player.Id.GetHashCode(),
					BodyBuild.Average),
				Priorities = RecruitingPriorities.Balanced,
			});
			_ = bump;
		}

		return players;
	}

	private static PlayerRatings RatingsFromOverall(int overall, PlayerPosition position)
	{
		int hit = position.IsPitcher() ? Math.Max(25, overall - 22) : overall;
		int pitch = position.IsPitcher() ? overall : Math.Max(25, overall - 22);
		return PlayerRatings.Clamp(hit, hit, hit, hit, hit, hit, pitch, pitch, pitch, pitch);
	}

	public static Guid StableId(string firstName, string lastName)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in $"{firstName}|{lastName}".ToUpperInvariant())
			{
				hash = (hash * 31) + c;
			}

			byte[] bytes = new byte[16];
			BitConverter.GetBytes(hash).CopyTo(bytes, 0);
			BitConverter.GetBytes(hash ^ 0x5bd1e995).CopyTo(bytes, 4);
			BitConverter.GetBytes((firstName.Length * 7919) + lastName.Length).CopyTo(bytes, 8);
			BitConverter.GetBytes(hash * 37).CopyTo(bytes, 12);
			return new Guid(bytes);
		}
	}

	private static FinalRosterPlayer FromClubhouse(ClubhousePlayer player)
	{
		HandednessExtensions.ParseBatsThrows(player.BatsThrows, out BattingSide bats, out ThrowingHand throws);
		int overall = player.Overall;
		int bump = player.Year.ToUpperInvariant() switch
		{
			"FR" => 12,
			"SO" => 8,
			"JR" => 4,
			_ => 2,
		};

		return new FinalRosterPlayer
		{
			Id = StableId(player.FirstName, player.LastName),
			FirstName = player.FirstName,
			LastName = player.LastName,
			Position = PlayerPositionExtensions.ParseLabel(player.Position),
			Throws = throws,
			Bats = bats,
			Year = PlayerGradeExtensions.FromAbbrev(player.Year),
			Overall = overall,
			Potential = Math.Min(99, overall + bump),
			Jersey = player.Jersey,
			FromTryout = false,
			Assignment = TeamLevel.JuniorVarsity.MatchesSquad(player.Squad)
				? RosterAssignment.JuniorVarsity
				: RosterAssignment.Varsity,
		};
	}
}
