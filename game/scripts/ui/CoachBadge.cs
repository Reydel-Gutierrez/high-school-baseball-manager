using HSBM.Domain;
using Godot;

/// <summary>
/// Top-bar identity chip: the user's head coach (engine headshot in the
/// school cap and jersey), name and job. Clicking it opens My Career.
/// </summary>
public partial class CoachBadge : PanelContainer
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);

	private readonly System.Action _onPressed;
	private GameSession? _session;
	private PersonPortrait _portrait = null!;
	private Label _name = null!;
	private Label _role = null!;
	private StyleBoxFlat _normal = null!;
	private StyleBoxFlat _hover = null!;

	public CoachBadge()
		: this(() => { })
	{
	}

	public CoachBadge(System.Action onPressed)
	{
		_onPressed = onPressed;
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		TooltipText = "My Career";
	}

	public override void _Ready()
	{
		_normal = Style(new Color(0, 0, 0, 0));
		_hover = Style(HoverBg);
		AddThemeStyleboxOverride("panel", _normal);

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		AddChild(row);

		_portrait = new PersonPortrait
		{
			CustomMinimumSize = new Vector2(38, 38),
			Circle = true,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddChild(_portrait);

		var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		text.AddThemeConstantOverride("separation", -3);
		_name = Text(GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf"), 14, TextPrimary);
		_role = Text(GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf"), 10, TextMuted);
		text.AddChild(_name);
		text.AddChild(_role);
		row.AddChild(text);

		MouseEntered += () => AddThemeStyleboxOverride("panel", _hover);
		MouseExited += () => AddThemeStyleboxOverride("panel", _normal);
		GuiInput += OnGuiInput;

		_session = GameSession.Current;
		if (_session != null)
		{
			_session.CareerChanged += Refresh;
			_session.ContractStateChanged += Refresh;
		}

		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.CareerChanged -= Refresh;
			_session.ContractStateChanged -= Refresh;
		}
	}

	private void OnGuiInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			AcceptEvent();
			_onPressed();
		}
	}

	public void Refresh()
	{
		if (_session == null || _portrait == null)
		{
			return;
		}

		CoachProfile coach = _session.Coach;
		string first = coach.FirstName ?? "";
		string last = coach.LastName ?? "";
		_name.Text = (first.Length > 0 ? $"{first[0]}. {last}" : last).ToUpperInvariant();

		bool hired = _session.HasSignedContract;
		string org = _session.CatalogOrganizationId;
		_role.Text = hired ? $"HEAD COACH  ·  {TeamBrands.ShortName(org).ToUpperInvariant()}" : "FREE AGENT COACH";

		_portrait.Age = coach.Age > 0 ? coach.Age : 40;
		if (hired && !string.IsNullOrWhiteSpace(org))
		{
			(Color jersey, Color trim) = TeamColors.For(org);
			_portrait.Jersey = jersey;
			_portrait.Trim = trim;
			_portrait.Cap = true;
			_portrait.Monogram = ProgramMark.CapMonogram(org);
			_portrait.Ring = trim;
			_portrait.BackdropColor = jersey.Darkened(0.4f);
		}
		else
		{
			_portrait.Jersey = new Color(0.16f, 0.18f, 0.22f);
			_portrait.Trim = new Color(0.85f, 0.85f, 0.88f);
			_portrait.Cap = false;
			_portrait.Monogram = "";
			_portrait.Ring = TextMuted;
		}

		_portrait.SetAppearance(coach.Appearance);
	}

	private static Label Text(FontFile font, int size, Color color)
	{
		var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat Style(Color bg) => new()
	{
		BgColor = bg,
		ContentMarginLeft = 6,
		ContentMarginRight = 10,
		ContentMarginTop = 3,
		ContentMarginBottom = 3,
		CornerRadiusTopLeft = 22,
		CornerRadiusTopRight = 6,
		CornerRadiusBottomLeft = 22,
		CornerRadiusBottomRight = 6,
	};
}
