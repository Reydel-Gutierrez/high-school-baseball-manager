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

	private SubViewport _viewport = null!;
	private MeshInstance3D _head = null!;
	private MeshInstance3D _eyeL = null!;
	private MeshInstance3D _eyeR = null!;
	private ShaderMaterial _skin = null!;
	private ShaderMaterial _eyeMatL = null!;
	private ShaderMaterial _eyeMatR = null!;
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
		if (FileAccess.FileExists(path))
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
		var camera = new Camera3D
		{
			Fov = 15f,
			Near = 0.05f,
			Far = 10f,
			Environment = env,
		};
		_viewport.AddChild(camera);
		camera.Position = new Vector3(0.10f, 0.0f, 1.35f);
		camera.LookAtFromPosition(camera.Position, new Vector3(0f, -0.055f, -0.04f), Vector3.Up);

		_skin = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/facekit/skin.gdshader") };
		_skin.SetShaderParameter("masks", LoadMask("masks"));
		_skin.SetShaderParameter("detail", LoadMask("detail"));
		_skin.SetShaderParameter("hair_map", LoadMask("hair"));
		_skin.SetShaderParameter("cloth", LoadMask("cloth"));
		_head = new MeshInstance3D { MaterialOverride = _skin };
		_viewport.AddChild(_head);

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
		if (!_ready || _busy || _queue.Count == 0)
		{
			return;
		}

		ShootNext();
	}

	private async void ShootNext()
	{
		_busy = true;
		FaceRecipe recipe = _queue.Dequeue();
		try
		{
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

	private void Pose(FaceRecipe r)
	{
		FaceKit kit = FaceKit.Shared!;
		Vector3[] p = kit.Morph(r.Weights);
		Vector3[] n = kit.Normals(p);
		_head.Mesh = kit.BuildSkin(p, n);
		_skin.SetShaderParameter("skin_color", r.Skin);
		_skin.SetShaderParameter("hair_color", r.Hair);
		_skin.SetShaderParameter("brow_color", r.Brow);
		_skin.SetShaderParameter("hair_style", r.HairStyle);
		_skin.SetShaderParameter("hair_length", r.HairLength);
		_skin.SetShaderParameter("beard", r.Beard);
		_skin.SetShaderParameter("jersey_color", r.Jersey);
		_skin.SetShaderParameter("trim_color", r.Trim);
		_skin.SetShaderParameter("show_jersey", r.ShowJersey ? 1f : 0f);
		_skin.SetShaderParameter("age_lines", r.AgeLines);
		PlaceEye(_eyeL, _eyeMatL, kit, p, "eye_l", r.Iris);
		PlaceEye(_eyeR, _eyeMatR, kit, p, "eye_r", r.Iris);
	}

	private static void PlaceEye(MeshInstance3D eye, ShaderMaterial mat, FaceKit kit, Vector3[] p, string group, Color iris)
	{
		Vector3 c = kit.Centroid(p, group);
		float radius = kit.Radius(p, group, c) * 0.96f;
		eye.Position = c;
		eye.Scale = Vector3.One * radius;
		mat.SetShaderParameter("iris_color", iris);
		mat.SetShaderParameter("look_dir", new Vector3(-c.X * 0.4f, 0f, 1f).Normalized());
	}

	private static ImageTexture LoadMask(string name)
	{
		byte[] bytes = FileAccess.GetFileAsBytes($"res://assets/facekit/{name}.facetex");
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
