namespace HSBM.Domain;

/// <summary>
/// Deterministic RNG used for career generation and game simulation.
/// Do not use <see cref="Random.Shared"/>.
/// </summary>
public interface ISeededRandom
{
	int Seed { get; }
	int Next(int minInclusive, int maxExclusive);
	double NextDouble();
	void NextBytes(Span<byte> buffer);
	Guid NextGuid();
	ISeededRandom Derive(int salt);
	T Pick<T>(IReadOnlyList<T> items);
	void Shuffle<T>(IList<T> items);
}

public sealed class SeededRandom : ISeededRandom
{
	private readonly Random _random;

	public SeededRandom(int seed)
	{
		Seed = seed;
		_random = new Random(seed);
	}

	public int Seed { get; }

	public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

	public double NextDouble() => _random.NextDouble();

	public void NextBytes(Span<byte> buffer) => _random.NextBytes(buffer);

	public Guid NextGuid()
	{
		Span<byte> bytes = stackalloc byte[16];
		_random.NextBytes(bytes);
		return new Guid(bytes);
	}

	public ISeededRandom Derive(int salt)
	{
		unchecked
		{
			int mixed = (Seed * 397) ^ salt;
			if (mixed == Seed)
			{
				mixed ^= unchecked((int)0xA5A5A5A5);
			}

			return new SeededRandom(mixed);
		}
	}

	public T Pick<T>(IReadOnlyList<T> items)
	{
		if (items.Count == 0)
		{
			throw new InvalidOperationException("Cannot pick from an empty list.");
		}

		return items[Next(0, items.Count)];
	}

	public void Shuffle<T>(IList<T> items)
	{
		for (int i = items.Count - 1; i > 0; i--)
		{
			int j = Next(0, i + 1);
			(items[i], items[j]) = (items[j], items[i]);
		}
	}

	/// <summary>
	/// Stable mix of two integers. Do not use HashCode.Combine
	/// (it is not guaranteed stable across runtimes).
	/// </summary>
	public static int Mix(int left, int right)
	{
		unchecked
		{
			return (left * 397) ^ right;
		}
	}

	/// <summary>
	/// Stable mix of a career seed and a game id. Do not use HashCode.Combine
	/// (it is not guaranteed stable across runtimes).
	/// </summary>
	public static int Mix(int careerSeed, Guid gameId)
	{
		unchecked
		{
			int hash = careerSeed;
			foreach (byte b in gameId.ToByteArray())
			{
				hash = (hash * 31) + b;
			}

			return hash;
		}
	}
}
