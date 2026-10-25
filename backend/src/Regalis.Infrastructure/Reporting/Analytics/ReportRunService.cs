using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Reporting;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>The report catalogue (spec §6): one definition per report, in four groups.</summary>
internal static class ReportRegistry
{
    public static readonly IReadOnlyList<ReportHandler> All =
    [
        new BookingReport(),
        new CancellationsReport(),
        new MeetingRoomUsageReport(),
        new DayPassSalesReport(),
        new EventBookingsReport(),
        new BookingSourceReport(),
        new RevenueByMonthReport(),
        new RevenueByCategoryReport(),
        new GstSummaryReport(),
        new InvoiceRegisterReport(),
        new PaymentsReceivedReport(),
        new MoneyOwedReport(),
        new RefundsReport(),
        new CustomerListReport(),
        new TopCustomersReport(),
        new NewSignupsReport(),
        new GoneQuietReport(),
        new WalletBalancesReport(),
        new DeskOccupancyReport(),
        new BusyHoursReport(),
        new CheckInLogReport(),
        new MaintenanceTicketsReport(),
        new AssetRegisterReport(),
        new PurchaseOrdersReport()
    ];

    public static readonly List<ReportGroupDto> Groups =
    [
        new("bookings", "Bookings", "Every booking, cancellation and pass sold, and where they came from.", 1),
        new("revenue", "Revenue & tax", "Money received, invoiced and still owed, and the tax on it.", 2),
        new("customers", "Customers", "Who the customers are, who spends the most and who has gone quiet.", 3),
        new("operations", "Occupancy & operations", "How full the space is, and the work that keeps it running.", 4)
    ];

    /// <summary>Shown as "Most used" before anyone has opened anything.</summary>
    public static readonly string[] Starters = ["booking", "revenue-by-month", "money-owed", "desk-occupancy"];

    public static ReportHandler? Find(string key) =>
        All.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every report needs REPORT.VIEW plus the permission of its group (spec §13).</summary>
    public static bool CanSee(ICurrentUser user, ReportHandler handler) =>
        user.HasPermission(PermissionCodes.Report.View) && user.HasPermission(handler.Permission);

    public static bool CanSee(IReadOnlySet<string> permissions, ReportHandler handler) =>
        permissions.Contains(PermissionCodes.Report.View) && permissions.Contains(handler.Permission);
}

internal static class ReportJson
{
    public static string Serialize(Dictionary<string, List<string>> values) => JsonSerializer.Serialize(values);

