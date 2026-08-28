namespace Regalis.Shared.Api;

/// <summary>
/// The common query contract for every list endpoint:
/// <c>?page=1&amp;pageSize=20&amp;sortBy=createdDate&amp;sortDir=desc&amp;search=…</c>.
/// <see cref="PageSize"/> is capped at <see cref="MaxPageSize"/>.
/// </summary>
public class PagedRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public string? SortBy { get; set; }
    public string SortDir { get; set; } = "desc";
    public string? Search { get; set; }

    public bool IsDescending => !string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase);
}
