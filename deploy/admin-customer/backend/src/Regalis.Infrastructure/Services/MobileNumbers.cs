using System;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Mobile numbers are stored as typed by the screens. Since 13 Nov 2026 the screens send a
/// country code plus digits ("+919876543210"); older rows hold the bare Indian 10 digits
/// ("9876543210"). Both mean the same phone, so duplicate checks look for either form.
/// Rows are not rewritten.
/// </summary>
public static class MobileNumbers
{
	public static string[] Aliases(string mobile)
	{
		string m = (mobile ?? string.Empty).Trim();
		if (m.Length == 13 && m.StartsWith("+91", StringComparison.Ordinal) && IsDigits(m.Substring(3)))
		{
			return new[] { m, m.Substring(3) };
		}
		if (m.Length == 10 && IsDigits(m))
		{
			return new[] { m, "+91" + m };
		}
		return new[] { m };
	}

	private static bool IsDigits(string s)
	{
		foreach (char c in s)
		{
			if (c < '0' || c > '9')
			{
				return false;
			}
		}
		return s.Length > 0;
	}
}
