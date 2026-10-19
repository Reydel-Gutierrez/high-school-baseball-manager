using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Fitted ball cap generated per head (port of tools/facekit/web/cap.js).
/// Crown: a parametric dome over the scalp. For each azimuth (theta, 0 =
/// straight ahead) and polar angle (phi, 0 = top) the head's outer radius is
/// read from a direction histogram of its vertices, then pushed out by the
/// cap's thickness. The rim follows a hat line (low at the back, above the
/// ears, high on the forehead). Brim: pre-curved bill with thickness.
/// UV: crown u = theta/180 (-1..1), v = s (0 top .. 1 rim); brim top v = 2,
/// brim edge 2.5, underbrim 3, button -1.
/// </summary>
public static class BallCap
{
	private static readonly Vector3 C0 = new(0f, 0.030f, -0.060f);
	private const int NT = 72;
	private const int NP = 40;
	private const float PhiMax = Mathf.Pi * 0.75f;

	public static float HatLine(float thDeg)
	{
		float a = Math.Abs(thDeg);
		ReadOnlySpan<float> deg = [0, 30, 60, 90, 120, 150, 180];
		ReadOnlySpan<float> y = [0.043f, 0.042f, 0.035f, 0.022f, 0.008f, -0.006f, -0.012f];
		for (int i = 0; i < deg.Length - 1; i++)
		{
			if (a <= deg[i + 1])
			{
				float t = (a - deg[i]) / (deg[i + 1] - deg[i]);
				return y[i] + (y[i + 1] - y[i]) * t;
			}
		}

		return y[^1];
	}

	private sealed class Field
	{
		private readonly float[] _r;

		public Field(Vector3[] p)
		{
			_r = new float[(NT + 1) * (NP + 1)];
			foreach (Vector3 v in p)
			{
				Vector3 d = v - C0;
				float r = d.Length();
				if (r < 1e-6f)
				{
					continue;
				}

				float phi = Mathf.Acos(Mathf.Clamp(d.Y / r, -1f, 1f));
				if (phi > PhiMax)
				{
					continue;
				}

				float th = Mathf.Atan2(d.X, d.Z);
				int ti = (int)Math.Round((th / Mathf.Pi + 1f) * NT / 2f);
				int pj = (int)Math.Round(phi / PhiMax * NP);
				int k = ti * (NP + 1) + pj;
				if (r > _r[k])
				{
					_r[k] = r;
				}
			}

			// fill holes, then smooth (wrapping in theta)
			var o = new float[_r.Length];
			for (int pass = 0; pass < 16; pass++)
			{
				for (int ti = 0; ti <= NT; ti++)
				{
					for (int pj = 0; pj <= NP; pj++)
					{
						float s = 0;
						int c = 0;
						for (int dt = -1; dt <= 1; dt++)
						{
							for (int dp = -1; dp <= 1; dp++)
							{
								int t2 = (ti + dt + NT) % NT;
								int p2 = Math.Clamp(pj + dp, 0, NP);
								float val = _r[t2 * (NP + 1) + p2];
								if (val > 0)
								{
									s += val;
									c++;
								}
							}
						}

						int k = ti * (NP + 1) + pj;
						float cur = _r[k];
						if (pass > 2)
						{
							o[k] = c > 0 ? (cur > 0 ? cur * 0.3f + s / c * 0.7f : s / c) : cur;
						}
						else
						{
							o[k] = c > 0 ? Math.Max(cur, s / c) : cur;
						}
					}
				}

				Array.Copy(o, _r, _r.Length);
			}
		}

		public float At(float th, float phi)
		{
			float tf = (th / Mathf.Pi + 1f) * NT / 2f;
			float pf = Mathf.Clamp(phi / PhiMax * NP, 0, NP);
			int t0 = (int)Math.Floor(tf);
			int p0 = (int)Math.Floor(pf);
			float ft = tf - t0;
			float fp = pf - p0;
			float G(int a, int b) => _r[(((a % NT) + NT) % NT) * (NP + 1) + Math.Min(NP, b)];
			return (G(t0, p0) * (1 - ft) + G(t0 + 1, p0) * ft) * (1 - fp) + (G(t0, p0 + 1) * (1 - ft) + G(t0 + 1, p0 + 1) * ft) * fp;
		}
	}

	public static ArrayMesh Build(Vector3[] p)
	{
		var field = new Field(p);
		var pos = new List<Vector3>();
		var nor = new List<Vector3>();
		var uv = new List<Vector2>();
		var idx = new List<int>();

		Vector3 Point(float th, float phi, float off)
		{
			float r = field.At(th, phi) + off;
			return new Vector3(C0.X + r * Mathf.Sin(phi) * Mathf.Sin(th), C0.Y + r * Mathf.Cos(phi), C0.Z + r * Mathf.Sin(phi) * Mathf.Cos(th));
		}

		// rim polar angle per azimuth: where the dome meets the hat line
		float RimPhi(float th)
		{
			float target = HatLine(Mathf.RadToDeg(th));
			float lo = 0.2f;
			float hi = Mathf.Pi * 0.74f;
			for (int i = 0; i < 30; i++)
			{
				float m = (lo + hi) / 2f;
				if (Point(th, m, 0.008f).Y > target)
				{
					lo = m;
				}
				else
				{
					hi = m;
				}
			}

			return (lo + hi) / 2f;
		}

		const int Seg = 96;
		const int Ring = 28;
		for (int i = 0; i <= Seg; i++)
		{
			float th = -Mathf.Pi + Mathf.Tau * i / Seg;
			float pr = RimPhi(th);
			for (int j = 0; j <= Ring; j++)
			{
				float s = j / (float)Ring;
				float front = Math.Max(0f, Mathf.Cos(th));
				float off = 0.0060f + 0.0070f * Mathf.Sin(Mathf.Pi * Math.Min(1f, s * 1.1f)) * (0.5f + 0.5f * front) + 0.0025f * (1 - s);
				pos.Add(Point(th, pr * s, off));
				uv.Add(new Vector2(th / Mathf.Pi, s));
				nor.Add(Vector3.Zero);
			}
		}

		int At(int i, int j) => i * (Ring + 1) + j;
		for (int i = 0; i < Seg; i++)
		{
			for (int j = 0; j < Ring; j++)
			{
				idx.AddRange([At(i, j), At(i + 1, j), At(i, j + 1), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1)]);
			}
		}

