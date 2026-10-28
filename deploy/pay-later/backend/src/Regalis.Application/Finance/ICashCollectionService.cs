using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

/// <summary>Pay later at the front desk: what is owed, and the receipts that settle it.</summary>
public interface ICashCollectionService
{
    /// <summary>Confirmed pay-later bookings with a balance, oldest due first.</summary>
    Task<PagedResult<OutstandingPayLaterDto>> GetOutstandingAsync(OutstandingListRequest request, CancellationToken ct = default);

    /// <summary>Records one receipt in a serializable transaction: receipt number, the PAY-
    /// payment, the invoice and booking totals, the collector's open register session.</summary>
    Task<CashCollectionDto> RecordAsync(RecordCollectionRequest request, CancellationToken ct = default);

    Task<PagedResult<CashCollectionDto>> GetCollectionsAsync(CashCollectionListRequest request, CancellationToken ct = default);

    Task<CashCollectionDto> GetCollectionAsync(long cashCollectionId, CancellationToken ct = default);

    /// <summary>Voids a receipt (the row stays) and reverses the totals it moved.</summary>
    Task<CashCollectionDto> VoidAsync(long cashCollectionId, VoidCollectionRequest request, CancellationToken ct = default);

    Task<(byte[] Content, string FileName)> GetReceiptPdfAsync(long cashCollectionId, CancellationToken ct = default);
}

/// <summary>The collector's drawer for the day: open with a float, close with a count,
/// an Admin approves and takes the cash.</summary>
public interface ICashRegisterService
{
    /// <summary>The caller's open session at a location today, or null.</summary>
    Task<CashRegisterSessionDto?> GetCurrentAsync(int locationId, CancellationToken ct = default);

    Task<CashRegisterSessionDto> OpenAsync(OpenRegisterRequest request, CancellationToken ct = default);

    Task<CashRegisterSessionDto> CloseAsync(long sessionId, CloseRegisterRequest request, CancellationToken ct = default);

    Task<CashRegisterSessionDto> ApproveAsync(long sessionId, CancellationToken ct = default);

    Task<CashRegisterSessionDto> GetAsync(long sessionId, CancellationToken ct = default);

    Task<PagedResult<CashRegisterSessionDto>> GetSessionsAsync(CashRegisterListRequest request, CancellationToken ct = default);
}
