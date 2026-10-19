using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Shared pieces of the League pages (concepts 12–16): a team badge (logo or
/// team-color monogram), the race pill, and the compact district standings
/// card used by the Standings grid and the Districts directory.
/// </summary>
public static class LeagueUi
{
	/// <summary>School logo when it has art, otherwise a team-color ring with initials.</summary>
	public static Control TeamBadge(string teamId, string name, float size)
	{
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, size);
		if (logo != null)
		{
			return logo;
		}

		(Color primary, Color secondary) = TeamColors.For(teamId);
		var ring = new PanelContainer
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ring.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(primary.Darkened(0.55f), 1f), (int)(size / 2), 0, 0, secondary.Lerp(UiKit.Text, 0.25f), 1));
		Label initials = UiKit.Text_(Initials(name), UiKit.Bold, Mathf.Max(9, (int)(size * 0.42f)), UiKit.Text, HorizontalAlignment.Center);
		initials.VerticalAlignment = VerticalAlignment.Center;
		ring.AddChild(initials);
		return ring;
	}

	public static string Initials(string name)
	{
		string[] words = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
		{
			return "?";
		}

		return words.Length == 1
			? words[0][..1].ToUpperInvariant()
			: (words[0][..1] + words[1][..1]).ToUpperInvariant();
	}

	/// <summary>Small caps pill sized for 22px table rows.</summary>
	public static PanelContainer SmallPill(string text, UiKit.Tone tone)
	{
		(Color bg, Color fg) = tone switch
		{
			UiKit.Tone.Good => (new Color(UiKit.Green, 0.13f), UiKit.Green),
			UiKit.Tone.Warn => (new Color(UiKit.Gold, 0.16f), UiKit.Gold),
			UiKit.Tone.Bad => (new Color(UiKit.Red, 0.15f), new Color("#ff7a6b")),
			_ => (new Color("#1d2130"), UiKit.Muted),
		};
		var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		pill.AddThemeStyleboxOverride("panel", UiKit.Box(bg, 10, 8, 0));
		pill.AddChild(UiKit.Text_(text, UiKit.Semibold, 11, fg, HorizontalAlignment.Center));
		return pill;
	}

	/// <summary>
	/// CLINCHED / IN / HUNT / OUT from the qualification race. Before anyone
	/// has played, every team is still hunting.
	/// </summary>
	public static PanelContainer RacePill(HubStandingRow row, bool seasonStarted)
	{
		if (!seasonStarted)
		{
			return SmallPill("HUNT", UiKit.Tone.Warn);
		}

		return row.Race switch
		{
			RaceStatus.Clinched => SmallPill("CLINCHED", UiKit.Tone.Good),
			RaceStatus.Eliminated => SmallPill("OUT", UiKit.Tone.Bad),
			_ => row.Rank <= LeagueStandings.QualifiersPerDistrict ? SmallPill("IN", UiKit.Tone.Good) : SmallPill("HUNT", UiKit.Tone.Warn),
		};
	}

	public static bool SeasonStarted(List<HubStandingRow> rows)
	{
		foreach (HubStandingRow row in rows)
		{
			if (row.Wins + row.Losses > 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Table row container: hairline below, gold left edge and raised fill for the user's team.</summary>
	public static PanelContainer RowPanel(bool highlight, float padY = 3)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var box = new StyleBoxFlat
		{
			BgColor = highlight ? UiKit.Selected : Colors.Transparent,
			BorderColor = highlight ? UiKit.Gold : UiKit.Line,
			BorderWidthBottom = 1,
			BorderWidthLeft = highlight ? 2 : 0,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = padY,
			ContentMarginBottom = padY,
		};
		panel.AddThemeStyleboxOverride("panel", box);
		return panel;
	}

	/// <summary>Muted caps column header row.</summary>
	public static HBoxContainer ColumnHeader(params (string Text, float Width, HorizontalAlignment Align)[] columns)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		foreach ((string text, float width, HorizontalAlignment align) in columns)
		{
			row.AddChild(UiKit.Cell(text, width, UiKit.Semibold, 11, UiKit.Muted, align));
		}

		return row;
	}

	/// <summary>
	/// One district's table: # · program · W-L · GB · STRK · RACE, the user's
	/// school highlighted. <paramref name="onFull"/> wires the "FULL →" link.
	/// </summary>
	public static PanelContainer StandingsCard(string districtId, TeamLevel level, string userTeamId, System.Action? onFull, int rowSize = 13)
	{
		HubDistrict district = DistrictHubData.GetDistrict(districtId);
		List<HubStandingRow> rows = DistrictHubData.StandingsFor(districtId, level);
		bool started = SeasonStarted(rows);

		PanelContainer card = UiKit.MakeCard(16, 8);
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		card.AddChild(col);

		string title = $"{district.Name.ToUpperInvariant()}  ·  {district.Region.ToUpperInvariant()}";
		col.AddChild(UiKit.CardHeader(title, onFull == null ? null : "FULL", onFull));
		var header = new MarginContainer();
		header.AddThemeConstantOverride("margin_left", 8);
		header.AddThemeConstantOverride("margin_right", 8);
		header.AddThemeConstantOverride("margin_bottom", 2);
		header.AddChild(ColumnHeader(
			("#", 22, HorizontalAlignment.Right),
			("PROGRAM", 0, HorizontalAlignment.Left),
			("W-L", 52, HorizontalAlignment.Center),
			("GB", 36, HorizontalAlignment.Center),
			("STRK", 40, HorizontalAlignment.Center),
			("RACE", 74, HorizontalAlignment.Left)));
		col.AddChild(header);
		col.AddChild(UiKit.Hairline());

		foreach (HubStandingRow row in rows)
		{
			bool user = row.Team.Id == userTeamId;
			PanelContainer panel = RowPanel(user, rowSize >= 15 ? 4 : 1);
			SchoolLinks.MakeClickable(panel, row.Team.Id);
			var cells = new HBoxContainer();
			cells.AddThemeConstantOverride("separation", 10);
			cells.AddChild(UiKit.Cell(row.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture), 22, UiKit.Semibold, rowSize, UiKit.Muted, HorizontalAlignment.Right));
			var identity = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			identity.AddThemeConstantOverride("separation", 8);
			identity.AddChild(TeamBadge(row.Team.Id, row.Team.Name, rowSize + 4));
			identity.AddChild(UiKit.Cell(row.Team.Name.ToUpperInvariant(), 0, UiKit.Bold, rowSize, user ? UiKit.Gold : UiKit.Text));
			cells.AddChild(identity);
			cells.AddChild(UiKit.Cell($"{row.Wins}-{row.Losses}", 52, UiKit.Bold, rowSize, UiKit.Text, HorizontalAlignment.Center));
			cells.AddChild(UiKit.Cell(row.GamesBack, 36, UiKit.Medium, rowSize, UiKit.Muted, HorizontalAlignment.Center));
			Color streak = row.Streak.StartsWith('W') ? UiKit.Green : row.Streak.StartsWith('L') ? UiKit.Red : UiKit.Muted;
			cells.AddChild(UiKit.Cell(string.IsNullOrEmpty(row.Streak) ? "–" : row.Streak, 40, UiKit.Bold, rowSize, streak, HorizontalAlignment.Center));
			var race = new HBoxContainer { CustomMinimumSize = new Vector2(74, 0) };
			race.AddChild(RacePill(row, started));
			cells.AddChild(race);
			panel.AddChild(cells);
			col.AddChild(panel);
		}

		return card;
	}

	/// <summary>Walk up to the navigator (pages live under the shell).</summary>
	public static AppNavigator? Navigator(Node from)
	{
		Node? node = from;
		while (node != null)
		{
			if (node is AppNavigator navigator)
			{
				return navigator;
			}

			node = node.GetParent();
		}

		return null;
	}
}