    public static Dictionary<string, List<string>> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? [];
            return new Dictionary<string, List<string>>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static List<string>? Columns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Running, exporting and finding reports. Reads only — the only rows it
/// writes are favourites, preferences and the export log (spec §1 NOTE).</summary>
internal sealed class ReportRunService(
    RegalisDbContext db,
    ICurrentUser user,
    IDateTimeProvider clock,
    ReportFileBuilder files) : IReportRunService, IReportCatalogService
{
    public const int PageSize = 50;
    public const int DirectExportLimit = 5000;

    private DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

    private ReportHandler Resolve(string key)
    {
        var handler = ReportRegistry.Find(key) ?? throw new NotFoundException("Report", key);
        if (!ReportRegistry.CanSee(user, handler))
        {
            throw new ForbiddenException("You do not have access to this report.");
        }
        return handler;
    }

    private async Task<(ReportRunContext Ctx, ReportLookups Lookups)> ContextAsync(ReportHandler handler,
        Dictionary<string, List<string>> raw, CancellationToken ct)
    {
        var lookups = await ReportLookups.LoadAsync(db, ct);
        var (from, to, values) = ReportFilterParser.Resolve(handler, lookups, raw, Today);
        return (new ReportRunContext { Db = db, From = from, To = to, Today = Today, Values = values, Lookups = lookups },
            lookups);
    }

    public async Task<ReportRunResult> RunAsync(string reportKey, ReportRunRequest request, CancellationToken ct = default)
    {
        var handler = Resolve(reportKey);
        var preference = await db.ReportPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.UserId && p.ReportKey == handler.Key, ct);

        // Opening a report with no filters picks up where this admin left it — except
        // the dates, which go back to the default range rather than a stale window.
        Dictionary<string, List<string>> raw;
        if (request.Filters is null)
        {
            raw = ReportJson.Deserialize(preference?.FiltersJson);
            raw.Remove("from");
            raw.Remove("to");
        }
        else
        {
            raw = ReportFilterParser.Normalise(request.Filters);
        }
        var (ctx, lookups) = await ContextAsync(handler, raw, ct);
        var paging = new ReportPaging(Math.Max(1, request.Page), PageSize,
            request.Sort ?? preference?.SortJson ?? handler.DefaultSort, request.Search);
        var output = await handler.RunAsync(ctx, paging, ct);
        var chips = await ReportChips.BuildAsync(db, handler, lookups, ctx.Values, ctx.From, ctx.To, Today, ct);

        var now = clock.UtcNow;
        if (preference is null)
        {
            preference = new ReportPreference { UserId = user.UserId, ReportKey = handler.Key };
            db.ReportPreferences.Add(preference);
        }
        preference.FiltersJson = ReportJson.Serialize(ctx.Values);
        preference.SortJson = output.Sort;
        if (string.IsNullOrWhiteSpace(request.Search))
        {
            preference.LastRowCount = output.TotalRows;
        }
        if (request.Opened)
        {
            preference.OpenCount++;
            preference.LastOpenedUtc = now;
        }
        preference.UpdatedUtc = now;
        await db.SaveChangesAsync(ct);

        var (previousFrom, previousTo) = ReportMath.PreviousPeriod(ctx.From, ctx.To);
        return new ReportRunResult(handler.Key, handler.Name, ctx.From, ctx.To, previousFrom, previousTo,
            output.Sentence, chips, output.Summary, output.Charts, handler.ColumnDefinitions, output.Rows,
            output.Totals, output.TotalRows, paging.Page, PageSize, output.Sort, handler.RowNoun, ctx.Values,
            ReportJson.Columns(preference.ColumnsJson));
    }

    public async Task<ReportExportOutcome> ExportAsync(string reportKey, ReportExportRequest request,
        string? portalBaseUrl, CancellationToken ct = default)
    {
        // The buttons are hidden without the permission; the endpoint refuses anyway.
        if (!user.HasPermission(PermissionCodes.Report.Export))
        {
            throw new ForbiddenException("You do not have permission to export reports.");
        }
        var handler = Resolve(reportKey);
        var format = ParseFormat(request.Format);
        var (ctx, lookups) = await ContextAsync(handler, ReportFilterParser.Normalise(request.Filters), ct);
        var sort = request.Sort ?? handler.DefaultSort;
        var (count, _) = await handler.TotalsAsync(ctx, ct);
        var filtersJson = ReportJson.Serialize(ctx.Values);
        var now = clock.UtcNow;

        if (count > DirectExportLimit)
        {
            db.ReportExportJobs.Add(new ReportExportJob
            {
                UserId = user.UserId,
                ReportKey = handler.Key,
                FiltersJson = filtersJson,
                Format = format,
                Status = ReportExportJobStatus.Queued,
                RowCount = count,
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
                PortalBaseUrl = portalBaseUrl,
                CreatedUtc = now
            });
            db.ReportExports.Add(new ReportExport
            {
                UserId = user.UserId, ReportKey = handler.Key, FiltersJson = filtersJson, RowCount = count,
                Format = format, Delivery = ReportExportDelivery.EmailedLink, CreatedUtc = now
            });
            await db.SaveChangesAsync(ct);
            return new ReportExportOutcome(null, null, null, true, count,
                $"This export has {count:N0} rows. We will email it to you when it is ready.");
        }

        var chips = await ReportChips.BuildAsync(db, handler, lookups, ctx.Values, ctx.From, ctx.To, Today, ct);
        var columns = ReportJson.Columns(await db.ReportPreferences.AsNoTracking()
            .Where(p => p.UserId == user.UserId && p.ReportKey == handler.Key)
            .Select(p => p.ColumnsJson).FirstOrDefaultAsync(ct));
        var built = await files.BuildAsync(handler, ctx, format, sort, ExportedBy(), ReportChips.Sentence(ctx.From, ctx.To, chips),
            columns, now, ct);
        db.ReportExports.Add(new ReportExport
        {
            UserId = user.UserId, ReportKey = handler.Key, FiltersJson = filtersJson, RowCount = built.RowCount,
            Format = format, Delivery = ReportExportDelivery.Download, CreatedUtc = now
        });
        await db.SaveChangesAsync(ct);
        return new ReportExportOutcome(built.Content, built.ContentType, built.FileName, false, built.RowCount, null);
    }

