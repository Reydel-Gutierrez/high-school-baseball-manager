using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// The 3D head: a CC0 MakeHuman bust (head, neck, shoulders) with its
/// morph targets, baked by tools/facekit/build_kit.py into one binary.
/// Morphing is plain vertex arithmetic, so a face is built in about a
/// millisecond and the result is stable for a given set of weights.
/// </summary>
public sealed class FaceKit
{
	public const string KitPath = "res://assets/facekit/facekit.bin";

	private static FaceKit? _shared;

	private readonly Dictionary<string, (int[] Index, Vector3[] Delta)> _targets = new(StringComparer.Ordinal);

	public int SubCount { get; private set; }
	public Vector3[] Base { get; private set; } = [];
	public int[] RenderSub { get; private set; } = [];
	public Vector2[] RenderUv { get; private set; } = [];
	public Dictionary<string, int[]> Surfaces { get; } = new(StringComparer.Ordinal);
	public Dictionary<string, int[]> Groups { get; } = new(StringComparer.Ordinal);

	/// <summary>Midpoint of the eyes on the base mesh; portraits are framed on it.</summary>
	public Vector3 BaseEyeMid { get; private set; }

	public static FaceKit? Shared
	{
		get
		{
			if (_shared == null && FileAccess.FileExists(KitPath))
			{
				try
				{
					_shared = Load(KitPath);
				}
				catch (Exception ex)
				{
					GD.PushError($"FaceKit: {ex.Message}");
				}
			}

			return _shared;
		}
	}

	public bool HasTarget(string name) => _targets.ContainsKey(name);

	public static FaceKit Load(string path)
	{
		byte[] bytes = FileAccess.GetFileAsBytes(path);
		var kit = new FaceKit();
		int o = 0;
		string magic = Encoding.ASCII.GetString(bytes, 0, 8);
		if (magic != "HSBMFK02")
		{
			throw new InvalidOperationException($"bad face kit header '{magic}'");
		}

		o = 8;
		int I32() { int v = BitConverter.ToInt32(bytes, o); o += 4; return v; }
		float F32() { float v = BitConverter.ToSingle(bytes, o); o += 4; return v; }
		string Str() { int n = I32(); string s = Encoding.UTF8.GetString(bytes, o, n); o += n; return s; }

		kit.SubCount = I32();
		kit.Base = new Vector3[kit.SubCount];
		for (int i = 0; i < kit.SubCount; i++)
		{
			kit.Base[i] = new Vector3(F32(), F32(), F32());
		}

		int render = I32();
		kit.RenderSub = new int[render];
		kit.RenderUv = new Vector2[render];
		for (int i = 0; i < render; i++)
		{
			kit.RenderSub[i] = I32();
			float u = F32();
			float v = F32();
			kit.RenderUv[i] = new Vector2(u, 1f - v);
		}

		int surfaces = I32();
		for (int s = 0; s < surfaces; s++)
		{
			string name = Str();
			int tris = I32();
			var idx = new int[tris * 3];
			for (int k = 0; k < idx.Length; k++)
			{
				idx[k] = I32();
			}

			kit.Surfaces[name] = idx;
		}

		int groups = I32();
		for (int g = 0; g < groups; g++)
		{
			string name = Str();
			int n = I32();
			var idx = new int[n];
			for (int k = 0; k < n; k++)
			{
				idx[k] = I32();
			}

			kit.Groups[name] = idx;
		}

		int targets = I32();
		for (int t = 0; t < targets; t++)
		{
			string name = Str();
			int n = I32();
			var idx = new int[n];
			var delta = new Vector3[n];
			for (int k = 0; k < n; k++)
			{
				idx[k] = I32();
				delta[k] = new Vector3(F32(), F32(), F32());
			}

			kit._targets[name] = (idx, delta);
		}

		kit.BaseEyeMid = (kit.Centroid(kit.Base, "eye_l") + kit.Centroid(kit.Base, "eye_r")) / 2f;
		return kit;
	}

	public Vector3 Centroid(Vector3[] positions, string group)
	{
		Vector3 sum = Vector3.Zero;
		int[] idx = Groups[group];
		foreach (int i in idx)
		{
			sum += positions[i];
		}

		return sum / Math.Max(1, idx.Length);
	}

	public float Radius(Vector3[] positions, string group, Vector3 center)
	{
		float sum = 0;
		int[] idx = Groups[group];
		foreach (int i in idx)
		{
			sum += positions[i].DistanceTo(center);
		}

		return sum / Math.Max(1, idx.Length);
	}

	/// <summary>Base mesh plus weighted target deltas, re-centred on the eyes.</summary>
	public Vector3[] Morph(IReadOnlyDictionary<string, float> weights)
	{
		var p = (Vector3[])Base.Clone();
		foreach ((string name, float w) in weights)
		{
			if (w == 0 || !_targets.TryGetValue(name, out (int[] Index, Vector3[] Delta) t))
			{
				continue;
			}

			for (int k = 0; k < t.Index.Length; k++)
			{
				p[t.Index[k]] += t.Delta[k] * w;
			}
		}

		Vector3 mid = (Centroid(p, "eye_l") + Centroid(p, "eye_r")) / 2f;
		for (int i = 0; i < p.Length; i++)
		{
			p[i] -= mid;
		}

		return p;
	}

	public Vector3[] Normals(Vector3[] p)
	{
		var n = new Vector3[p.Length];
		int[] tris = Surfaces["skin"];
		for (int t = 0; t < tris.Length; t += 3)
		{
			int a = RenderSub[tris[t]];
			int b = RenderSub[tris[t + 1]];
			int c = RenderSub[tris[t + 2]];
			Vector3 face = (p[c] - p[a]).Cross(p[b] - p[a]);
			n[a] += face;
			n[b] += face;
			n[c] += face;
		}

		for (int i = 0; i < n.Length; i++)
		{
			n[i] = n[i].LengthSquared() > 0 ? n[i].Normalized() : Vector3.Up;
		}

		return n;
	}

	public ArrayMesh BuildSkin(Vector3[] p, Vector3[] n)
	{
		int count = RenderSub.Length;
		var verts = new Vector3[count];
		var norms = new Vector3[count];
		for (int i = 0; i < count; i++)
		{
			verts[i] = p[RenderSub[i]];
			norms[i] = n[RenderSub[i]];
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.Normal] = norms;
		arrays[(int)Mesh.ArrayType.TexUV] = RenderUv;
		arrays[(int)Mesh.ArrayType.Index] = Surfaces["skin"];
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}
}
