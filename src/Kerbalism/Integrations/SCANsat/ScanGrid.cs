using System;
using System.IO;
using System.IO.Compression;

namespace KERBALISM
{
	/// <summary>
	/// 360x180 Int16 coverage grid helpers (SCANsat layout) with deflate persistence.
	/// </summary>
	internal static class ScanGrid
	{
		public const int Width = 360;
		public const int Height = 180;
		public const double FullSphereWeight = 41251.914;

		public static Int16[,] Create() => new Int16[Width, Height];

		public static Int16[,] Clone(Int16[,] source)
		{
			if (source == null)
				return null;

			var copy = Create();
			Array.Copy(source, copy, source.Length);
			return copy;
		}

		public static bool IsEmpty(Int16[,] grid)
		{
			if (grid == null)
				return true;

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					if (grid[x, y] != 0)
						return false;
				}
			}

			return true;
		}

		public static void Or(Int16[,] target, Int16[,] source)
		{
			if (target == null || source == null)
				return;

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					target[x, y] |= source[x, y];
				}
			}
		}

		public static void OrMasked(Int16[,] target, Int16[,] source, short mask)
		{
			if (target == null || source == null || mask == 0)
				return;

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					short bits = (short)(source[x, y] & mask);
					if (bits != 0)
						target[x, y] |= bits;
				}
			}
		}

		public static Int16[,] ExtractMask(Int16[,] source, short mask)
		{
			if (source == null || mask == 0)
				return null;

			Int16[,] result = null;
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					short bits = (short)(source[x, y] & mask);
					if (bits == 0)
						continue;

					if (result == null)
						result = Create();
					result[x, y] = bits;
				}
			}

			return result;
		}

		public static void ClearMask(Int16[,] grid, short mask)
		{
			if (grid == null || mask == 0)
				return;

			short clear = (short)~mask;
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					grid[x, y] = (short)(grid[x, y] & clear);
				}
			}
		}

		public static void ClearBits(Int16[,] grid, Int16[,] bits)
		{
			if (grid == null || bits == null)
				return;

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					if (bits[x, y] != 0)
						grid[x, y] = (short)(grid[x, y] & ~bits[x, y]);
				}
			}
		}

		/// <summary>Area weight of cells where any bit in mask is set.</summary>
		public static double AreaWeight(Int16[,] grid, short mask)
		{
			if (grid == null || mask == 0)
				return 0.0;

			double weight = 0.0;
			int bitCount = CountBits(mask);
			if (bitCount <= 0)
				return 0.0;

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					short bits = (short)(grid[x, y] & mask);
					if (bits == 0)
						continue;

					double cell = CosLat(y);
					int set = CountBits(bits);
					weight += cell * set;
				}
			}

			return weight;
		}

		public static double CoveragePercent(Int16[,] grid, short mask)
		{
			int bitCount = CountBits(mask);
			if (bitCount <= 0)
				return 0.0;

			double weight = AreaWeight(grid, mask);
			double denom = FullSphereWeight * bitCount;
			if (denom <= double.Epsilon)
				return 0.0;

			return Math.Min(100.0, 100.0 * weight / denom);
		}

		/// <summary>
		/// Take cells from source (masked) until accumulated science size reaches budget.
		/// Returns extracted grid and corresponding size; clears taken bits from source.
		/// </summary>
		public static Int16[,] TakeBudget(Int16[,] source, short mask, double dataSize, double sizeBudget, out double takenSize)
		{
			takenSize = 0.0;
			if (source == null || mask == 0 || sizeBudget <= double.Epsilon || dataSize <= double.Epsilon)
				return null;

			int bitCount = CountBits(mask);
			if (bitCount <= 0)
				return null;

			double sizePerWeight = dataSize / (FullSphereWeight * bitCount);
			Int16[,] taken = null;

			for (int x = 0; x < Width && takenSize < sizeBudget; x++)
			{
				for (int y = 0; y < Height && takenSize < sizeBudget; y++)
				{
					short bits = (short)(source[x, y] & mask);
					if (bits == 0)
						continue;

					double bitSize = CosLat(y) * sizePerWeight;
					if (bitSize <= double.Epsilon)
						continue;

					for (int bitIndex = 0; bitIndex < 16; bitIndex++)
					{
						short bit = (short)(1 << bitIndex);
						if ((bits & bit) == 0 || takenSize + bitSize > sizeBudget + 1e-9)
							continue;

						if (taken == null)
							taken = Create();

						taken[x, y] |= bit;
						source[x, y] = (short)(source[x, y] & ~bit);
						takenSize += bitSize;
					}
				}
			}

			return taken;
		}

		public static string Encode(Int16[,] grid)
		{
			if (IsEmpty(grid))
				return string.Empty;

			byte[] raw = new byte[Width * Height * 2];
			int k = 0;
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					short v = grid[x, y];
					raw[k++] = (byte)(v & 0xff);
					raw[k++] = (byte)((v >> 8) & 0xff);
				}
			}

			using (var output = new MemoryStream())
			{
				using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
				{
					deflate.Write(raw, 0, raw.Length);
				}

				return Convert.ToBase64String(output.ToArray()).Replace('/', '-').Replace('=', '_');
			}
		}

		public static Int16[,] Decode(string blob)
		{
			if (string.IsNullOrEmpty(blob))
				return null;

			try
			{
				blob = blob.Replace('-', '/').Replace('_', '=');
				byte[] compressed = Convert.FromBase64String(blob);
				byte[] raw = new byte[Width * Height * 2];

				using (var input = new MemoryStream(compressed))
				using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
				{
					int read = 0;
					while (read < raw.Length)
					{
						int n = deflate.Read(raw, read, raw.Length - read);
						if (n <= 0)
							break;
						read += n;
					}
				}

				var grid = Create();
				int k = 0;
				for (int x = 0; x < Width; x++)
				{
					for (int y = 0; y < Height; y++)
					{
						grid[x, y] = (short)(raw[k] | (raw[k + 1] << 8));
						k += 2;
					}
				}

				return grid;
			}
			catch (Exception e)
			{
				Lib.Log("Failed to decode SCANsat scan payload: " + e.Message, Lib.LogLevel.Warning);
				return null;
			}
		}

		public static double CosLat(int y)
		{
			if (y < 0 || y >= Height)
				return 0.0;
			return Math.Cos((y - 90) * 0.0174532924);
		}

		public static int CountBits(int value)
		{
			int count = 0;
			uint v = unchecked((uint)value);
			while (v != 0)
			{
				count++;
				v &= v - 1;
			}
			return count;
		}
	}
}
