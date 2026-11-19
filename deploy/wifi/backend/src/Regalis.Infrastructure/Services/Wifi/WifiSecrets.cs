using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>
/// Encrypts Wi-Fi passwords and the controller API key (spec 4.4, 7.2, 8): AES-256-GCM.
/// Key: configuration "Wifi:EncryptionKey" (base64, 32 bytes) when set; otherwise a random key
/// created once in {content root}/App_Data/keys/wifi-secrets.key. Either way the key is NOT in the
/// database, so a database backup alone cannot reveal a password. Losing the key means the stored
/// passwords cannot be read: an admin then uses "Reset password", which issues new ones.
/// </summary>
public sealed class WifiSecrets
{
	private const string Prefix = "v1:";
	private readonly byte[] _key;

	public WifiSecrets(IConfiguration configuration, IHostEnvironment env, ILogger<WifiSecrets> logger)
	{
		string? configured = configuration["Wifi:EncryptionKey"];
		if (!string.IsNullOrWhiteSpace(configured))
		{
			_key = Convert.FromBase64String(configured.Trim());
			if (_key.Length != 32)
			{
				throw new InvalidOperationException("Wifi:EncryptionKey must be 32 bytes, base64-encoded.");
			}
			return;
		}
		string dir = Path.Combine(env.ContentRootPath, "App_Data", "keys");
		string file = Path.Combine(dir, "wifi-secrets.key");
		if (File.Exists(file))
		{
			_key = Convert.FromBase64String(File.ReadAllText(file).Trim());
			return;
		}
		Directory.CreateDirectory(dir);
		_key = RandomNumberGenerator.GetBytes(32);
		File.WriteAllText(file, Convert.ToBase64String(_key));
		logger.LogWarning("Wi-Fi: created a new encryption key at {File}. Back it up, or set Wifi:EncryptionKey.", file);
	}

	public string Encrypt(string plain)
	{
		byte[] nonce = RandomNumberGenerator.GetBytes(12);
		byte[] data = Encoding.UTF8.GetBytes(plain);
		byte[] cipher = new byte[data.Length];
		byte[] tag = new byte[16];
		using (AesGcm aes = new AesGcm(_key, 16))
		{
			aes.Encrypt(nonce, data, cipher, tag);
		}
		return Prefix + Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray());
	}

	/// <summary>Null when the value cannot be read (wrong key, damaged value).</summary>
	public string? Decrypt(string? stored)
	{
		if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return null;
		}
		try
		{
			byte[] all = Convert.FromBase64String(stored.Substring(Prefix.Length));
			byte[] nonce = all[..12];
			byte[] tag = all[^16..];
			byte[] cipher = all[12..^16];
			byte[] plain = new byte[cipher.Length];
			using AesGcm aes = new AesGcm(_key, 16);
			aes.Decrypt(nonce, cipher, tag, plain);
			return Encoding.UTF8.GetString(plain);
		}
		catch (Exception)
		{
			return null;
		}
	}

	// ---- generators ----------------------------------------------------------------------------

	/// <summary>No 0/O, 1/I/L: easy to read aloud and type.</summary>
	private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

	private const string Lower = "abcdefghjkmnpqrstuvwxyz";

	public static string Random(string alphabet, int length)
	{
		char[] c = new char[length];
		for (int i = 0; i < length; i++)
		{
			c[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
		}
		return new string(c);
	}

	public static string Code(int length) => Random(Upper, length);

	/// <summary>"kaus-4T9W-21XQ": 4 letters of the first name, then 8 random characters (14 in all; spec 4.1).</summary>
	public static string NewPassword(string? name)
	{
		string letters = new string((name ?? string.Empty).ToLowerInvariant().Where(ch => ch >= 'a' && ch <= 'z').Take(4).ToArray());
		if (letters.Length < 4)
		{
			letters += Random(Lower, 4 - letters.Length);
		}
		return $"{letters}-{Random(Upper, 4)}-{Random(Upper, 4)}";
	}

	/// <summary>"kausika.rs" from "Kausika RS": first name, dot, the other initials or surname (letters and digits only).</summary>
	public static string UserBase(string? name, string? company)
	{
		string[] parts = (name ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(p => new string(p.Where(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')).ToArray())).Where(p => p.Length > 0).ToArray();
		string first = parts.Length > 0 ? parts[0] : "user";
		string rest = parts.Length > 1 ? string.Concat(parts.Skip(1)) : string.Empty;
		string co = new string((company ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Where(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')).ToArray() ?? Array.Empty<char>());
		string b = co.Length > 0 ? $"{co}.{first}" : (rest.Length > 0 ? $"{first}.{rest}" : first);
		return b.Length > 40 ? b.Substring(0, 40) : b;
	}

	/// <summary>Wi-Fi QR payload (the de-facto "WIFI:" format phones read).</summary>
	public static string QrText(string ssid, string? password, bool openNetwork)
	{
		static string E(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:").Replace("\"", "\\\"");
		return openNetwork || string.IsNullOrEmpty(password) ? $"WIFI:T:nopass;S:{E(ssid)};;" : $"WIFI:T:WPA;S:{E(ssid)};P:{E(password)};;";
	}
}
