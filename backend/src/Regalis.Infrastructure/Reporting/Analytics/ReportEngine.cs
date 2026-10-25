using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>Option lists the filter bars are drawn from, loaded once per request.</summary>
internal sealed class ReportLookups
{
    public required List<ReportOption> Locations { get; init; }
    public required List<ReportOption> WorkspaceTypes { get; init; }
    public required List<ReportOption> MeetingRooms { get; init; }
    public required List<ReportOption> DayPassPlans { get; init; }
    public required List<ReportOption> Suppliers { get; init; }
    public required List<ReportOption> AssetCategories { get; init; }
    public required ReportCategories Categories { get; init; }

    public static async Task<ReportLookups> LoadAsync(RegalisDbContext db, CancellationToken ct)
    {
        var categories = await ReportCategories.LoadAsync(db, ct);
        return new ReportLookups
        {
            Locations = await db.Locations.AsNoTracking().OrderBy(l => l.LocationName)
                .Select(l => new ReportOption(l.LocationId.ToString(), l.LocationName)).ToListAsync(ct),
            WorkspaceTypes = await db.WorkspaceTypes.AsNoTracking()
                .Where(t => t.Workspaces.Any())
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.WorkspaceTypeId)
                .Select(t => new ReportOption(t.WorkspaceTypeId.ToString(), t.TypeName)).ToListAsync(ct),
            MeetingRooms = await db.Workspaces.AsNoTracking()
                .Where(w => w.WorkspaceType.TypeCode == "MEETING_ROOM")
                .OrderBy(w => w.WorkspaceCode)
                .Select(w => new ReportOption(w.WorkspaceId.ToString(), w.WorkspaceName)).ToListAsync(ct),
            DayPassPlans = await db.PassPlans.AsNoTracking()
                .Where(p => p.PassType == Domain.Enums.PassType.DayPass)
                .OrderBy(p => p.PlanName)
                .Select(p => new ReportOption(p.PassPlanId.ToString(), p.PlanName)).ToListAsync(ct),
            Suppliers = await db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierName)
                .Select(s => new ReportOption(s.SupplierId.ToString(), s.SupplierName)).ToListAsync(ct),
            AssetCategories = await db.AssetCategories.AsNoTracking().OrderBy(c => c.DisplayOrder)
                .Select(c => new ReportOption(c.AssetCategoryId.ToString(), c.CategoryName)).ToListAsync(ct),
            Categories = categories
        };
    }
}

/// <summary>The filter controls a report can offer.</summary>
internal static class Filters
{
    public static ReportFilterDefinition Dates() => new("dates", "Date range", "dateRange", [], []);

    public static ReportFilterDefinition Location(ReportLookups lookups) =>
        new("location", "Location", "location", lookups.Locations, [], "All locations");

    public static ReportFilterDefinition Customer() => new("customer", "Customer", "customer", [], [], "Any customer");

    public static ReportFilterDefinition Multi(string key, string label, List<ReportOption> options,
        List<string>? defaults = null, string allLabel = "All") =>
        new(key, label, "multi", options, defaults ?? [], allLabel);

    public static ReportFilterDefinition Select(string key, string label, List<ReportOption> options,
        string? defaultValue = null, string allLabel = "All") =>
        new(key, label, "select", options, defaultValue is null ? [] : [defaultValue], allLabel);

    public static ReportFilterDefinition Toggle(string key, string label, bool on = false) =>
        new(key, label, "toggle", [], on ? ["true"] : []);

    public static List<ReportOption> Options(params (string Value, string Label)[] options) =>
        options.Select(o => new ReportOption(o.Value, o.Label)).ToList();
}

