using System.Data;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Purchase orders (document §4). Two rules carry the weight here. Totals are always
/// recomputed from quantity, rate, discount and the supplier's tax percent, so a
/// client-sent total is never trusted. And receiving goods writes the incoming
/// TRN_InventoryTransaction rows, the line receipts and the header status inside one
/// serializable transaction — stock and the order can never disagree, and there is no
/// second "incoming goods" table to fall out of step with the ledger (Invariant 11).
/// </summary>
public class PurchaseOrderService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IPurchaseOrderService
{
    public async Task<PagedResult<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        PurchaseOrderListRequest request, CancellationToken ct = default)
    {
        var query = db.PurchaseOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.PoNumber.Contains(term));
        }
        if (request.Status is not null)
        {
            query = query.Where(p => (byte)p.Status == request.Status);
        }
        if (request.SupplierId is not null)
        {
            query = query.Where(p => p.SupplierId == request.SupplierId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(p => p.LocationId == request.LocationId);
        }
        if (request.From is not null)
        {
            query = query.Where(p => p.PoDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(p => p.PoDate <= request.To);
        }

        var totalCount = await query.CountAsync(ct);
        var ids = await query
            .OrderByDescending(p => p.PoDate).ThenByDescending(p => p.PurchaseOrderId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => p.PurchaseOrderId)
            .ToListAsync(ct);

        var rows = await LoadAsync(ids, ct);
        return PagedResult<PurchaseOrderDto>.Create(rows, request.Page, request.PageSize, totalCount);
    }

    public async Task<PurchaseOrderDto> GetPurchaseOrderAsync(long purchaseOrderId,
        CancellationToken ct = default) =>
        (await LoadAsync([purchaseOrderId], ct)).FirstOrDefault()
        ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request,
        CancellationToken ct = default)
    {
        await GuardHeaderAsync(request, ct);
        await GuardLineItemsAsync(request, ct);

        var today = IstClock.IstDateOf(clock.UtcNow);
        var order = new PurchaseOrder
        {
            SupplierId = request.SupplierId,
            LocationId = request.LocationId,
            PoDate = request.PoDate ?? today,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Remarks = request.Remarks?.Trim(),
            Status = PurchaseOrderStatus.Draft
        };
        ReplaceLines(order, request);

        // The number is allocated inside the transaction that saves the order, so a
        // rollback gives the number back instead of leaving a gap.
        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            order.PoNumber = await numberSequence.NextAsync(SequenceKeys.PurchaseOrder, ct);
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return order.PurchaseOrderId;
        });

        return await GetPurchaseOrderAsync(newId, ct);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(long purchaseOrderId,
        SavePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleException(
                "Only a draft purchase order can be edited. Cancel this one and raise a new PO.");
        }
        await GuardHeaderAsync(request, ct);
        await GuardLineItemsAsync(request, ct);

        order.SupplierId = request.SupplierId;
        order.LocationId = request.LocationId;
        order.PoDate = request.PoDate ?? order.PoDate;
        order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        order.Remarks = request.Remarks?.Trim();

        // Nothing is received while Draft, so replacing the lines loses no receipts.
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        ReplaceLines(order, request);

        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> SendAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleException("Only a draft purchase order can be sent.");
        }
        order.Status = PurchaseOrderStatus.Sent;
        order.SentUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(long purchaseOrderId,
        ReceiveGoodsRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Serializable: two storekeepers receiving the same PO at once must not
            // both pass the "not more than ordered" check.
            await using var dbTransaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var order = await db.PurchaseOrders
                .Include(p => p.Lines)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
                ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

            if (order.Status is PurchaseOrderStatus.Draft)
            {
                throw new BusinessRuleException(
                    "Send the purchase order to the supplier before receiving against it.");
            }
            if (order.Status is PurchaseOrderStatus.Cancelled)
            {
                throw new BusinessRuleException("This purchase order was cancelled.");
            }
            if (order.Status is PurchaseOrderStatus.Received)
            {
                throw new BusinessRuleException("This purchase order is already fully received.");
            }

            var receivedDate = request.ReceivedDate ?? IstClock.IstDateOf(clock.UtcNow);
            if (receivedDate < order.PoDate)
            {
                throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                    "Goods cannot be received before the PO date.",
                    nameof(request.ReceivedDate))]);
            }

            foreach (var lineRequest in request.Lines)
            {
                var line = order.Lines
                    .FirstOrDefault(l => l.PurchaseOrderLineId == lineRequest.PurchaseOrderLineId)
                    ?? throw new NotFoundException("PurchaseOrderLine", lineRequest.PurchaseOrderLineId);

                var pending = line.Quantity - line.ReceivedQuantity;
                if (lineRequest.ReceivedQuantity > pending)
                {
                    throw new BusinessRuleException(
                        $"{line.ItemName}: only {pending:0.###} pending against this order.");
                }

                line.ReceivedQuantity += lineRequest.ReceivedQuantity;

                // Only a line tied to the stock master moves inventory; a PO for a
                // service or a one-off purchase still records its receipt on the line.
                if (line.InventoryItemId is null)
                {
                    continue;
                }

                var item = await db.InventoryItems
                    .FirstOrDefaultAsync(i => i.InventoryItemId == line.InventoryItemId, ct)
                    ?? throw new NotFoundException("InventoryItem", line.InventoryItemId);

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryItemId = item.InventoryItemId,
                    TransactionNumber = await numberSequence
                        .NextAsync(SequenceKeys.InventoryTransaction, ct),
                    TransactionType = InventoryTransactionType.In,
                    Quantity = lineRequest.ReceivedQuantity,
                    TransactionDate = receivedDate,
                    SourceOrDestination = order.Supplier?.SupplierName,
                    ReferenceNumber = order.PoNumber,
                    SupplierId = order.SupplierId,
                    PurchaseOrderId = order.PurchaseOrderId,
                    PurchaseInvoiceNumber = request.PurchaseInvoiceNumber?.Trim(),
                    AttachmentPath = request.AttachmentPath?.Trim(),
                    UnitCost = line.Rate,
                    LocationId = item.LocationId,
                    EnteredByUserId = currentUser.UserId,
                    Notes = request.Notes?.Trim()
                });
            }

            order.Status = order.Lines.All(l => l.ReceivedQuantity >= l.Quantity)
                ? PurchaseOrderStatus.Received
                : PurchaseOrderStatus.PartiallyReceived;

            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return true;
        });

        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> CancelAsync(long purchaseOrderId, string reason,
        CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

        if (order.Status == PurchaseOrderStatus.Cancelled)
        {
            throw new BusinessRuleException("This purchase order is already cancelled.");
        }
        // Goods already in the building are in the ledger; cancelling the paperwork
        // would leave stock with no explanation.
        if (order.Lines.Any(l => l.ReceivedQuantity > 0))
        {
            throw new BusinessRuleException(
                "Goods have already been received against this purchase order. " +
                "Correct the stock with an Adjustment transaction instead of cancelling.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Give a reason for the cancellation.", nameof(reason))]);
        }

        order.Status = PurchaseOrderStatus.Cancelled;
        order.CancelledUtc = clock.UtcNow;
        order.CancellationReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task GuardHeaderAsync(SavePurchaseOrderRequest request, CancellationToken ct)
    {
        if (!await db.Suppliers.AnyAsync(s => s.SupplierId == request.SupplierId, ct))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
    }

    private async Task GuardLineItemsAsync(SavePurchaseOrderRequest request, CancellationToken ct)
    {
        var itemIds = request.Lines
            .Where(l => l.InventoryItemId is not null)
            .Select(l => l.InventoryItemId!.Value)
            .Distinct()
            .ToList();
        if (itemIds.Count == 0)
        {
            return;
        }
        var found = await db.InventoryItems
            .Where(i => itemIds.Contains(i.InventoryItemId))
            .Select(i => i.InventoryItemId)
            .ToListAsync(ct);
        var missing = itemIds.Except(found).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("InventoryItem", missing[0]);
        }
    }

    /// <summary>Server-side money. Line value is quantity × rate − discount; tax is the
    /// supplier's percent on that; the header is the sum of its lines.</summary>
    private static void ReplaceLines(PurchaseOrder order, SavePurchaseOrderRequest request)
    {
        var lineNumber = 0;
        foreach (var source in request.Lines)
        {
            lineNumber++;
            var gross = Math.Round(source.Quantity * source.Rate, 2, MidpointRounding.AwayFromZero);
            var lineAmount = Math.Round(gross - source.DiscountAmount, 2, MidpointRounding.AwayFromZero);
            var taxAmount = Math.Round(lineAmount * source.TaxPercent / 100m, 2, MidpointRounding.AwayFromZero);

            order.Lines.Add(new PurchaseOrderLine
            {
                LineNumber = lineNumber,
                InventoryItemId = source.InventoryItemId,
                ItemName = source.ItemName.Trim(),
                HsnSacCode = source.HsnSacCode?.Trim(),
                Unit = source.Unit?.Trim(),
                Quantity = source.Quantity,
                ReceivedQuantity = 0,
                Rate = source.Rate,
                DiscountAmount = source.DiscountAmount,
                LineAmount = lineAmount,
                TaxPercent = source.TaxPercent,
                TaxAmount = taxAmount,
                TotalAmount = Math.Round(lineAmount + taxAmount, 2, MidpointRounding.AwayFromZero),
                Remarks = source.Remarks?.Trim()
            });
        }

        order.SubTotal = Math.Round(order.Lines.Sum(l => l.Quantity * l.Rate), 2, MidpointRounding.AwayFromZero);
        order.DiscountAmount = Math.Round(order.Lines.Sum(l => l.DiscountAmount), 2, MidpointRounding.AwayFromZero);
        order.TaxableAmount = Math.Round(order.Lines.Sum(l => l.LineAmount), 2, MidpointRounding.AwayFromZero);
        order.TaxAmount = Math.Round(order.Lines.Sum(l => l.TaxAmount), 2, MidpointRounding.AwayFromZero);
        order.TotalAmount = Math.Round(order.Lines.Sum(l => l.TotalAmount), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<List<PurchaseOrderDto>> LoadAsync(List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }
        var orders = await db.PurchaseOrders.AsNoTracking()
            .Where(p => ids.Contains(p.PurchaseOrderId))
            .Select(p => new
            {
                Order = p,
                SupplierName = db.Suppliers.Where(s => s.SupplierId == p.SupplierId)
                    .Select(s => s.SupplierName).FirstOrDefault(),
                SupplierGstin = db.Suppliers.Where(s => s.SupplierId == p.SupplierId)
                    .Select(s => s.Gstin).FirstOrDefault(),
                LocationName = db.Locations.Where(l => l.LocationId == p.LocationId)
                    .Select(l => l.LocationName).FirstOrDefault(),
                Lines = p.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseOrderLineDto(
                    l.PurchaseOrderLineId, l.LineNumber, l.InventoryItemId,
                    db.InventoryItems.Where(i => i.InventoryItemId == l.InventoryItemId)
                        .Select(i => i.ItemCode).FirstOrDefault(),
                    l.ItemName, l.HsnSacCode, l.Unit, l.Quantity, l.ReceivedQuantity,
                    l.Quantity - l.ReceivedQuantity, l.Rate, l.DiscountAmount, l.LineAmount,
                    l.TaxPercent, l.TaxAmount, l.TotalAmount, l.Remarks)).ToList()
            })
            .ToListAsync(ct);

        // Preserve the page's ordering, which the id query already decided.
        return ids
            .Select(id => orders.FirstOrDefault(o => o.Order.PurchaseOrderId == id))
            .Where(o => o is not null)
            .Select(o => new PurchaseOrderDto(
                o!.Order.PurchaseOrderId, o.Order.PoNumber, o.Order.PoDate, o.Order.SupplierId,
                o.SupplierName ?? string.Empty, o.SupplierGstin, o.Order.LocationId, o.LocationName,
                o.Order.ExpectedDeliveryDate, o.Order.SubTotal, o.Order.DiscountAmount,
                o.Order.TaxableAmount, o.Order.TaxAmount, o.Order.TotalAmount, (byte)o.Order.Status,
                o.Order.SentUtc, o.Order.CancelledUtc, o.Order.CancellationReason, o.Order.Remarks,
                o.Order.AttachmentPath, o.Order.CreatedDate, o.Lines))
            .ToList();
    }
}
