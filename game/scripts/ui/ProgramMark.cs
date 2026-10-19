using Godot;

/// <summary>
/// School crest for page headers. Shows the logo when one exists, otherwise
/// a monogram badge, and re-resolves whenever the user's career changes.
/// Pass an organization id to pin a specific school instead of the user's.
/// </summary>
public partial class ProgramMark : Control
{
	private static readonly Color BadgeFill = new(0.13f, 0.15f, 0.19f);
	private static readonly Color BadgeRing = new(0.96f, 0.65f, 0.14f);
	private static readonly Color BadgeText = new(0.96f, 0.65f, 0.14f);

	private readonly float _size;
	private readonly string? _pinnedOrganizationId;
	private Texture2D? _logo;
	private string _monogram = "";
	private Font? _font;

	public ProgramMark()
		: this(84)
	{
	}

	public ProgramMark(float size, string? organizationId = null)
	{
		_size = size;
		_pinnedOrganizationId = organizationId;
		CustomMinimumSize = new Vector2(size, size);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public string OrganizationId => _pinnedOrganizationId ?? UserProgram.OrganizationId;

	public override void _EnterTree()
	{
		_font ??= GD.Load<Font>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		if (GameSession.Current != null && _pinnedOrganizationId == null)
		{
			GameSession.Current.CareerChanged += Refresh;
			GameSession.Current.ContractStateChanged += Refresh;
		}

		Refresh();
	}

	public override void _ExitTree()
	{
		if (GameSession.Current != null && _pinnedOrganizationId == null)
		{
			GameSession.Current.CareerChanged -= Refresh;
			GameSession.Current.ContractStateChanged -= Refresh;
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged && IsVisibleInTree())
		{
			Refresh();
		}
	}

	public void Refresh()
	{
		string id = OrganizationId;
		_logo = TeamLogos.Load(id);
		_monogram = Monogram(id);
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 box = Size;
		float side = Mathf.Min(box.X, box.Y);
		if (side <= 0)
		{
			side = _size;
		}

		var origin = new Vector2((box.X - side) / 2f, (box.Y - side) / 2f);
		if (_logo != null)
		{
			Vector2 tex = _logo.GetSize();
			float scale = Mathf.Min(side / tex.X, side / tex.Y);
			Vector2 drawn = tex * scale;
			DrawTextureRect(_logo, new Rect2(origin + (new Vector2(side, side) - drawn) / 2f, drawn), false);
			return;
		}

		Vector2 center = origin + new Vector2(side / 2f, side / 2f);
		float radius = side * 0.46f;
		HSBM.Domain.SchoolBrand? brand = TeamBrands.For(OrganizationId);
		Color fill = brand != null ? new Color(brand.PrimaryHex) : BadgeFill;
		Color ring = brand != null ? new Color(brand.SecondaryHex) : BadgeRing;
		DrawCircle(center, radius, fill);
		DrawArc(center, radius, 0, Mathf.Tau, 64, ring, Mathf.Max(1.5f, side * 0.03f), true);
		if (_font == null || string.IsNullOrEmpty(_monogram))
		{
			return;
		}

		int fontSize = (int)(side * (_monogram.Length > 2 ? 0.30f : 0.38f));
		Vector2 textSize = _font.GetStringSize(_monogram, HorizontalAlignment.Left, -1, fontSize);
		var baseline = new Vector2(center.X - textSize.X / 2f, center.Y + fontSize * 0.36f);
		DrawString(_font, baseline, _monogram, HorizontalAlignment.Left, -1, fontSize, brand != null ? ring : BadgeText);
	}

	/// <summary>Cap-front letters: one or two, like a big-league cap ("N", "DV").</summary>
	public static string CapMonogram(string organizationId)
	{
		if (TeamBrands.For(organizationId) is { } brand)
		{
			return brand.CapMonogram;
		}

		string m = Monogram(organizationId);
		return m.Length >= 3 ? m[..1] : m;
	}

	public static string Monogram(string organizationId)
	{
		string name;
		try
		{
			name = HSBM.Domain.LeagueCatalog.Standard.GetOrganization(organizationId).ShortName;
		}
		catch (System.Exception)
		{
			name = organizationId;
		}

		string[] words = name.Split(new[] { ' ', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
		if (words.Length >= 2)
		{
			return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
		}

		return name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
	}
}
