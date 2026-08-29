using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>The one locking conflict check (DATABASE_DESIGN §5), shared by every
/// code path that writes occupancy: holds, confirmations, reschedules, extensions.</summary>
internal static class BookingEngineLocks
{
    /// <summary>Runs the documented availability predicate under UPDLOCK/HOLDLOCK
    /// inside the caller's serializable transaction; the key-range locks block a
    /// concurrent insert of a conflicting row until the transaction ends.</summary>
    public static async Task<int> CountConflictsAsync(RegalisDbContext db, int workspaceId,
        DateTime startUtc, DateTime endUtc, long? excludeBookingId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM dbo.TRN_Booking WITH (UPDLOCK, HOLDLOCK)
                WHERE WorkspaceId = @workspaceId AND IsDeleted = 0
                  AND Status IN (1,2,3)
                  AND BookingId <> @excludeBookingId
                  AND StartUtc < @endUtc AND @startUtc < EndUtc)
            + (SELECT COUNT(*) FROM dbo.TRN_WorkspaceBlock WITH (UPDLOCK, HOLDLOCK)
                WHERE WorkspaceId = @workspaceId AND IsDeleted = 0 AND IsActive = 1
                  AND StartUtc < @endUtc AND @startUtc < EndUtc);
            """;
        AddParameter(command, "@workspaceId", workspaceId);
        AddParameter(command, "@startUtc", startUtc);
        AddParameter(command, "@endUtc", endUtc);
        AddParameter(command, "@excludeBookingId", excludeBookingId ?? -1L);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