/// <summary>Everything a report query needs: the period, the filter values (defaults
/// already filled in), today in IST, and the database.</summary>
internal sealed class ReportRunContext
{
    public required RegalisDbContext Db { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required DateOnly Today { get; init; }
    public required Dictionary<string, List<string>> Values { get; init; }
    public required ReportLookups Lookups { get; init; }

    public ReportCategories Categories => Lookups.Categories;
    public DateTime FromUtc => ReportMath.IstStartUtc(From);
    public DateTime ToUtc => ReportMath.IstStartUtc(To.AddDays(1));
    public int Days => ReportMath.DaysIn(From, To);

    public List<string> Multi(string key) => Values.GetValueOrDefault(key) ?? [];
    public string? One(string key) => Multi(key).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    public bool Flag(string key) => string.Equals(One(key), "true", StringComparison.OrdinalIgnoreCase);

    public int? Int(string key) => int.TryParse(One(key), out var value) ? value : null;
    public long? Long(string key) => long.TryParse(One(key), out var value) ? value : null;

    public List<int> Ints(string key) =>
        Multi(key).Select(v => int.TryParse(v, out var i) ? (int?)i : null).Where(i => i is not null)
            .Select(i => i!.Value).ToList();

    public int? LocationId => Int("location");
    public long? CustomerId => Long("customer");

    public ReportRunContext WithRange(DateOnly from, DateOnly to) => new()
    {
        Db = Db, From = from, To = to, Today = Today, Values = Values, Lookups = Lookups
    };

    public ReportRunContext Previous()
    {
        var (from, to) = ReportMath.PreviousPeriod(From, To);
        return WithRange(from, to);
    }
}

internal sealed record ReportPaging(int Page, int PageSize, string Sort, string? Search);

internal sealed record ReportOutput(
    string Sentence,
    List<SummaryFigureDto> Summary,
    List<ChartDto> Charts,
    List<Dictionary<string, object?>> Rows,
    Dictionary<string, object?> Totals,
    int TotalRows,
    string Sort);

/// <summary>A summary figure before the engine adds its comparison.</summary>
internal sealed record Figure(string Key, string Label, decimal? Value, string Format, bool UpIsGood = true,
    bool Compare = true, string? Sub = null);

internal enum Total
{
    None,
    Sum,
    Custom
}

/// <summary>A column: Property is the row property it reads (the key is its camelCase).</summary>
internal sealed record Col(string Property, string Header, string Type, Total Total = Total.None,
    bool Sortable = true, bool Visible = true, bool Searchable = false)
{
    public string Key => char.ToLowerInvariant(Property[0]) + Property[1..];
}

/// <summary>
/// A report definition: what the library lists, the filters it offers, its columns
/// and default sort, and the permission that shows it (spec §6 NOTE). Adding a report
/// is adding one of these — not a screen.
/// </summary>
internal abstract class ReportHandler
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    /// <summary>bookings | revenue | customers | operations.</summary>
    public abstract string Group { get; }
    /// <summary>The library's category chip: the group, or occupancy / operations.</summary>
    public virtual string Chip => Group;
    public abstract string[] Keywords { get; }
    public abstract string Permission { get; }
    public abstract string RowNoun { get; }
    /// <summary>last30 | all — "all" for registers that are not about a period.</summary>
    public virtual string DefaultRange => "last30";
    public abstract string DefaultSort { get; }
    public virtual string? Hint => null;

    public abstract List<ReportFilterDefinition> Filters(ReportLookups lookups);
    public abstract List<ReportColumnDefinition> ColumnDefinitions { get; }

    public abstract Task<ReportOutput> RunAsync(ReportRunContext ctx, ReportPaging paging, CancellationToken ct);

    /// <summary>Every matching row, in order, for an export — streamed in batches.</summary>
    public abstract IAsyncEnumerable<Dictionary<string, object?>> AllRowsAsync(ReportRunContext ctx, string sort,
        CancellationToken ct);

    /// <summary>The totals row and the row count, without loading a page.</summary>
    public abstract Task<(int Count, Dictionary<string, object?> Totals)> TotalsAsync(ReportRunContext ctx,
        CancellationToken ct);

    public abstract Task<(List<SummaryFigureDto> Summary, List<ChartDto> Charts)> SummaryAndChartsAsync(
        ReportRunContext ctx, CancellationToken ct);

    public abstract string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals);

    /// <summary>A live figure for the library row ("₹1,20,000 owed"), or null for the hint.</summary>
    public virtual Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct) =>
        Task.FromResult<string?>(null);
}

