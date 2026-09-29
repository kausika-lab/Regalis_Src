using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Purchase orders (document §4). Three rules carry the weight here.
///
/// Totals are always recomputed from quantity, rate, discount, the supplier's tax
/// percent, TDS and adjustment, so a client-sent total is never trusted. The pricing
/// mirrors QuotationService: the whole-order discount is spread across lines in
/// proportion to their value, GST is charged per line on what is left and split
/// CGST/SGST or IGST by comparing the supplier's state with the delivery location's.
///
/// The workflow is Draft → PendingApproval → Approved → Sent → (Partially) Received,
/// with Cancelled reachable until goods arrive. Only a Draft can be edited; from Sent
/// the PO is frozen. Each step writes a status-history row and a named audit row.
///
/// Receiving goods writes the incoming TRN_InventoryTransaction rows, the line
/// receipts and the header status inside one serializable transaction — stock and the
/// order can never disagree (Invariant 11).
/// </summary>
public class PurchaseOrderService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ISettingService settings,
    IOptions<StorageOptions> storageOptions) : IPurchaseOrderService
{
    private const int MaxAttachments = 10;
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const string DefaultTds =
        "194C1|194C Contractor – individual/HUF 1%|1;194C2|194C Contractor – others 2%|2;" +
        "194J|194J Professional services 10%|10;194I|194I Rent 10%|10;194Q|194Q Purchase of goods 0.1%|0.1";

    private static readonly Dictionary<string, string> AllowedAttachmentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        PurchaseOrderListRequest request, CancellationToken ct = default)
    {
        var query = db.PurchaseOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.PoNumber.Contains(term)
                                     || (p.ReferenceNumber != null && p.ReferenceNumber.Contains(term))
                                     || p.Supplier.SupplierName.Contains(term));
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

    public async Task<PurchaseOrderFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default)
    {
        var accounts = Csv(await settings.GetStringAsync(AppSettingKeys.Purchase.AccountCategories,
            "Pantry & supplies,Furniture & fixtures,Office supplies,Housekeeping supplies," +
            "Repairs & maintenance,IT & electronics,Professional services,Other", ct));
        // The expense heads already in use are accounting structures too: offer them.
        var expenseHeads = await db.Expenses.AsNoTracking()
            .Select(e => e.Category).Distinct().ToListAsync(ct);
        accounts.AddRange(expenseHeads
            .Where(h => !string.IsNullOrWhiteSpace(h)
                        && !accounts.Contains(h, StringComparer.OrdinalIgnoreCase))
            .OrderBy(h => h));

        return new PurchaseOrderFormOptionsDto(
            await numberSequence.PeekAsync(SequenceKeys.PurchaseOrder, ct),
            await RequireApprovalAsync(ct),
            await settings.GetDecimalAsync(AppSettingKeys.Purchase.DefaultGstPercent, 18m, ct),
            ParseRates(await settings.GetStringAsync(AppSettingKeys.Purchase.GstRateOptions,
                "0,5,12,18,28", ct)),
            Csv(await settings.GetStringAsync(AppSettingKeys.Purchase.ShipmentPreferences,
                "Supplier delivery,Pickup,Courier,Other", ct)),
            accounts,
            await settings.GetStringAsync(AppSettingKeys.Purchase.TermsAndConditions, "", ct),
            await HomeStateCodeAsync(ct),
            await LoadTdsOptionsAsync(ct),
            GstStates.All,
            MaxAttachments,
            MaxAttachmentBytes);
    }

    // ── Create / edit ────────────────────────────────────────────────────────

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request,
        CancellationToken ct = default)
    {
        var (supplier, location) = await GuardHeaderAsync(request, ct);
        await GuardLineItemsAsync(request, ct);

        var today = IstClock.IstDateOf(clock.UtcNow);
        var order = new PurchaseOrder
        {
            PoDate = request.PoDate ?? today,
            Status = PurchaseOrderStatus.Draft,
            TermsAndConditions = request.TermsAndConditions is null
                ? NullIfBlank(await settings.GetStringAsync(AppSettingKeys.Purchase.TermsAndConditions, "", ct))
                : NullIfBlank(request.TermsAndConditions)
        };
        await ApplyAsync(order, request, supplier, location, ct);

        // The number is allocated inside the transaction that saves the order, so a
        // rollback gives the number back instead of leaving a gap, and the unique
        // index on PoNumber is the last line of defence against a duplicate.
        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            order.PoNumber = await numberSequence.NextAsync(SequenceKeys.PurchaseOrder, ct);
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync(ct);
            AddHistory(order, null, PurchaseOrderStatus.Draft, "Created", null);
            AddAudit(order, "PO_CREATED", null, PurchaseOrderStatus.Draft, null);
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
            throw new BusinessRuleException(order.Status == PurchaseOrderStatus.PendingApproval
                ? "This purchase order is waiting for approval. Ask the approver to reject it back to Draft to make changes."
                : "Only a draft purchase order can be edited. Cancel this one and raise a new PO.");
        }
        var (supplier, location) = await GuardHeaderAsync(request, ct);
        await GuardLineItemsAsync(request, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            order.PoDate = request.PoDate ?? order.PoDate;
            if (request.TermsAndConditions is not null)
            {
                order.TermsAndConditions = NullIfBlank(request.TermsAndConditions);
            }
            // Nothing is received while Draft, so replacing the lines loses no receipts.
            db.PurchaseOrderLines.RemoveRange(order.Lines);
            order.Lines.Clear();
            await ApplyAsync(order, request, supplier, location, ct);
            AddAudit(order, "PO_EDITED", order.Status, order.Status, null);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return true;
        });
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    // ── Workflow ─────────────────────────────────────────────────────────────

    public async Task<PurchaseOrderDto> SubmitAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        var order = await LoadForWorkflowAsync(purchaseOrderId, ct);
        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleException("Only a draft purchase order can be submitted for approval.");
        }
        EnsureReadyToIssue(order);

        order.Status = PurchaseOrderStatus.PendingApproval;
        order.SubmittedUtc = clock.UtcNow;
        AddHistory(order, PurchaseOrderStatus.Draft, order.Status, "Submitted", null);
        AddAudit(order, "PO_SUBMITTED", PurchaseOrderStatus.Draft, order.Status, null);
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(long purchaseOrderId, string? comment,
        CancellationToken ct = default)
    {
        var order = await LoadForWorkflowAsync(purchaseOrderId, ct);
        var from = order.Status;
        if (from == PurchaseOrderStatus.Draft)
        {
            if (await RequireApprovalAsync(ct))
            {
                throw new BusinessRuleException("Submit the purchase order for approval first.");
            }
            EnsureReadyToIssue(order);
        }
        else if (from != PurchaseOrderStatus.PendingApproval)
        {
            throw new BusinessRuleException("Only a purchase order awaiting approval can be approved.");
        }

        var nowUtc = clock.UtcNow;
        order.Status = PurchaseOrderStatus.Approved;
        order.SubmittedUtc ??= nowUtc;
        order.ApprovedUtc = nowUtc;
        order.ApprovedByUserId = currentUser.UserId;
        AddHistory(order, from, order.Status, "Approved", NullIfBlank(comment));
        AddAudit(order, "PO_APPROVED", from, order.Status, NullIfBlank(comment));
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> RejectAsync(long purchaseOrderId, string reason,
        CancellationToken ct = default)
    {
        var order = await LoadForWorkflowAsync(purchaseOrderId, ct);
        if (order.Status != PurchaseOrderStatus.PendingApproval)
        {
            throw new BusinessRuleException("Only a purchase order awaiting approval can be rejected.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Give a reason, so the requester knows what to change.", "comment")]);
        }

        order.Status = PurchaseOrderStatus.Draft;
        order.SubmittedUtc = null;
        AddHistory(order, PurchaseOrderStatus.PendingApproval, order.Status, "Rejected", reason.Trim());
        AddAudit(order, "PO_REJECTED", PurchaseOrderStatus.PendingApproval, order.Status, reason.Trim());
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> SendAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        var order = await LoadForWorkflowAsync(purchaseOrderId, ct);
        if (order.Status != PurchaseOrderStatus.Approved)
        {
            throw new BusinessRuleException(order.Status == PurchaseOrderStatus.Draft
                || order.Status == PurchaseOrderStatus.PendingApproval
                ? "Approve the purchase order before sending it to the supplier."
                : "Only an approved purchase order can be sent.");
        }
        order.Status = PurchaseOrderStatus.Sent;
        order.SentUtc = clock.UtcNow;
        AddHistory(order, PurchaseOrderStatus.Approved, order.Status, "Sent", null);
        AddAudit(order, "PO_SENT", PurchaseOrderStatus.Approved, order.Status, null);
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(long purchaseOrderId,
        ReceiveGoodsRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            // Serializable: two storekeepers receiving the same PO at once must not
            // both pass the "not more than ordered" check.
            await using var dbTransaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var order = await db.PurchaseOrders
                .Include(p => p.Lines)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
                ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

            switch (order.Status)
            {
                case PurchaseOrderStatus.Draft:
                case PurchaseOrderStatus.PendingApproval:
                case PurchaseOrderStatus.Approved:
                    throw new BusinessRuleException(
                        "Send the purchase order to the supplier before receiving against it.");
                case PurchaseOrderStatus.Cancelled:
                    throw new BusinessRuleException("This purchase order was cancelled.");
                case PurchaseOrderStatus.Received:
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

            var from = order.Status;
            order.Status = order.Lines.All(l => l.ReceivedQuantity >= l.Quantity)
                ? PurchaseOrderStatus.Received
                : PurchaseOrderStatus.PartiallyReceived;
            var fully = order.Status == PurchaseOrderStatus.Received;
            var note = string.IsNullOrWhiteSpace(request.PurchaseInvoiceNumber)
                ? null
                : $"Supplier invoice {request.PurchaseInvoiceNumber.Trim()}";
            AddHistory(order, from, order.Status, fully ? "Fully received" : "Partially received", note);
            AddAudit(order, fully ? "PO_RECEIVED" : "PO_PARTIALLY_RECEIVED", from, order.Status, note);

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

        var from = order.Status;
        order.Status = PurchaseOrderStatus.Cancelled;
        order.CancelledUtc = clock.UtcNow;
        order.CancellationReason = reason.Trim();
        AddHistory(order, from, order.Status, "Cancelled", order.CancellationReason);
        AddAudit(order, "PO_CANCELLED", from, order.Status, order.CancellationReason);
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    // ── Attachments ──────────────────────────────────────────────────────────

    public async Task<PurchaseOrderDto> AddAttachmentAsync(long purchaseOrderId,
        AttachmentUpload upload, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);
        EnsureDraft(order, "Attachments can only be changed while the purchase order is a draft.");

        if (await db.PurchaseOrderAttachments.CountAsync(a => a.PurchaseOrderId == purchaseOrderId, ct)
            >= MaxAttachments)
        {
            throw new BusinessRuleException($"A purchase order can carry at most {MaxAttachments} files.");
        }
        if (upload.Length is <= 0 or > MaxAttachmentBytes)
        {
            throw new BusinessRuleException("Each file must be between 1 byte and 10 MB.");
        }
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) ||
            !AllowedAttachmentTypes.TryGetValue(extension, out var contentType))
        {
            throw new BusinessRuleException("Allowed file types: .pdf .jpg .jpeg .png .docx .xlsx");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > MaxAttachmentBytes)
        {
            throw new BusinessRuleException("Each file must be between 1 byte and 10 MB.");
        }
        // Office files are ZIP containers, which the sniffer does not cover; everything
        // it does know about is checked, so a renamed executable never gets stored.
        var contentMatches = extension.ToLowerInvariant() is ".docx" or ".xlsx"
            ? bytes is [0x50, 0x4B, ..]
            : FileTypeSniffer.Matches(extension, bytes);
        if (!contentMatches)
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "purchase-orders",
            order.PurchaseOrderId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, storedFileName);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        db.PurchaseOrderAttachments.Add(new PurchaseOrderAttachment
        {
            PurchaseOrderId = order.PurchaseOrderId,
            FileName = Path.GetFileName(upload.FileName),
            StoredFileName = storedFileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderDto> RemoveAttachmentAsync(long purchaseOrderId, long attachmentId,
        CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);
        EnsureDraft(order, "Attachments can only be changed while the purchase order is a draft.");

        var attachment = await db.PurchaseOrderAttachments.FirstOrDefaultAsync(
            a => a.PurchaseOrderAttachmentId == attachmentId && a.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrderAttachment", attachmentId);
        db.PurchaseOrderAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        if (File.Exists(attachment.FilePath))
        {
            File.Delete(attachment.FilePath);
        }
        return await GetPurchaseOrderAsync(purchaseOrderId, ct);
    }

    public async Task<PurchaseOrderAttachmentContent> GetAttachmentAsync(long purchaseOrderId,
        long attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.PurchaseOrderAttachments.AsNoTracking().FirstOrDefaultAsync(
            a => a.PurchaseOrderAttachmentId == attachmentId && a.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrderAttachment", attachmentId);
        if (!File.Exists(attachment.FilePath))
        {
            throw new NotFoundException("PurchaseOrderAttachment", attachmentId);
        }
        return new PurchaseOrderAttachmentContent(await File.ReadAllBytesAsync(attachment.FilePath, ct),
            attachment.ContentType, attachment.FileName);
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    public async Task<byte[]> GetPdfAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Lines)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);
        var location = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LocationId == order.LocationId, ct);
        var approvedBy = order.ApprovedByUserId is null
            ? null
            : await db.Users.AsNoTracking().Where(u => u.UserId == order.ApprovedByUserId)
                .Select(u => u.FullName).FirstOrDefaultAsync(ct);

        var logoPath = await settings.GetStringAsync("Company.LogoPath", "", ct);
        byte[]? logo = null;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath)
            && Path.GetExtension(logoPath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
        {
            logo = await File.ReadAllBytesAsync(logoPath, ct);
        }

        var context = new PurchaseOrderPdfContext(
            CompanyName: await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct),
            CompanyAddress: NullIfBlank(await settings.GetStringAsync("Company.Address", "", ct))
                            ?? AddressOf(location),
            CompanyGstin: NullIfBlank(await settings.GetStringAsync("Company.Gstin", "", ct)),
            CompanyPhone: NullIfBlank(await settings.GetStringAsync("Company.Phone", "", ct))
                          ?? location?.ContactNumber,
            CompanyEmail: NullIfBlank(await settings.GetStringAsync("Company.Email", "", ct))
                          ?? location?.Email,
            LocationName: location?.LocationName,
            ApprovedByName: approvedBy,
            Logo: logo);
        return PurchaseOrderPdfGenerator.Generate(order, context);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Supplier Supplier, Domain.Entities.Masters.Location Location)> GuardHeaderAsync(
        SavePurchaseOrderRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId, ct)
            ?? throw new NotFoundException("Supplier", request.SupplierId);
        var location = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LocationId == request.LocationId, ct)
            ?? throw new NotFoundException("Location", request.LocationId);
        if (!string.IsNullOrWhiteSpace(request.TdsCode)
            && (await LoadTdsOptionsAsync(ct)).All(o => !string.Equals(o.Code, request.TdsCode.Trim(),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "That TDS section is not configured.", nameof(SavePurchaseOrderRequest.TdsCode))]);
        }
        return (supplier, location);
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

    /// <summary>Copies the header fields and recomputes every line and total.</summary>
    private async Task ApplyAsync(PurchaseOrder order, SavePurchaseOrderRequest request,
        Supplier supplier, Domain.Entities.Masters.Location location, CancellationToken ct)
    {
        order.SupplierId = request.SupplierId;
        order.LocationId = request.LocationId;
        order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        order.Remarks = NullIfBlank(request.Remarks);
        order.ReferenceNumber = NullIfBlank(request.ReferenceNumber);
        order.ShipmentPreference = NullIfBlank(request.ShipmentPreference);
        order.IsReverseCharge = request.IsReverseCharge;
        order.DeliveryAddress = NullIfBlank(request.DeliveryAddress) ?? AddressOf(location);

        var supplierState = SupplierStateCode(supplier);
        var locationState = StateCodeOf(location.State) ?? await HomeStateCodeAsync(ct);
        order.IsInterState = supplierState is not null && supplierState != locationState;

        var tds = string.IsNullOrWhiteSpace(request.TdsCode)
            ? null
            : (await LoadTdsOptionsAsync(ct)).First(o => string.Equals(o.Code, request.TdsCode.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var totals = PurchaseOrderPricing.Compute(
            request.Lines.Select(l => new PurchaseOrderPricing.LineInput(
                l.Quantity, l.Rate, l.DiscountAmount, l.TaxPercent)).ToList(),
            (PurchaseOrderDiscountType?)request.DiscountType,
            request.DiscountValue ?? 0m,
            order.IsInterState,
            order.IsReverseCharge,
            tds?.Percent ?? 0m,
            request.AdjustmentAmount ?? 0m);

        var lineNumber = 0;
        foreach (var (source, math) in request.Lines.Zip(totals.Lines))
        {
            lineNumber++;
            order.Lines.Add(new PurchaseOrderLine
            {
                LineNumber = lineNumber,
                InventoryItemId = source.InventoryItemId,
                ItemName = source.ItemName.Trim(),
                HsnSacCode = NullIfBlank(source.HsnSacCode),
                Unit = NullIfBlank(source.Unit),
                AccountCategory = NullIfBlank(source.AccountCategory),
                Quantity = source.Quantity,
                ReceivedQuantity = 0,
                Rate = source.Rate,
                DiscountAmount = math.Discount,
                LineAmount = math.Taxable,
                TaxPercent = source.TaxPercent,
                TaxAmount = math.Tax,
                CgstAmount = math.Cgst,
                SgstAmount = math.Sgst,
                IgstAmount = math.Igst,
                TotalAmount = math.Total,
                Remarks = NullIfBlank(source.Remarks)
            });
        }

        order.SubTotal = totals.SubTotal;
        order.DiscountType = request.DiscountType is null || (request.DiscountValue ?? 0m) == 0m
            ? null
            : (PurchaseOrderDiscountType)request.DiscountType;
        order.DiscountValue = order.DiscountType is null ? 0m : request.DiscountValue ?? 0m;
        order.DiscountAmount = totals.Discount;
        order.TaxableAmount = totals.Taxable;
        order.CgstAmount = totals.Cgst;
        order.SgstAmount = totals.Sgst;
        order.IgstAmount = totals.Igst;
        order.TaxAmount = totals.Tax;
        order.TdsCode = tds?.Code;
        order.TdsLabel = tds?.Label;
        order.TdsPercent = tds?.Percent ?? 0m;
        order.TdsAmount = totals.Tds;
        order.AdjustmentLabel = request.AdjustmentAmount is null or 0m ? null : NullIfBlank(request.AdjustmentLabel);
        order.AdjustmentAmount = request.AdjustmentAmount ?? 0m;
        order.TotalAmount = totals.Total;

        if (totals.ExcessDiscount)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Discount cannot be more than the sub total.", nameof(SavePurchaseOrderRequest.DiscountValue))]);
        }
        if (order.TotalAmount < 0)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Total cannot be negative. Check the discount, TDS and adjustment.",
                nameof(SavePurchaseOrderRequest.AdjustmentAmount))]);
        }
    }

    /// <summary>The checks that apply before a PO can go to an approver or a supplier.</summary>
    private static void EnsureReadyToIssue(PurchaseOrder order)
    {
        var errors = new List<ApiError>();
        if (order.ExpectedDeliveryDate is null)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError,
                "Set the expected delivery date.", nameof(PurchaseOrder.ExpectedDeliveryDate)));
        }
        if (string.IsNullOrWhiteSpace(order.DeliveryAddress))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError,
                "Choose a delivery address.", nameof(PurchaseOrder.DeliveryAddress)));
        }
        if (string.IsNullOrWhiteSpace(order.TermsAndConditions))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError,
                "Terms & conditions are required before the PO is issued.",
                nameof(PurchaseOrder.TermsAndConditions)));
        }
        if (order.Lines.Count == 0)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError,
                "A purchase order needs at least one line.", "lines"));
        }
        if (errors.Count > 0)
        {
            throw new AppValidationException(errors);
        }
    }

    private static void EnsureDraft(PurchaseOrder order, string message)
    {
        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleException(message);
        }
    }

    private async Task<PurchaseOrder> LoadForWorkflowAsync(long purchaseOrderId, CancellationToken ct) =>
        await db.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
        ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);

    private void AddHistory(PurchaseOrder order, PurchaseOrderStatus? from, PurchaseOrderStatus to,
        string action, string? reason) =>
        db.PurchaseOrderStatusHistory.Add(new PurchaseOrderStatusHistory
        {
            PurchaseOrderId = order.PurchaseOrderId,
            PurchaseOrder = order,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = clock.UtcNow,
            Reason = reason
        });

    /// <summary>A named business event in TRN_AuditLog, beside the generic row-change
    /// entries the save interceptor already writes.</summary>
    private void AddAudit(PurchaseOrder order, string action, PurchaseOrderStatus? from,
        PurchaseOrderStatus to, string? reason) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            UserName = currentUser.FullName ?? currentUser.Email,
            Action = action,
            ModuleName = "Finance",
            EntityName = nameof(PurchaseOrder),
            EntityId = order.PurchaseOrderId.ToString(CultureInfo.InvariantCulture),
            OldValue = from is null ? null : JsonSerializer.Serialize(new { Status = from.ToString() }),
            NewValue = JsonSerializer.Serialize(new
            {
                order.PoNumber,
                Status = to.ToString(),
                order.TotalAmount,
                Reason = reason
            }),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow
        });

    private async Task<bool> RequireApprovalAsync(CancellationToken ct) =>
        await settings.GetBoolAsync(AppSettingKeys.Purchase.RequireApproval, false, ct);

    private async Task<string> HomeStateCodeAsync(CancellationToken ct) =>
        await settings.GetStringAsync(AppSettingKeys.Quotation.HomeStateCode, "29", ct);

    /// <summary>The GSTIN's first two digits are the state code; failing that, the state name.</summary>
    private static string? SupplierStateCode(Supplier supplier)
    {
        var gstin = supplier.Gstin?.Trim();
        if (gstin is { Length: >= 2 } && char.IsDigit(gstin[0]) && char.IsDigit(gstin[1]))
        {
            return gstin[..2];
        }
        return StateCodeOf(supplier.State);
    }

    private static string? StateCodeOf(string? stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return null;
        }
        var name = stateName.Trim();
        return GstStates.All.FirstOrDefault(s =>
                   string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(s.Code, name, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private static string? AddressOf(Domain.Entities.Masters.Location? location)
    {
        if (location is null)
        {
            return null;
        }
        var cityLine = string.Join(" ", new[] { location.City, location.PostalCode }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        var parts = new[] { location.AddressLine1, location.AddressLine2, cityLine, location.State }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private async Task<IReadOnlyList<QuotationTdsOptionDto>> LoadTdsOptionsAsync(CancellationToken ct)
    {
        var raw = await settings.GetStringAsync(AppSettingKeys.Purchase.TdsOptions, DefaultTds, ct);
        var options = new List<QuotationTdsOptionDto>();
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries
                                             | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && decimal.TryParse(parts[2], NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var percent))
            {
                options.Add(new QuotationTdsOptionDto(parts[0], parts[1], percent));
            }
        }
        return options;
    }

    private static List<decimal> ParseRates(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => decimal.TryParse(r, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                ? v
                : -1m)
            .Where(v => v is >= 0 and <= 100)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

    private static List<string> Csv(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
                Supplier = db.Suppliers.Where(s => s.SupplierId == p.SupplierId)
                    .Select(s => new { s.SupplierName, s.Gstin, s.Email, s.MobileNumber, s.AddressLine, s.City, s.State, s.Pincode })
                    .FirstOrDefault(),
                LocationName = db.Locations.Where(l => l.LocationId == p.LocationId)
                    .Select(l => l.LocationName).FirstOrDefault(),
                CreatedByName = db.Users.Where(u => u.UserId == p.CreatedBy)
                    .Select(u => u.FullName).FirstOrDefault(),
                ApprovedByName = db.Users.Where(u => u.UserId == p.ApprovedByUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                Lines = p.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseOrderLineDto(
                    l.PurchaseOrderLineId, l.LineNumber, l.InventoryItemId,
                    db.InventoryItems.Where(i => i.InventoryItemId == l.InventoryItemId)
                        .Select(i => i.ItemCode).FirstOrDefault(),
                    l.ItemName, l.HsnSacCode, l.Unit, l.Quantity, l.ReceivedQuantity,
                    l.Quantity - l.ReceivedQuantity, l.Rate, l.DiscountAmount, l.LineAmount,
                    l.TaxPercent, l.TaxAmount, l.TotalAmount, l.Remarks,
                    l.CgstAmount, l.SgstAmount, l.IgstAmount, l.AccountCategory)).ToList(),
                Attachments = p.Attachments.OrderBy(a => a.PurchaseOrderAttachmentId)
                    .Select(a => new PurchaseOrderAttachmentDto(a.PurchaseOrderAttachmentId, a.FileName,
                        a.ContentType, a.FileSizeBytes, a.UploadedUtc)).ToList(),
                History = p.StatusHistory.OrderBy(h => h.PurchaseOrderStatusHistoryId)
                    .Select(h => new PurchaseOrderHistoryDto(h.Action, (byte?)h.FromStatus, (byte)h.ToStatus,
                        db.Users.Where(u => u.UserId == h.ChangedByUserId).Select(u => u.FullName).FirstOrDefault(),
                        h.ChangedUtc, h.Reason)).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        // Preserve the page's ordering, which the id query already decided.
        return ids
            .Select(id => orders.FirstOrDefault(o => o.Order.PurchaseOrderId == id))
            .Where(o => o is not null)
            .Select(o =>
            {
                var p = o!.Order;
                var s = o.Supplier;
                var supplierAddress = s is null
                    ? null
                    : string.Join(", ", new[] { s.AddressLine, s.City, string.Join(" ", new[] { s.State, s.Pincode }.Where(x => !string.IsNullOrWhiteSpace(x))) }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                return new PurchaseOrderDto(
                    p.PurchaseOrderId, p.PoNumber, p.PoDate, p.SupplierId,
                    s?.SupplierName ?? string.Empty, s?.Gstin, p.LocationId, o.LocationName,
                    p.ExpectedDeliveryDate, p.SubTotal, p.DiscountAmount,
                    p.TaxableAmount, p.TaxAmount, p.TotalAmount, (byte)p.Status,
                    p.SentUtc, p.CancelledUtc, p.CancellationReason, p.Remarks,
                    p.AttachmentPath, p.CreatedDate, o.Lines,
                    p.ReferenceNumber, p.ShipmentPreference, p.IsReverseCharge, p.DeliveryAddress,
                    p.IsInterState, (byte?)p.DiscountType, p.DiscountValue,
                    p.CgstAmount, p.SgstAmount, p.IgstAmount,
                    p.TdsCode, p.TdsLabel, p.TdsPercent, p.TdsAmount,
                    p.AdjustmentLabel, p.AdjustmentAmount, p.TermsAndConditions,
                    p.SubmittedUtc, p.ApprovedUtc, o.ApprovedByName, o.CreatedByName,
                    s?.Email, s?.MobileNumber, string.IsNullOrWhiteSpace(supplierAddress) ? null : supplierAddress,
                    o.Attachments, o.History);
            })
            .ToList();
    }
}
