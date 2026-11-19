using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>
/// A small QR encoder (byte mode, error correction M, versions 1-10) and PNG writer, for the QR
/// image attached to the Wi-Fi email (spec 2, 5.5). The browser draws its own QR with the existing
/// AssetQr module. Follows ISO/IEC 18004; checked against the qrcode library in tests/qr_check.mjs.
/// </summary>
public static class WifiQr
{
	// Level M, versions 1..10: (EC codewords per block, blocks in group 1, data codewords, blocks in group 2, data codewords).
	private static readonly int[][] Blocks =
	{
		Array.Empty<int>(),
		new[] { 10, 1, 16, 0, 0 }, new[] { 16, 1, 28, 0, 0 }, new[] { 26, 1, 44, 0, 0 }, new[] { 18, 2, 32, 0, 0 }, new[] { 24, 2, 43, 0, 0 },
		new[] { 16, 4, 27, 0, 0 }, new[] { 18, 4, 31, 0, 0 }, new[] { 22, 2, 38, 2, 39 }, new[] { 22, 3, 36, 2, 37 }, new[] { 26, 4, 43, 1, 44 }
	};

	private static readonly int[][] Align =
	{
		Array.Empty<int>(), Array.Empty<int>(), new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 }, new[] { 6, 34 },
		new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 }, new[] { 6, 28, 50 }
	};

	/// <summary>Modules [y, x], true = dark. mask: 0-7 forces a mask (tests); -1 picks the lowest penalty.</summary>
	public static bool[,] Encode(string text, int mask = -1)
	{
		byte[] payload = Encoding.UTF8.GetBytes(text);
		int version = 0;
		for (int v = 1; v <= 10; v++)
		{
			int cap = DataCodewords(v);
			int bits = 4 + (v < 10 ? 8 : 16) + payload.Length * 8;
			if (bits <= cap * 8)
			{
				version = v;
				break;
			}
		}
		if (version == 0)
		{
			throw new ArgumentException("Too long for a Wi-Fi QR code.");
		}
		byte[] codewords = Interleave(version, DataBytes(version, payload));
		int size = version * 4 + 17;
		bool[,] m = new bool[size, size];
		bool[,] fn = new bool[size, size];
		DrawFunction(version, size, m, fn);
		DrawCodewords(size, m, fn, codewords);
		if (mask < 0)
		{
			int best = int.MaxValue;
			for (int k = 0; k < 8; k++)
			{
				bool[,] trial = (bool[,])m.Clone();
				ApplyMask(size, trial, fn, k);
				DrawFormat(size, trial, fn, k);
				int p = Penalty(size, trial);
				if (p < best)
				{
					best = p;
					mask = k;
				}
			}
		}
		ApplyMask(size, m, fn, mask);
		DrawFormat(size, m, fn, mask);
		return m;
	}

	private static int DataCodewords(int v) => Blocks[v][1] * Blocks[v][2] + Blocks[v][3] * Blocks[v][4];

	private static byte[] DataBytes(int version, byte[] payload)
	{
		List<bool> bits = new List<bool>();
		void Put(int value, int count)
		{
			for (int i = count - 1; i >= 0; i--)
			{
				bits.Add(((value >> i) & 1) != 0);
			}
		}
		Put(4, 4);
		Put(payload.Length, version < 10 ? 8 : 16);
		foreach (byte b in payload)
		{
			Put(b, 8);
		}
		int capBits = DataCodewords(version) * 8;
		Put(0, Math.Min(4, capBits - bits.Count));
		while (bits.Count % 8 != 0)
		{
			bits.Add(false);
		}
		byte[] data = new byte[DataCodewords(version)];
		int n = bits.Count / 8;
		for (int i = 0; i < n; i++)
		{
			int v = 0;
			for (int j = 0; j < 8; j++)
			{
				v = (v << 1) | (bits[i * 8 + j] ? 1 : 0);
			}
			data[i] = (byte)v;
		}
		for (int i = n, pad = 0; i < data.Length; i++, pad++)
		{
			data[i] = (byte)(pad % 2 == 0 ? 0xEC : 0x11);
		}
		return data;
	}

	private static byte[] Interleave(int version, byte[] data)
	{
		int[] spec = Blocks[version];
		int ecLen = spec[0];
		List<byte[]> dataBlocks = new List<byte[]>();
		int pos = 0;
		for (int g = 0; g < 2; g++)
		{
			int count = spec[1 + g * 2];
			int len = spec[2 + g * 2];
			for (int i = 0; i < count; i++)
			{
				dataBlocks.Add(data[pos..(pos + len)]);
				pos += len;
			}
		}
		byte[] gen = Generator(ecLen);
		List<byte[]> ecBlocks = new List<byte[]>();
		foreach (byte[] block in dataBlocks)
		{
			ecBlocks.Add(Remainder(block, gen));
		}
		List<byte> result = new List<byte>();
		int maxData = 0;
		foreach (byte[] b in dataBlocks)
		{
			maxData = Math.Max(maxData, b.Length);
		}
		for (int i = 0; i < maxData; i++)
		{
			foreach (byte[] b in dataBlocks)
			{
				if (i < b.Length)
				{
					result.Add(b[i]);
				}
			}
		}
		for (int i = 0; i < ecLen; i++)
		{
			foreach (byte[] b in ecBlocks)
			{
				result.Add(b[i]);
			}
		}
		return result.ToArray();
	}

	private static int Mul(int x, int y)
	{
		int z = 0;
		for (int i = 7; i >= 0; i--)
		{
			z = (z << 1) ^ ((z >> 7) * 0x11D);
			z ^= ((y >> i) & 1) * x;
		}
		return z;
	}

	/// <summary>Generator polynomial coefficients (highest degree first, leading 1 dropped).</summary>
	private static byte[] Generator(int degree)
	{
		byte[] r = new byte[degree];
		r[degree - 1] = 1;
		int root = 1;
		for (int i = 0; i < degree; i++)
		{
			for (int j = 0; j < r.Length; j++)
			{
				r[j] = (byte)Mul(r[j], root);
				if (j + 1 < r.Length)
				{
					r[j] ^= r[j + 1];
				}
			}
			root = Mul(root, 0x02);
		}
		return r;
	}

	private static byte[] Remainder(byte[] data, byte[] gen)
	{
		byte[] r = new byte[gen.Length];
		foreach (byte b in data)
		{
			int factor = b ^ r[0];
			Array.Copy(r, 1, r, 0, r.Length - 1);
			r[^1] = 0;
			for (int i = 0; i < r.Length; i++)
			{
				r[i] ^= (byte)Mul(gen[i], factor);
			}
		}
		return r;
	}

	private static void Set(bool[,] m, bool[,] fn, int x, int y, bool dark)
	{
		m[y, x] = dark;
		fn[y, x] = true;
	}

	private static void DrawFunction(int version, int size, bool[,] m, bool[,] fn)
	{
		for (int i = 0; i < size; i++)
		{
			Set(m, fn, 6, i, i % 2 == 0);
			Set(m, fn, i, 6, i % 2 == 0);
		}
		foreach ((int cx, int cy) in new[] { (3, 3), (size - 4, 3), (3, size - 4) })
		{
			for (int dy = -4; dy <= 4; dy++)
			{
				for (int dx = -4; dx <= 4; dx++)
				{
					int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
					int x = cx + dx, y = cy + dy;
					if (x >= 0 && x < size && y >= 0 && y < size)
					{
						Set(m, fn, x, y, d != 2 && d != 4);
					}
				}
			}
		}
		int[] a = Align[version];
		for (int i = 0; i < a.Length; i++)
		{
			for (int j = 0; j < a.Length; j++)
			{
				if ((i == 0 && j == 0) || (i == 0 && j == a.Length - 1) || (i == a.Length - 1 && j == 0))
				{
					continue;
				}
				for (int dy = -2; dy <= 2; dy++)
				{
					for (int dx = -2; dx <= 2; dx++)
					{
						Set(m, fn, a[i] + dx, a[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
					}
				}
			}
		}
		DrawFormat(size, m, fn, 0);
		if (version >= 7)
		{
			int rem = version;
			for (int i = 0; i < 12; i++)
			{
				rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
			}
			int bits = (version << 12) | rem;
			for (int i = 0; i < 18; i++)
			{
				bool bit = ((bits >> i) & 1) != 0;
				int p = size - 11 + i % 3, q = i / 3;
				Set(m, fn, p, q, bit);
				Set(m, fn, q, p, bit);
			}
		}
	}

	private static void DrawFormat(int size, bool[,] m, bool[,] fn, int mask)
	{
		int data = (0 << 3) | mask; // error correction M = 00
		int rem = data;
		for (int i = 0; i < 10; i++)
		{
			rem = (rem << 1) ^ ((rem >> 9) * 0x537);
		}
		int bits = ((data << 10) | rem) ^ 0x5412;
		bool B(int i) => ((bits >> i) & 1) != 0;
		for (int i = 0; i <= 5; i++)
		{
			Set(m, fn, 8, i, B(i));
		}
		Set(m, fn, 8, 7, B(6));
		Set(m, fn, 8, 8, B(7));
		Set(m, fn, 7, 8, B(8));
		for (int i = 9; i < 15; i++)
		{
			Set(m, fn, 14 - i, 8, B(i));
		}
		for (int i = 0; i < 8; i++)
		{
			Set(m, fn, size - 1 - i, 8, B(i));
		}
		for (int i = 8; i < 15; i++)
		{
			Set(m, fn, 8, size - 15 + i, B(i));
		}
		Set(m, fn, 8, size - 8, true);
	}

	private static void DrawCodewords(int size, bool[,] m, bool[,] fn, byte[] data)
	{
		int i = 0;
		for (int right = size - 1; right >= 1; right -= 2)
		{
			if (right == 6)
			{
				right = 5;
			}
			for (int vert = 0; vert < size; vert++)
			{
				for (int j = 0; j < 2; j++)
				{
					int x = right - j;
					bool upward = ((right + 1) & 2) == 0;
					int y = upward ? size - 1 - vert : vert;
					if (!fn[y, x] && i < data.Length * 8)
					{
						m[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
						i++;
					}
				}
			}
		}
	}

	private static void ApplyMask(int size, bool[,] m, bool[,] fn, int mask)
	{
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				bool invert = mask switch
				{
					0 => (x + y) % 2 == 0,
					1 => y % 2 == 0,
					2 => x % 3 == 0,
					3 => (x + y) % 3 == 0,
					4 => (x / 3 + y / 2) % 2 == 0,
					5 => x * y % 2 + x * y % 3 == 0,
					6 => (x * y % 2 + x * y % 3) % 2 == 0,
					_ => ((x + y) % 2 + x * y % 3) % 2 == 0
				};
				if (!fn[y, x] && invert)
				{
					m[y, x] = !m[y, x];
				}
			}
		}
	}

	/// <summary>The four penalty rules of the standard (any mask is a valid code; this only picks a good-looking one).</summary>
	private static int Penalty(int size, bool[,] m)
	{
		int score = 0;
		for (int pass = 0; pass < 2; pass++)
		{
			for (int a = 0; a < size; a++)
			{
				int run = 1;
				for (int b = 1; b < size; b++)
				{
					bool cur = pass == 0 ? m[a, b] : m[b, a];
					bool prev = pass == 0 ? m[a, b - 1] : m[b - 1, a];
					if (cur == prev)
					{
						run++;
					}
					else
					{
						if (run >= 5)
						{
							score += 3 + run - 5;
						}
						run = 1;
					}
				}
				if (run >= 5)
				{
					score += 3 + run - 5;
				}
				for (int b = 0; b + 10 < size; b++)
				{
					int bitsRow = 0;
					for (int k = 0; k < 11; k++)
					{
						bool v = pass == 0 ? m[a, b + k] : m[b + k, a];
						bitsRow = (bitsRow << 1) | (v ? 1 : 0);
					}
					if (bitsRow == 0x5D0 || bitsRow == 0x05D)
					{
						score += 40;
					}
				}
			}
		}
		int dark = 0;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				if (m[y, x])
				{
					dark++;
				}
				if (x + 1 < size && y + 1 < size && m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1])
				{
					score += 3;
				}
			}
		}
		int k5 = Math.Abs(dark * 20 - size * size * 10) / (size * size);
		return score + k5 * 10;
	}

	/// <summary>Grey-scale PNG, 4-module quiet zone.</summary>
	public static byte[] Png(string text, int scale = 8)
	{
		bool[,] m = Encode(text);
		int size = m.GetLength(0);
		int quiet = 4;
		int px = (size + quiet * 2) * scale;
		using MemoryStream raw = new MemoryStream();
		for (int y = 0; y < px; y++)
		{
			raw.WriteByte(0);
			int my = y / scale - quiet;
			for (int x = 0; x < px; x++)
			{
				int mx = x / scale - quiet;
				bool dark = my >= 0 && my < size && mx >= 0 && mx < size && m[my, mx];
				raw.WriteByte(dark ? (byte)0x24 : (byte)0xFF);
			}
		}
		byte[] idat;
		using (MemoryStream z = new MemoryStream())
		{
			using (ZLibStream zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true))
			{
				raw.Position = 0;
				raw.CopyTo(zs);
			}
			idat = z.ToArray();
		}
		using MemoryStream png = new MemoryStream();
		png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		byte[] ihdr = new byte[13];
		WriteInt(ihdr, 0, px);
		WriteInt(ihdr, 4, px);
		ihdr[8] = 8;
		ihdr[9] = 0;
		Chunk(png, "IHDR", ihdr);
		Chunk(png, "IDAT", idat);
		Chunk(png, "IEND", Array.Empty<byte>());
		return png.ToArray();
	}

	private static void WriteInt(byte[] b, int o, int v)
	{
		b[o] = (byte)(v >> 24);
		b[o + 1] = (byte)(v >> 16);
		b[o + 2] = (byte)(v >> 8);
		b[o + 3] = (byte)v;
	}

	private static void Chunk(Stream s, string type, byte[] data)
	{
		byte[] len = new byte[4];
		WriteInt(len, 0, data.Length);
		s.Write(len);
		byte[] t = Encoding.ASCII.GetBytes(type);
		s.Write(t);
		s.Write(data);
		uint crc = Crc(t, 0xFFFFFFFF);
		crc = Crc(data, crc) ^ 0xFFFFFFFF;
		byte[] c = new byte[4];
		WriteInt(c, 0, (int)crc);
		s.Write(c);
	}

	private static uint[]? _table;

	private static uint Crc(byte[] data, uint crc)
	{
		if (_table == null)
		{
			uint[] t = new uint[256];
			for (uint n = 0; n < 256; n++)
			{
				uint c = n;
				for (int k = 0; k < 8; k++)
				{
					c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
				}
				t[n] = c;
			}
			_table = t;
		}
		foreach (byte b in data)
		{
			crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
		}
		return crc;
	}
}
