using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Read side of the admin wallet screens. Every figure is summed from the append-only
/// ledger (TRN_WalletTransaction) in SQL — one GROUP BY per customer, LEFT JOINed to
/// MST_Wallet so a wallet with no activity still shows as ₹0 — so Balance is always
/// credits − debits and nothing depends on the stored MST_Wallet counters. The grouped
/// query uses IX_WalletTxn (CustomerId, TransactionUtc).
/// </summary>
public class WalletLedgerQueries(
    RegalisDbContext db,
    ISettingService settings,
    IDateTimeProvider clock) : IWalletLedgerQueries
{
    /// <summary>A flat row EF can filter, sort and aggregate on the server.</summary>
    public sealed class Row
    {
        public long CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public decimal Balance { get; set; }
        public DateTime? LastActivityUtc { get; set; }
    }

    public async Task<WalletListResultDto> GetWalletsAsync(WalletListRequest request,
        CancellationToken ct = default)
    {
        var threshold = await LowBalanceThresholdAsync(ct);
        var all = Rows();
        var filtered = Filter(all, request, threshold);

        var totalCount = await filtered.CountAsync(ct);
        var page = await Sort(filtered, request.Sort)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = page.Select(r => new WalletListItemDto(r.CustomerId, r.FullName, r.Email,
            r.MobileNumber, r.Credit, r.Debit, r.Balance, r.LastActivityUtc, r.Balance < threshold)).ToList();

        var summary = new WalletSummaryDto(
            BalanceHeld: await all.SumAsync(r => r.Balance, ct),
            TotalRecharged: await all.SumAsync(r => r.Credit, ct),
            TotalSpent: await all.SumAsync(r => r.Debit, ct),
            CustomerCount: await all.CountAsync(ct),
            LowBalanceCount: await all.CountAsync(r => r.Balance < threshold, ct),
            LowBalanceThreshold: threshold);

        var totals = new WalletTotalsDto(
            totalCount,
            await filtered.SumAsync(r => r.Credit, ct),
            await filtered.SumAsync(r => r.Debit, ct),
            await filtered.SumAsync(r => r.Balance, ct));

        return new WalletListResultDto(
            PagedResult<WalletListItemDto>.Create(items, request.Page, request.PageSize, totalCount),
            summary, totals);
    }

    public async Task<byte[]> ExportWalletsAsync(WalletListRequest request, CancellationToken ct = default)
    {
        var threshold = await LowBalanceThresholdAsync(ct);
        var rows = await Sort(Filter(Rows(), request, threshold), request.Sort).Take(10_000).ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Customer,Email,Mobile,Total incoming,Total spent,Current balance,Low balance,Last activity (IST)");
        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(",",
                Cell(r.FullName), Cell(r.Email), Cell(r.MobileNumber),
                Money(r.Credit), Money(r.Debit), Money(r.Balance),
                r.Balance < threshold ? "Yes" : "No",
                r.LastActivityUtc is null ? "" : IstClock.ToIst(r.LastActivityUtc.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        }
        csv.AppendLine(string.Join(",", Cell($"Totals ({rows.Count} customers)"), "", "",
            Money(rows.Sum(r => r.Credit)), Money(rows.Sum(r => r.Debit)), Money(rows.Sum(r => r.Balance)), "", ""));
        // BOM so Excel opens the ₹-free UTF-8 file with names intact.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    // ── query pieces ────────────────────────────────────────────────────────

    /// <summary>
    /// One row per customer wallet. The ledger is summed once, grouped by customer, and
    /// LEFT JOINed so a wallet with no entries still appears at 0. Written as SQL because
    /// EF cannot sort or re-aggregate over a LINQ left join to a grouped subquery; EF 8
    /// composes the filters, ORDER BY, paging and SUMs around it as a derived table.
    /// </summary>
    private IQueryable<Row> Rows() => db.Database.SqlQuery<Row>($"""
        SELECT w.CustomerId, u.FullName, u.Email, u.MobileNumber,
               COALESCE(l.Credit, 0) AS Credit,
               COALESCE(l.Debit, 0) AS Debit,
               COALESCE(l.Credit, 0) - COALESCE(l.Debit, 0) AS Balance,
               l.LastActivityUtc
        FROM MST_Wallet w
        JOIN MST_User u ON u.UserId = w.CustomerId AND u.IsDeleted = 0
        LEFT JOIN (
            SELECT t.CustomerId,
                   SUM(CASE WHEN t.Direction = 1 THEN t.Amount ELSE 0 END) AS Credit,
                   SUM(CASE WHEN t.Direction = 2 THEN t.Amount ELSE 0 END) AS Debit,
                   MAX(t.TransactionUtc) AS LastActivityUtc
            FROM TRN_WalletTransaction t
            WHERE t.IsDeleted = 0
            GROUP BY t.CustomerId
        ) l ON l.CustomerId = w.CustomerId
        WHERE w.IsDeleted = 0
        """);

    private IQueryable<Row> Filter(IQueryable<Row> rows, WalletListRequest request, decimal threshold)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(r => r.FullName.Contains(term) || r.Email.Contains(term)
                                   || (r.MobileNumber != null && r.MobileNumber.Contains(term)));
        }
        rows = (request.Filter ?? "all").ToLowerInvariant() switch
        {
            "with-balance" => rows.Where(r => r.Balance > 0),
            "zero" => rows.Where(r => r.Balance == 0),
            "low" => rows.Where(r => r.Balance < threshold),
            _ => rows
        };
        var now = clock.UtcNow;
        rows = (request.Activity ?? "all").ToLowerInvariant() switch
        {
            "30d" => rows.Where(r => r.LastActivityUtc >= now.AddDays(-30)),
            "90d" => rows.Where(r => r.LastActivityUtc >= now.AddDays(-90)),
            "inactive-90d" => rows.Where(r => r.LastActivityUtc == null || r.LastActivityUtc < now.AddDays(-90)),
            _ => rows
        };
        return rows;
    }

    private static IOrderedQueryable<Row> Sort(IQueryable<Row> rows, string? sort) =>
        (sort ?? "balance-desc").ToLowerInvariant() switch
        {
            "balance-asc" => rows.OrderBy(r => r.Balance).ThenBy(r => r.FullName),
            "name" => rows.OrderBy(r => r.FullName).ThenBy(r => r.CustomerId),
            "last-activity" => rows.OrderByDescending(r => r.LastActivityUtc).ThenBy(r => r.FullName),
            "spent" => rows.OrderByDescending(r => r.Debit).ThenBy(r => r.FullName),
            _ => rows.OrderByDescending(r => r.Balance).ThenBy(r => r.FullName)
        };

    private async Task<decimal> LowBalanceThresholdAsync(CancellationToken ct) =>
        await settings.GetDecimalAsync(AppSettingKeys.Wallet.LowBalanceThreshold, 100m, ct);

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Cell(string? value)
    {
        var text = value ?? "";
        // Neutralise spreadsheet formulas and quote anything with separators.
        if (text.Length > 0 && "=+-@".Contains(text[0]))
        {
            text = "'" + text;
        }
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