		SmoothNormals(pos, idx, nor, 0, int.MaxValue);

		// top button
		Vector3 top = Point(0, 0, 0.0085f);
		int bStart = pos.Count;
		for (int j = 0; j <= 6; j++)
		{
			for (int i = 0; i <= 16; i++)
			{
				float a = i / 16f * Mathf.Tau;
				float e = j / 6f * Mathf.Pi / 2f;
				float rr = 0.0042f * Mathf.Cos(e);
				pos.Add(new Vector3(top.X + rr * Mathf.Sin(a), top.Y + 0.0028f * Mathf.Sin(e), top.Z + rr * Mathf.Cos(a)));
				nor.Add(new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a)));
				uv.Add(new Vector2(0, -1));
			}
		}

		for (int j = 0; j < 6; j++)
		{
			for (int i = 0; i < 16; i++)
			{
				int a = bStart + j * 17 + i;
				idx.AddRange([a, a + 1, a + 17, a + 1, a + 18, a + 17]);
			}
		}

		// brim: from the front rim, pre-curved, 3 mm thick
		int brimStart = pos.Count;
		const int B = 40;
		const int D = 10;
		const float Th = 0.003f;
		float span = Mathf.DegToRad(64f);
		var grid = new Vector3[B + 1, D + 1];
		for (int i = 0; i <= B; i++)
		{
			float th = -span + 2 * span * i / B;
			float pr = RimPhi(th);
			Vector3 b = Point(th, pr, 0.0075f);
			// bills point forward (D-shaped outline), not radially out to the sides
			var fwd = new Vector3(Mathf.Sin(th) * 0.25f, 0, Mathf.Cos(th) * 0.25f + 0.75f).Normalized();
			float len = 0.074f * Mathf.Sqrt(Math.Max(0f, 1f - (th / span) * (th / span))) + 0.002f;
			for (int k = 0; k <= D; k++)
			{
				float t = k / (float)D;
				float o = len * t;
				float curl = -0.032f * Mathf.Pow(Math.Abs(th) / span, 1.5f) * Mathf.Pow(t, 0.8f); // sides bend down
				float droop = -Mathf.Tan(Mathf.DegToRad(9f)) * o; // bill tilts down
				grid[i, k] = new Vector3(b.X + fwd.X * o, b.Y + droop + curl, b.Z + fwd.Z * o);
			}
		}

		for (int side = 0; side <= 1; side++)
		{
			int start = pos.Count;
			for (int i = 0; i <= B; i++)
			{
				for (int k = 0; k <= D; k++)
				{
					Vector3 q = grid[i, k];
					pos.Add(new Vector3(q.X, q.Y - side * Th, q.Z));
					nor.Add(new Vector3(0, side == 1 ? -1 : 1, 0));
					uv.Add(new Vector2(i / (float)B * 2 - 1, side == 1 ? 3 : 2));
				}
			}

			for (int i = 0; i < B; i++)
			{
				for (int k = 0; k < D; k++)
				{
					int a = start + i * (D + 1) + k;
					int b = a + D + 1;
					if (side == 1)
					{
						idx.AddRange([a, a + 1, b, a + 1, b + 1, b]);
					}
					else
					{
						idx.AddRange([a, b, a + 1, a + 1, b, b + 1]);
					}
				}
			}
		}

		// brim edge strip
		int eStart = pos.Count;
		for (int i = 0; i <= B; i++)
		{
			Vector3 q = grid[i, D];
			Vector3 f = new Vector3(q.X, 0, q.Z + 0.06f).Normalized();
			pos.Add(q);
			pos.Add(new Vector3(q.X, q.Y - Th, q.Z));
			nor.Add(f);
			nor.Add(f);
			uv.Add(new Vector2(0, 2.5f));
			uv.Add(new Vector2(0, 2.5f));
		}

		for (int i = 0; i < B; i++)
		{
			int a = eStart + i * 2;
			idx.AddRange([a, a + 1, a + 2, a + 1, a + 3, a + 2]);
		}

		SmoothNormals(pos, idx, nor, brimStart, eStart);

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = pos.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = nor.ToArray();
		arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	private static void SmoothNormals(List<Vector3> pos, List<int> idx, List<Vector3> nor, int from, int to)
	{
		var acc = new Vector3[pos.Count];
		for (int t = 0; t < idx.Count; t += 3)
		{
			int a = idx[t];
			int b = idx[t + 1];
			int c = idx[t + 2];
			if (a < from || a >= to)
			{
				continue;
			}

			Vector3 n = (pos[b] - pos[a]).Cross(pos[c] - pos[a]);
			acc[a] += n;
			acc[b] += n;
			acc[c] += n;
		}

		for (int i = from; i < Math.Min(to, pos.Count); i++)
		{
			if (acc[i].LengthSquared() > 0)
			{
				nor[i] = acc[i].Normalized();
			}
		}
	}
}
