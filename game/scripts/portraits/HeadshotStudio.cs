using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Off-screen photo studio. Builds each person's 3D head from a
/// <see cref="FaceRecipe"/>, renders one frame into a transparent
/// SubViewport, and hands back a texture. Results are cached in memory
/// and on disk (user://headshots), so a face is rendered once per look.
/// </summary>
public partial class HeadshotStudio : Node
{
	public const int Width = 400;
	public const int Height = 500;
	private const int MemoryCacheLimit = 400;
	private const string DiskDir = "user://headshots";

	private static HeadshotStudio? _instance;

	private readonly Dictionary<string, Texture2D> _cache = new(StringComparer.Ordinal);
	private readonly LinkedList<string> _lru = new();
	private readonly Dictionary<string, List<Action<Texture2D>>> _waiting = new(StringComparer.Ordinal);
	private readonly Queue<FaceRecipe> _queue = new();
	private (FaceRecipe Recipe, Action<Texture2D> Done)? _live;
	private Camera3D _camera = null!;

	private SubViewport _viewport = null!;
	private MeshInstance3D _head = null!;
	private MeshInstance3D _eyeL = null!;
	private MeshInstance3D _eyeR = null!;
	private ShaderMaterial _skin = null!;
	private ShaderMaterial _eyeMatL = null!;
	private ShaderMaterial _eyeMatR = null!;
	private const int ShellCount = 16;
	private readonly List<MeshInstance3D> _shells = new();
	private readonly List<ShaderMaterial> _shellMats = new();
	private MeshInstance3D _lashes = null!;
	private MeshInstance3D _cap = null!;
	private ShaderMaterial _capMat = null!;
	private SubViewport _logoViewport = null!;
	private Label _logoLabel = null!;
	private readonly Dictionary<string, ImageTexture> _logos = new(StringComparer.Ordinal);
	private bool _busy;
	private bool _ready;

	public static bool Available => FaceKit.Shared != null;