    public async Task<ReportDownload> DownloadAsync(string token, CancellationToken ct = default)
    {
        var job = await db.ReportExportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Token == token, ct);
        if (job is null || (job.UserId != user.UserId && !user.HasPermission(PermissionCodes.Report.Manage)))
        {
            throw new NotFoundException("Export", token);
        }
        if (job.Status == ReportExportJobStatus.Queued)
        {
            throw new BusinessRuleException("This export is still being built. We will email you when it is ready.");
        }
        if (job.Status != ReportExportJobStatus.Ready || job.ExpiresUtc < clock.UtcNow || job.FilePath is null ||
            !File.Exists(job.FilePath))
        {
            throw new BusinessRuleException("This download link has expired. Export the report again.");
        }
        var contentType = job.Format == ReportFormat.Pdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return new ReportDownload(await File.ReadAllBytesAsync(job.FilePath, ct), contentType, job.FileName ?? "report");
    }

    private string ExportedBy() => string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? $"User {user.UserId}" : user.FullName!;

    internal static ReportFormat ParseFormat(string? format) => format?.ToLowerInvariant() switch
    {
        "pdf" => ReportFormat.Pdf,
        "xlsx" or "excel" or null or "" => ReportFormat.Excel,
        _ => throw new BusinessRuleException("The format must be Excel or PDF.")
    };

    // ── The library ─────────────────────────────────────────────────────────

    public async Task<ReportCatalogueDto> GetCatalogueAsync(CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.Report.View))
        {
            throw new ForbiddenException("You do not have access to Reports & Analysis.");
        }
        var visible = ReportRegistry.All.Where(h => ReportRegistry.CanSee(user, h)).ToList();
        var keys = visible.Select(h => h.Key).ToList();
        var lookups = await ReportLookups.LoadAsync(db, ct);
        var favourites = await db.ReportFavourites.AsNoTracking()
            .Where(f => f.UserId == user.UserId).OrderBy(f => f.CreatedUtc).Select(f => f.ReportKey).ToListAsync(ct);
        var preferences = await db.ReportPreferences.AsNoTracking()
            .Where(p => p.UserId == user.UserId).ToDictionaryAsync(p => p.ReportKey, ct);
        var nextEmails = await db.ReportSchedules.AsNoTracking()
            .Where(s => s.IsActive && keys.Contains(s.ReportKey))
            .GroupBy(s => s.ReportKey).Select(g => new { Key = g.Key, Next = g.Min(s => s.NextRunUtc) })
            .ToDictionaryAsync(g => g.Key, g => g.Next, ct);

        var ctx = new ReportRunContext
        {
            Db = db, From = Today, To = Today, Today = Today,
            Values = new Dictionary<string, List<string>>(), Lookups = lookups
        };
        var reports = new List<ReportDefinitionDto>();
        foreach (var handler in visible)
        {
            string? live = null;
            try
            {
                live = await handler.LiveFigureAsync(ctx, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A live figure is a nicety; the library still lists the report without it.
            }
            preferences.TryGetValue(handler.Key, out var preference);
            reports.Add(new ReportDefinitionDto(handler.Key, handler.Name, handler.Description, handler.Group,
                handler.Chip, [.. handler.Keywords], handler.RowNoun, handler.DefaultSort, handler.DefaultRange,
                handler.Hint, live, handler.Filters(lookups), handler.ColumnDefinitions,
                favourites.Contains(handler.Key), preference?.LastOpenedUtc, preference?.LastRowCount,
                nextEmails.TryGetValue(handler.Key, out var next) ? next : null));
        }

        var pinned = favourites.Where(keys.Contains).Take(4).ToList();
        var mostUsed = pinned.Count == 0;
        if (mostUsed)
        {
            var opened = await db.ReportPreferences.AsNoTracking()
                .Where(p => keys.Contains(p.ReportKey) && p.OpenCount > 0)
                .GroupBy(p => p.ReportKey).Select(g => new { Key = g.Key, Opens = g.Sum(p => p.OpenCount) })
                .OrderByDescending(g => g.Opens).Take(4).Select(g => g.Key).ToListAsync(ct);
            pinned = opened.Concat(ReportRegistry.Starters.Where(keys.Contains)).Distinct().Take(4).ToList();
        }

        return new ReportCatalogueDto(ReportRegistry.Groups, reports, pinned, mostUsed, lookups.Locations,
            user.HasPermission(PermissionCodes.Report.Export), user.HasPermission(PermissionCodes.Report.Manage));
    }

    public async Task<List<string>> GetFavouritesAsync(CancellationToken ct = default) =>
        await db.ReportFavourites.AsNoTracking().Where(f => f.UserId == user.UserId)
            .OrderBy(f => f.CreatedUtc).Select(f => f.ReportKey).ToListAsync(ct);

    public async Task AddFavouriteAsync(string reportKey, CancellationToken ct = default)
    {
        var handler = Resolve(reportKey);
        if (!await db.ReportFavourites.AnyAsync(f => f.UserId == user.UserId && f.ReportKey == handler.Key, ct))
        {
            db.ReportFavourites.Add(new ReportFavourite
            {
                UserId = user.UserId, ReportKey = handler.Key, CreatedUtc = clock.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveFavouriteAsync(string reportKey, CancellationToken ct = default) =>
        await db.ReportFavourites.Where(f => f.UserId == user.UserId && f.ReportKey == reportKey)
            .ExecuteDeleteAsync(ct);

    public async Task SavePreferencesAsync(string reportKey, ReportPreferenceRequest request, CancellationToken ct = default)
    {
        var handler = Resolve(reportKey);
        var known = handler.ColumnDefinitions.Select(c => c.Key).ToHashSet();
        var columns = request.Columns?.Where(known.Contains).Distinct().ToList();
        var preference = await db.ReportPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.UserId && p.ReportKey == handler.Key, ct);
        if (preference is null)
        {
            preference = new ReportPreference { UserId = user.UserId, ReportKey = handler.Key };
            db.ReportPreferences.Add(preference);
        }
        preference.ColumnsJson = columns is { Count: > 0 } ? JsonSerializer.Serialize(columns) : null;
        preference.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ReportCustomerOption>> SearchCustomersAsync(string? query, CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.Report.View))
        {
            throw new ForbiddenException("You do not have access to Reports & Analysis.");
        }
        var term = query?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < 2)
        {
            return [];
        }
        return await db.Users.AsNoTracking()
            .Where(u => u.UserType == UserType.Customer &&
                        (u.FullName.Contains(term) || (u.CompanyName != null && u.CompanyName.Contains(term)) ||
                         u.MobileNumber.Contains(term) || u.Email.Contains(term)))
            .OrderBy(u => u.FullName).Take(10)
            .Select(u => new ReportCustomerOption(u.UserId, u.FullName, u.CompanyName, u.MobileNumber, u.Email))
            .ToListAsync(ct);
    }
}
