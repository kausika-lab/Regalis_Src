using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Gap-free numbers from MST_NumberSequence via a single atomic
/// `UPDATE … SET CurrentValue += 1 OUTPUT` — the row lock serializes concurrent
/// callers, and running inside the caller's transaction means the number commits
/// or rolls back with the business record (DATABASE_DESIGN §10).
/// </summary>
public class NumberSequenceService(RegalisDbContext db) : INumberSequenceService
{
    public async Task<string> NextAsync(string sequenceKey, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        // Latest financial-year row wins for year-scoped sequences (Invoice).
        command.CommandText = """
            UPDATE dbo.MST_NumberSequence
               SET CurrentValue += 1
            OUTPUT inserted.CurrentValue, inserted.Prefix, inserted.FinancialYear
             WHERE NumberSequenceId = (
                 SELECT TOP 1 NumberSequenceId FROM dbo.MST_NumberSequence
                  WHERE SequenceKey = @key
                  ORDER BY NumberSequenceId DESC);
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@key";
        parameter.Value = sequenceKey;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new BusinessRuleException($"Number sequence '{sequenceKey}' is not seeded.");
        }

        var value = reader.GetInt64(0);
        var prefix = reader.GetString(1);
        var financialYear = reader.IsDBNull(2) ? null : reader.GetString(2);

        // REG-000123 · REG/INV/2026-27/00042 (A09)
        // Year-scoped sequences are the ones seeded with a FinancialYear, so the shape
        // follows the data rather than a hard-coded key list. Behaviour for the rows
        // that exist today (Invoice alone carries a year) is unchanged.
        return financialYear is null
            ? $"{prefix}{value:D6}"
            : $"{prefix}/{financialYear}/{value:D5}";
    }

    public async Task<string> PeekAsync(string sequenceKey, CancellationToken ct = default)
    {
        // Read-only twin of NextAsync: same row, same shape, but CurrentValue is left
        // alone and the "+ 1" happens here instead of in the UPDATE.
        var row = await db.NumberSequences.AsNoTracking()
            .Where(x => x.SequenceKey == sequenceKey)
            .OrderByDescending(x => x.NumberSequenceId)
            .Select(x => new { x.CurrentValue, x.Prefix, x.FinancialYear })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException($"Number sequence '{sequenceKey}' is not seeded.");

        var value = row.CurrentValue + 1;
        return string.IsNullOrWhiteSpace(row.FinancialYear)
            ? $"{row.Prefix}{value:D6}"
            : $"{row.Prefix}/{row.FinancialYear}/{value:D5}";
    }
}