	/// <summary>The studio node, created under the scene root the first time it's needed.</summary>
	public static HeadshotStudio? Get(Node anyNode)
	{
		if (_instance != null && IsInstanceValid(_instance))
		{
			return _instance;
		}

		if (!Available || anyNode.GetTree() == null)
		{
			return null;
		}

		_instance = new HeadshotStudio { Name = "HeadshotStudio" };
		anyNode.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _instance);
		return _instance;
	}

	/// <summary>Cached texture if this face was already shot, else null.</summary>
	public Texture2D? Peek(FaceRecipe recipe)
	{
		if (_cache.TryGetValue(recipe.Key, out Texture2D? tex))
		{
			Touch(recipe.Key);
			return tex;
		}

		string path = $"{DiskDir}/{recipe.Key}.png";
		if (Godot.FileAccess.FileExists(path))
		{
			Image? img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
			if (img != null && !img.IsEmpty())
			{
				Texture2D loaded = ImageTexture.CreateFromImage(img);
				Remember(recipe.Key, loaded);
				return loaded;
			}
		}

		return null;
	}

	/// <summary>
	/// For editors: only the newest request matters and nothing is written
	/// to disk, so dragging a slider doesn't queue a hundred renders.
	/// </summary>
	public void RequestLive(FaceRecipe recipe, Action<Texture2D> done)
	{
		if (_cache.TryGetValue(recipe.Key, out Texture2D? have))
		{
			done(have);
			return;
		}

		_live = (recipe, done);
	}

	public void Request(FaceRecipe recipe, Action<Texture2D> done)
	{
		Texture2D? have = Peek(recipe);
		if (have != null)
		{
			done(have);
			return;
		}

		if (_waiting.TryGetValue(recipe.Key, out List<Action<Texture2D>>? list))
		{
			list.Add(done);
			return;
		}

		_waiting[recipe.Key] = [done];
		_queue.Enqueue(recipe);
	}

	public override void _Ready()
	{
		DirAccess.MakeDirRecursiveAbsolute(DiskDir);
		_viewport = new SubViewport
		{
			Size = new Vector2I(Width, Height),
			TransparentBg = true,
			OwnWorld3D = true,
			Msaa3D = Viewport.Msaa.Msaa4X,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		AddChild(_viewport);

		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.ClearColor,
			TonemapMode = Godot.Environment.ToneMapper.Linear,
		};
		_camera = new Camera3D
		{
			Fov = 15f,
			Near = 0.05f,
			Far = 10f,
			Environment = env,
		};
		_viewport.AddChild(_camera);
		AimCamera(0f);

		_skin = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/facekit/skin.gdshader") };
		_skin.SetShaderParameter("masks", LoadMask("masks"));
		_skin.SetShaderParameter("detail", LoadMask("detail"));
		_skin.SetShaderParameter("hair_map", LoadMask("hair"));
		_skin.SetShaderParameter("cloth", LoadMask("cloth"));
		ImageTexture light = LoadMask("light");
		_skin.SetShaderParameter("light_map", light);
		_skin.SetShaderParameter("wrinkle", LoadMask("wrinkle"));
		_skin.SetShaderParameter("tone", LoadMask("tone"));
		_head = new MeshInstance3D { MaterialOverride = _skin };
		_viewport.AddChild(_head);

		// Hair, beard and brows: the head pushed out in alpha-blended layers,
		// drawn inside-out (render priority = layer).
		Shader shellShader = GD.Load<Shader>("res://assets/facekit/shell.gdshader");
		for (int i = 1; i <= ShellCount; i++)
		{
			var mat = new ShaderMaterial { Shader = shellShader, RenderPriority = i };
			mat.SetShaderParameter("masks", _skin.GetShaderParameter("masks"));
			mat.SetShaderParameter("detail", _skin.GetShaderParameter("detail"));
			mat.SetShaderParameter("hair_map", _skin.GetShaderParameter("hair_map"));
			mat.SetShaderParameter("light_map", light);
			mat.SetShaderParameter("shell", (float)i);
			mat.SetShaderParameter("shells", (float)ShellCount);
			var mi = new MeshInstance3D { MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
			_viewport.AddChild(mi);
			_shells.Add(mi);
			_shellMats.Add(mat);
		}

		_lashes = new MeshInstance3D
		{
			MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/facekit/lash.gdshader"), RenderPriority = ShellCount + 2 },
		};
		_viewport.AddChild(_lashes);

		_capMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/facekit/cap.gdshader") };
		_cap = new MeshInstance3D { MaterialOverride = _capMat, Visible = false };
		_viewport.AddChild(_cap);

		// Cap monogram: letters rendered once per school into a texture.
		_logoViewport = new SubViewport
		{
			Size = new Vector2I(256, 256),
			TransparentBg = true,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		AddChild(_logoViewport);
		_logoLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Size = new Vector2(256, 256),
		};
		_logoLabel.AddThemeFontOverride("font", GD.Load<Font>("res://assets/fonts/BarlowCondensed-Bold.ttf"));
		_logoLabel.AddThemeColorOverride("font_color", Colors.White);
		_logoViewport.AddChild(_logoLabel);

		Shader eyeShader = GD.Load<Shader>("res://assets/facekit/eye.gdshader");
		var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 48, Rings = 32 };
		_eyeMatL = new ShaderMaterial { Shader = eyeShader };
		_eyeMatR = new ShaderMaterial { Shader = eyeShader };
		_eyeL = new MeshInstance3D { Mesh = sphere, MaterialOverride = _eyeMatL };
		_eyeR = new MeshInstance3D { Mesh = sphere, MaterialOverride = _eyeMatR };
		_viewport.AddChild(_eyeL);
		_viewport.AddChild(_eyeR);
		_ready = true;
	}

	public override void _Process(double delta)
	{
		if (!_ready || _busy)
		{
			return;
		}

		if (_live is { } live)
		{
			_live = null;
			ShootLive(live.Recipe, live.Done);
			return;
		}

		if (_queue.Count > 0)
		{
			ShootNext();
		}
	}

	private async void ShootNext()
	{
		_busy = true;
		FaceRecipe recipe = _queue.Dequeue();
		try
		{
			await EnsureLogo(recipe.Monogram);
			Pose(recipe);
			_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			Image image = _viewport.GetTexture().GetImage();
			Texture2D tex = ImageTexture.CreateFromImage(image);
			image.SavePng($"{DiskDir}/{recipe.Key}.png");
			Remember(recipe.Key, tex);
			if (_waiting.Remove(recipe.Key, out List<Action<Texture2D>>? list))
			{
				foreach (Action<Texture2D> cb in list)
				{
					cb(tex);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PushError($"HeadshotStudio: {ex.Message}");
			_waiting.Remove(recipe.Key);
		}
		finally
		{
			_busy = false;
		}
	}

	private async void ShootLive(FaceRecipe recipe, Action<Texture2D> done)
	{
		_busy = true;
		try
		{
			await EnsureLogo(recipe.Monogram);
			Pose(recipe);
			_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			Texture2D tex = ImageTexture.CreateFromImage(_viewport.GetTexture().GetImage());
			Remember(recipe.Key, tex);
			done(tex);
		}
		catch (Exception ex)
		{
			GD.PushError($"HeadshotStudio: {ex.Message}");
		}
		finally
		{
			_busy = false;
		}
	}

	private static readonly Vector3 Pivot = new(0f, -0.055f, -0.04f);

	private void AimCamera(float yaw)
	{
		Vector3 offset = new Vector3(0.10f, 0.055f, 1.39f).Rotated(Vector3.Up, Mathf.DegToRad(yaw));
		_camera.LookAtFromPosition(Pivot + offset, Pivot, Vector3.Up);
	}

	private void Pose(FaceRecipe r)
	{
		AimCamera(r.Yaw);
		FaceKit kit = FaceKit.Shared!;
		Vector3[] p = kit.Morph(r.Weights);
		Vector3[] n = kit.Normals(p);
		ArrayMesh skin = kit.BuildSkin(p, n);
		_head.Mesh = skin;
		float cap = r.Cap ? 1f : 0f;
		// Under a cap the scalp paint is clipped to the sides (x, y kept; fade and crown thinning maxed).
		Vector4 skinStyle = r.Cap ? new Vector4(r.HairShape.X, r.HairShape.Y, 1f, 1f) : r.HairShape;
		_skin.SetShaderParameter("skin_color", r.Skin);
		_skin.SetShaderParameter("hair_color", r.Hair);
		_skin.SetShaderParameter("brow_color", r.Brow);
		_skin.SetShaderParameter("hair_style", skinStyle);
		_skin.SetShaderParameter("beard", r.Beard);
		_skin.SetShaderParameter("jersey_color", r.Jersey);
		_skin.SetShaderParameter("trim_color", r.Trim);
		_skin.SetShaderParameter("show_jersey", r.ShowJersey ? 1f : 0f);
		_skin.SetShaderParameter("age", (float)r.Age);
		_skin.SetShaderParameter("cap", cap);

		foreach (MeshInstance3D shell in _shells)
		{
			shell.Mesh = skin; // brows always need the shells, so every face draws them
		}

		foreach (ShaderMaterial mat in _shellMats)
		{
			mat.SetShaderParameter("hair_color", r.Hair);
			mat.SetShaderParameter("brow_color", r.Brow);
			mat.SetShaderParameter("hair_style", r.HairShape);
			mat.SetShaderParameter("hair_length", r.HairLength);
			mat.SetShaderParameter("beard", r.Beard);
			mat.SetShaderParameter("cap", cap);
		}

		_lashes.Mesh = kit.Surfaces.ContainsKey("lashes") ? kit.BuildSurface("lashes", p, n) : null;

		_cap.Visible = r.Cap;
		if (r.Cap)
		{
			_cap.Mesh = BallCap.Build(p);
			_capMat.SetShaderParameter("cap_color", r.Jersey);
			_capMat.SetShaderParameter("logo_color", r.Trim);
			bool hasLogo = r.Monogram.Length > 0 && _logos.ContainsKey(r.Monogram);
			_capMat.SetShaderParameter("has_logo", hasLogo ? 1f : 0f);
			if (hasLogo)
			{
				_capMat.SetShaderParameter("logo", _logos[r.Monogram]);
			}
		}

		_eyeMatL.SetShaderParameter("cap", cap);
		_eyeMatR.SetShaderParameter("cap", cap);
		PlaceEye(_eyeL, _eyeMatL, kit, p, "eye_l", r.Iris, _camera.Position);
		PlaceEye(_eyeR, _eyeMatR, kit, p, "eye_r", r.Iris, _camera.Position);
	}

	/// <summary>Render the school letters for a cap front once and keep the texture.</summary>
	private async System.Threading.Tasks.Task EnsureLogo(string monogram)
	{
		if (string.IsNullOrEmpty(monogram) || _logos.ContainsKey(monogram))
		{
			return;
		}

		_logoLabel.Text = monogram;
		_logoLabel.AddThemeFontSizeOverride("font_size", monogram.Length switch { 1 => 210, 2 => 170, _ => 120 });
		_logoViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Image img = _logoViewport.GetTexture().GetImage();
		img.GenerateMipmaps();
		_logos[monogram] = ImageTexture.CreateFromImage(img);
	}

	private static void PlaceEye(MeshInstance3D eye, ShaderMaterial mat, FaceKit kit, Vector3[] p, string group, Color iris, Vector3 camera)
	{
		Vector3 c = kit.Centroid(p, group);
		float radius = kit.Radius(p, group, c) * 0.96f;
		eye.Position = c;
		eye.Scale = Vector3.One * radius;
		mat.SetShaderParameter("iris_color", iris);
		mat.SetShaderParameter("look_dir", new Vector3(-c.X * 0.35f + (camera.X - c.X) * 0.02f, 0f, 1f).Normalized());
	}

	private static ImageTexture LoadMask(string name)
	{
		byte[] bytes = Godot.FileAccess.GetFileAsBytes($"res://assets/facekit/{name}.facetex");
		var image = new Image();
		image.LoadPngFromBuffer(bytes);
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	private void Remember(string key, Texture2D tex)
	{
		_cache[key] = tex;
		Touch(key);
		while (_lru.Count > MemoryCacheLimit)
		{
			string old = _lru.Last!.Value;
			_lru.RemoveLast();
			_cache.Remove(old);
		}
	}

	private void Touch(string key)
	{
		_lru.Remove(key);
		_lru.AddFirst(key);
	}
}