/// <summary>
/// The generic half of every report. Rows come from one IQueryable with the filters
/// already applied in SQL (or, for reports whose rows are themselves aggregates, from
/// a server-side list); the engine sorts, searches, counts, totals and pages over it.
/// Totals and summary figures are aggregate queries over every matching row — never
/// the sum of the page (spec §8.1).
/// </summary>
internal abstract class ReportHandler<TRow> : ReportHandler where TRow : class
{
    private static readonly PropertyInfo? IdProperty = typeof(TRow).GetProperty("Id");

    protected abstract List<Col> Cols { get; }

    /// <summary>Rows matching the filters.</summary>
    protected abstract Task<IQueryable<TRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct);

    protected abstract Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<TRow> rows, CancellationToken ct);

    protected virtual Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<TRow> rows,
        CancellationToken ct) => Task.FromResult(new List<ChartDto>());

    /// <summary>Totals the engine cannot SUM in SQL (ratios, subquery columns).</summary>
    protected virtual Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<TRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct) => Task.CompletedTask;

    protected virtual string? LinkFor(TRow row) => null;

    public override List<ReportColumnDefinition> ColumnDefinitions =>
        Cols.Select(c => new ReportColumnDefinition(c.Key, c.Header, c.Type, c.Sortable, c.Total != Total.None,
            c.Visible)).ToList();

    public override async Task<ReportOutput> RunAsync(ReportRunContext ctx, ReportPaging paging, CancellationToken ct)
    {
        var rows = await QueryAsync(ctx, ct);
        var searched = ApplySearch(rows, paging.Search);
        var count = await CountAsync(searched, ct);
        var totals = await TotalsOfAsync(ctx, searched, ct);
        var (sorted, sort) = ApplySort(searched, paging.Sort);
        var page = await ToListAsync(sorted.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), ct);
        var (summary, charts) = await SummaryAndChartsAsync(ctx, rows, ct);

        // The sentence and the figures describe the filtered result, not the search.
        var resultCount = string.IsNullOrWhiteSpace(paging.Search) ? count : await CountAsync(rows, ct);
        var resultTotals = string.IsNullOrWhiteSpace(paging.Search) ? totals : await TotalsOfAsync(ctx, rows, ct);
        return new ReportOutput(SentenceFor(ctx, resultCount, resultTotals), summary, charts,
            page.Select(ToDictionary).ToList(), totals, count, sort);
    }

    public override async Task<(int Count, Dictionary<string, object?> Totals)> TotalsAsync(ReportRunContext ctx,
        CancellationToken ct)
    {
        var rows = await QueryAsync(ctx, ct);
        return (await CountAsync(rows, ct), await TotalsOfAsync(ctx, rows, ct));
    }

    public override async Task<(List<SummaryFigureDto> Summary, List<ChartDto> Charts)> SummaryAndChartsAsync(
        ReportRunContext ctx, CancellationToken ct) =>
        await SummaryAndChartsAsync(ctx, await QueryAsync(ctx, ct), ct);

    private async Task<(List<SummaryFigureDto>, List<ChartDto>)> SummaryAndChartsAsync(ReportRunContext ctx,
        IQueryable<TRow> rows, CancellationToken ct)
    {
        var figures = await FiguresAsync(ctx, rows, ct);
        List<Figure>? previous = null;
        if (figures.Any(f => f.Compare))
        {
            var previousCtx = ctx.Previous();
            previous = await FiguresAsync(previousCtx, await QueryAsync(previousCtx, ct), ct);
        }
        var summary = figures.Select(f =>
        {
            var before = f.Compare ? previous?.FirstOrDefault(p => p.Key == f.Key)?.Value : null;
            var (kind, change) = f.Compare ? ReportMath.Compare(f.Value, before) : (null, null);
            return new SummaryFigureDto(f.Key, f.Label, f.Value, f.Format, before, kind, change, f.UpIsGood, f.Sub);
        }).ToList();
        return (summary, await ChartsAsync(ctx, rows, ct));
    }

    public override async IAsyncEnumerable<Dictionary<string, object?>> AllRowsAsync(ReportRunContext ctx,
        string sort, [EnumeratorCancellation] CancellationToken ct)
    {
        var (sorted, _) = ApplySort(await QueryAsync(ctx, ct), sort);
        const int batch = 1000;
        for (var skip = 0; ; skip += batch)
        {
            var rows = await ToListAsync(sorted.Skip(skip).Take(batch), ct);
            foreach (var row in rows)
            {
                yield return ToDictionary(row);
            }
            if (rows.Count < batch)
            {
                yield break;
            }
        }
    }

    private async Task<Dictionary<string, object?>> TotalsOfAsync(ReportRunContext ctx, IQueryable<TRow> rows,
        CancellationToken ct)
    {
        var totals = new Dictionary<string, object?>();
        foreach (var col in Cols.Where(c => c.Total == Total.Sum))
        {
            totals[col.Key] = await SumAsync(rows, col.Property, ct);
        }
        await CustomTotalsAsync(ctx, rows, totals, ct);
        return totals;
    }

    protected Dictionary<string, object?> ToDictionary(TRow row)
    {
        var dict = new Dictionary<string, object?>(Cols.Count + 2);
        foreach (var col in Cols)
        {
            dict[col.Key] = typeof(TRow).GetProperty(col.Property)!.GetValue(row);
        }
        if (IdProperty is not null)
        {
            dict["_id"] = IdProperty.GetValue(row);
        }
        var link = LinkFor(row);
        if (link is not null)
        {
            dict["_link"] = link;
        }
        return dict;
    }

    private IQueryable<TRow> ApplySearch(IQueryable<TRow> rows, string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return rows;
        }
        var parameter = Expression.Parameter(typeof(TRow), "r");
        Expression? body = null;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var inMemory = rows.Provider is not IAsyncQueryProvider;
        foreach (var col in Cols.Where(c => c.Searchable))
        {
            var property = Expression.Property(parameter, col.Property);
            if (property.Type != typeof(string))
            {
                continue;
            }
            Expression target = property;
            Expression needle = Expression.Constant(term);
            if (inMemory)
            {
                // LINQ to objects is case-sensitive; SQL Server's collation is not.
                var lower = typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)!;
                target = Expression.Call(property, lower);
                needle = Expression.Constant(term.ToLowerInvariant());
            }
            var test = Expression.AndAlso(
                Expression.NotEqual(property, Expression.Constant(null, typeof(string))),
                Expression.Call(target, contains, needle));
            body = body is null ? test : Expression.OrElse(body, test);
        }
        return body is null ? rows : rows.Where(Expression.Lambda<Func<TRow, bool>>(body, parameter));
    }

    private (IQueryable<TRow> Rows, string Sort) ApplySort(IQueryable<TRow> rows, string? sort)
    {
        var (column, descending) = ParseSort(sort);
        if (column is null)
        {
            (column, descending) = ParseSort(DefaultSort);
        }
        var parameter = Expression.Parameter(typeof(TRow), "r");
        var property = Expression.Property(parameter, column!.Property);
        var keySelector = Expression.Lambda(property, parameter);
        var method = descending ? "OrderByDescending" : "OrderBy";
        var ordered = (IOrderedQueryable<TRow>)rows.Provider.CreateQuery<TRow>(Expression.Call(typeof(Queryable),
            method, [typeof(TRow), property.Type], rows.Expression, Expression.Quote(keySelector)));
        if (IdProperty is not null && column.Property != "Id")
        {
            // A stable tie-break, so a row never appears on two pages.
            var id = Expression.Property(parameter, IdProperty);
            ordered = (IOrderedQueryable<TRow>)ordered.Provider.CreateQuery<TRow>(Expression.Call(typeof(Queryable),
                descending ? "ThenByDescending" : "ThenBy", [typeof(TRow), id.Type], ordered.Expression,
                Expression.Quote(Expression.Lambda(id, parameter))));
        }
        return (ordered, $"{column.Key}:{(descending ? "desc" : "asc")}");
    }

    private (Col? Column, bool Descending) ParseSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return (null, false);
        }
        var parts = sort.Split(':');
        var column = Cols.FirstOrDefault(c => c.Sortable &&
                                              string.Equals(c.Key, parts[0], StringComparison.OrdinalIgnoreCase));
        return (column, parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase));
    }

    // ── Async helpers that also work on server-side lists (aggregate reports) ──

    protected static Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) =>
        query.Provider is IAsyncQueryProvider ? query.CountAsync(ct) : Task.FromResult(query.Count());

    protected static Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct) =>
        query.Provider is IAsyncQueryProvider ? query.ToListAsync(ct) : Task.FromResult(query.ToList());

    protected static async Task<decimal> SumAsync<T>(IQueryable<T> query, Expression<Func<T, decimal?>> selector,
        CancellationToken ct) =>
        query.Provider is IAsyncQueryProvider
            ? await query.SumAsync(selector, ct) ?? 0m
            : query.Sum(selector) ?? 0m;

    private static async Task<decimal> SumAsync(IQueryable<TRow> rows, string propertyName, CancellationToken ct)
    {
        var parameter = Expression.Parameter(typeof(TRow), "r");
        var property = Expression.Property(parameter, propertyName);
        var body = Expression.Convert(property, typeof(decimal?));
        return await SumAsync(rows, Expression.Lambda<Func<TRow, decimal?>>(body, parameter), ct);
    }

    /// <summary>Every row of a list-backed report sits in memory on the server; still
    /// never in the browser.</summary>
    protected static IQueryable<TRow> FromList(IEnumerable<TRow> rows) => rows.ToList().AsQueryable();
}

