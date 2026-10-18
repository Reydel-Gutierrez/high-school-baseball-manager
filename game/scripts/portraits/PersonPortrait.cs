using HSBM.Domain;
using Godot;

/// <summary>
/// Reusable portrait view. Bind an appearance (plus age and build when
/// known) and it shows a rendered 3D headshot; the flat layered drawing
/// fills in for the frame or two before the studio delivers.
/// </summary>
public partial class PersonPortrait : Control
{
	private static readonly IPortraitRenderer Fallback = new CompositePortraitRenderer();
	private PersonAppearance _appearance = new();
	private Texture2D? _photo;
	private string _requested = "";

	public PersonPortrait()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(96, 120);
	}

	/// <summary>Age drives the head's maturity (15-year-old vs 50-year-old coach).</summary>
	public int Age { get; set; } = 17;

	public int HeightInches { get; set; }

	public int WeightLbs { get; set; }

	/// <summary>Jersey color override (else the appearance's team color).</summary>
	public Color? Jersey { get; set; }

	public Color? Trim { get; set; }

	/// <summary>Draw a soft studio backdrop behind the cut-out headshot.</summary>
	public bool Backdrop { get; set; } = true;

	public Color BackdropColor { get; set; } = new(0.11f, 0.13f, 0.17f);

	public PersonAppearance Appearance
	{
		get => _appearance;
		set => SetAppearance(value);
	}

	public void SetAppearance(PersonAppearance appearance)
	{
		_appearance = appearance?.Clone() ?? new PersonAppearance();
		Refresh();
	}

	/// <summary>Bind a player: appearance, age, height and weight in one call.</summary>
	public void SetPlayer(Player player, Color? jersey = null, Color? trim = null)
	{
		Age = player.Age > 0 ? player.Age : player.Grade.TypicalAge();
		HeightInches = player.Physical.HeightInches;
		WeightLbs = player.Physical.WeightLbs;
		Jersey = jersey;
		Trim = trim;
		SetAppearance(player.Appearance);
	}

	public void Refresh()
	{
		FaceRecipe recipe = FaceRecipe.From(_appearance, Age, HeightInches, WeightLbs, Jersey, Trim);
		if (recipe.Key == _requested && _photo != null)
		{
			return;
		}

		_requested = recipe.Key;
		_photo = null;
		HeadshotStudio? studio = IsInsideTree() ? HeadshotStudio.Get(this) : null;
		if (studio != null)
		{
			string key = recipe.Key;
			studio.Request(recipe, tex =>
			{
				if (IsInstanceValid(this) && key == _requested)
				{
					_photo = tex;
					QueueRedraw();
				}
			});
		}

		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
		else if (what == NotificationEnterTree && _photo == null)
		{
			_requested = "";
			Callable.From(Refresh).CallDeferred();
		}
	}

	public override void _Draw()
	{
		var bounds = new Rect2(Vector2.Zero, Size);
		if (_photo == null)
		{
			Fallback.Draw(this, _appearance, bounds);
			return;
		}

		if (Backdrop)
		{
			DrawBackdrop(bounds);
		}

		// Fit the 4:5 headshot to the frame height (wide frames show it centred
		// on the backdrop); narrow frames crop the shoulders, never the head.
		Vector2 tex = _photo.GetSize();
		float scale = bounds.Size.X / bounds.Size.Y > tex.X / tex.Y
			? bounds.Size.Y / tex.Y
			: Mathf.Max(bounds.Size.X / tex.X, bounds.Size.Y / tex.Y);
		Vector2 drawn = tex * scale;
		var dest = new Rect2(new Vector2((bounds.Size.X - drawn.X) / 2f, 0f), drawn);
		DrawTextureRect(_photo, dest, false);
	}

	private void DrawBackdrop(Rect2 bounds)
	{
		Color top = BackdropColor.Lightened(0.10f);
		Color bottom = BackdropColor.Darkened(0.35f);
		const int bands = 16;
		for (int i = 0; i < bands; i++)
		{
			float t0 = i / (float)bands;
			float t1 = (i + 1) / (float)bands;
			DrawRect(new Rect2(0, bounds.Size.Y * t0, bounds.Size.X, bounds.Size.Y * (t1 - t0) + 1), top.Lerp(bottom, t0), true);
		}

		// soft key-light glow behind the head
		Vector2 c = new(bounds.Size.X * 0.55f, bounds.Size.Y * 0.30f);
		float r = Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.55f;
		for (int i = 6; i >= 1; i--)
		{
			DrawCircle(c, r * i / 6f, new Color(1, 1, 1, 0.018f));
		}
	}
}
