using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>Rules shared by every Billing &amp; Finance service: the period lock, Indian
/// financial-year arithmetic, money formatting for messages, and file storage.</summary>
public static class FinanceRules
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly Regex PanPattern = new("^[A-Z]{5}[0-9]{4}[A-Z]$");
    private static readonly Regex TanPattern = new("^[A-Z]{4}[0-9]{5}[A-Z]$");

    public static DateOnly Today(IDateTimeProvider clock) => IstClock.IstDateOf(clock.UtcNow);

    /// <summary>"₹28,940" or "₹28,940.50" - Indian digit grouping, paise only when present.</summary>
    public static string Money(decimal amount)
    {
        decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        string format = rounded == Math.Truncate(rounded) ? "#,##,##0" : "#,##,##0.00";
        return (rounded < 0 ? "-₹" : "₹") + Math.Abs(rounded).ToString(format, India);
    }

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static string Period(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static string MonthName(string period) =>
        DateTime.ParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public static string FinancialYear(DateOnly date) => IndianMoney.FinancialYearOf(date);

    /// <summary>Apr-Jun = 1, Jul-Sep = 2, Oct-Dec = 3, Jan-Mar = 4.</summary>
    public static byte Quarter(DateOnly date) => (byte)(date.Month >= 4 ? (date.Month - 4) / 3 + 1 : 4);

    public static (DateOnly From, DateOnly To) QuarterRange(string financialYear, byte quarter)
    {
        int startYear = int.Parse(financialYear[..4], CultureInfo.InvariantCulture);
        DateOnly from = quarter == 4 ? new DateOnly(startYear + 1, 1, 1) : new DateOnly(startYear, 4 + (quarter - 1) * 3, 1);
        return (from, from.AddMonths(3).AddDays(-1));
    }

    public static bool IsPan(string? value) => value != null && PanPattern.IsMatch(value);
    public static bool IsTan(string? value) => value != null && TanPattern.IsMatch(value);

    /// <summary>Refuses a document dated in a locked month (doc §13.3).</summary>
    public static async Task EnsurePeriodOpenAsync(RegalisDbContext db, DateOnly date, CancellationToken ct, string what = "this")
    {
        string period = Period(date);
        bool locked = await db.Set<PeriodLock>().AsNoTracking().AnyAsync(p => p.Period == period && p.IsLocked, ct);
        if (locked)
        {
            string month = DateTime.ParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM", CultureInfo.InvariantCulture);
            throw new BusinessRuleException($"{month} is locked. Unlock it in Settings, or date {what} in an open period.", "PERIOD_LOCKED");
        }
    }

    /// <summary>Refuses a future date or one before an anchor date.</summary>
    public static void EnsureNotFuture(DateOnly date, IDateTimeProvider clock, string message)
    {
        if (date > Today(clock))
        {
            throw new BusinessRuleException(message, "DATE_IN_FUTURE");
        }
    }

    public static void Require(ICurrentUser user, string permission, string message)
    {
        if (!user.HasPermission(permission))
        {
            throw new ForbiddenException(message);
        }
    }

    /// <summary>Upper case, spaces removed - the duplicate key for supplier bill numbers.</summary>
    public static string BillNoKey(string billNo) => Regex.Replace(billNo.Trim().ToUpperInvariant(), "\\s+", "");

    /// <summary>The two-digit GST state code of a state name or code; null when unknown.</summary>
    public static string? StateCodeOf(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }
        string s = state.Trim();
        if (s.Length == 2 && char.IsDigit(s[0]) && char.IsDigit(s[1]))
        {
            return s;
        }
        return GstStates.All.FirstOrDefault(x => string.Equals(x.Name, s, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    public static string? GstinStateCode(string? gstin)
    {
        string? g = gstin?.Trim();
        return g != null && g.Length >= 2 && char.IsDigit(g[0]) && char.IsDigit(g[1]) ? g[..2] : null;
    }

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

    /// <summary>Stores an upload under UploadRoot/&lt;folder&gt;/&lt;id&gt;; PDF, JPG or PNG up to 10 MB,
    /// checked by extension and by the file's first bytes.</summary>
    public static async Task<(string StoredPath, string ContentType)> SaveUploadAsync(string uploadRoot, string folder, long ownerId,
        string fileName, Stream content, long length, CancellationToken ct)
    {
        if (length <= 0)
        {
            throw new BusinessRuleException("The file is empty.");
        }
        if (length > 10L * 1024 * 1024)
        {
            throw new BusinessRuleException("Files can be up to 10 MB each.");
        }
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            throw new BusinessRuleException("Attach a PDF, JPG or PNG.");
        }
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        byte[] bytes = buffer.ToArray();
        string contentType = Sniff(bytes) ?? throw new BusinessRuleException("That file is not a real PDF, JPG or PNG.");
        string dir = Path.Combine(uploadRoot, folder, ownerId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        string stored = Path.Combine(dir, $"{Guid.NewGuid():N}{ext}");
        await File.WriteAllBytesAsync(stored, bytes, ct);
        return (stored, contentType);
    }

    private static string? Sniff(byte[] b)
    {
        if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return "application/pdf";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        return null;
    }

    /// <summary>Runs work in one serializable transaction under the execution strategy.</summary>
    public static Task<T> InTransactionAsync<T>(RegalisDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            T result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