/// <summary>Reads the JSON filter values the browser sends, fills in defaults and
/// validates the dates before any query runs (spec §7.2).</summary>
internal static class ReportFilterParser
{
    public static readonly DateOnly AllTimeStart = new(2020, 4, 1);
    private const int MaxDays = 3700;

    public static Dictionary<string, List<string>> Normalise(Dictionary<string, JsonElement>? raw)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (raw is null)
        {
            return values;
        }
        foreach (var (key, element) in raw)
        {
            values[key] = element.ValueKind switch
            {
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
                    .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList(),
                JsonValueKind.Null or JsonValueKind.Undefined => [],
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? [] : [element.GetString()!],
                JsonValueKind.True => ["true"],
                JsonValueKind.False => [],
                _ => [element.GetRawText()]
            };
        }
        return values;
    }

    public static Dictionary<string, JsonElement> ToJson(Dictionary<string, List<string>> values) =>
        values.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value));

    /// <summary>Defaults for the keys the request left out; the date range is resolved
    /// into "from"/"to".</summary>
    public static (DateOnly From, DateOnly To, Dictionary<string, List<string>> Values) Resolve(
        ReportHandler handler, ReportLookups lookups, Dictionary<string, List<string>> values, DateOnly today)
    {
        var resolved = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in handler.Filters(lookups))
        {
            if (filter.Kind == "dateRange")
            {
                continue;
            }
            resolved[filter.Key] = values.TryGetValue(filter.Key, out var given)
                ? given
                : [.. filter.Default];
        }

        var errors = new List<ApiError>();
        DateOnly? from = ParseDate(values, "from", errors);
        DateOnly? to = ParseDate(values, "to", errors);
        to ??= today;
        from ??= handler.DefaultRange == "all" ? AllTimeStart : to.Value.AddDays(-29);
        if (from > to)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "The start date is after the end date.", "from"));
        }
        else if (ReportMath.DaysIn(from.Value, to.Value) > MaxDays)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Choose a range of ten years or less.", "from"));
        }
        if (errors.Count > 0)
        {
            throw new AppValidationException(errors);
        }
        resolved["from"] = [from!.Value.ToString("yyyy-MM-dd")];
        resolved["to"] = [to.Value.ToString("yyyy-MM-dd")];
        return (from.Value, to.Value, resolved);
    }

    private static DateOnly? ParseDate(Dictionary<string, List<string>> values, string key, List<ApiError> errors)
    {
        var text = values.GetValueOrDefault(key)?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        errors.Add(new ApiError(ErrorCodes.ValidationError, "Enter a valid date.", key));
        return null;
    }
}
