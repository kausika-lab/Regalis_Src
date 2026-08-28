using Microsoft.EntityFrameworkCore;

namespace Regalis.Infrastructure;

/// <summary>EF Core 8 has no ToHashSetAsync (it arrives in EF 9); this fills the gap
/// for the seeders and admin services. In scope everywhere under Regalis.Infrastructure.</summary>
internal static class QueryableExtensions
{
    public static async Task<HashSet<T>> ToHashSetAsync<T>(
        this IQueryable<T> source, CancellationToken ct = default) =>
        (await source.ToListAsync(ct)).ToHashSet();
}
