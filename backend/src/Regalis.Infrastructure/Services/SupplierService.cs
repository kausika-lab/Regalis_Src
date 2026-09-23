using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Supplier master (document §21). A supplier is never hard-deleted once it has
/// purchase orders — the interceptor turns a delete into a soft delete, and an
/// in-use supplier is refused outright so historical POs keep naming a real vendor.
/// </summary>
public class SupplierService(RegalisDbContext db) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> GetSuppliersAsync(SupplierListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.SupplierName.Contains(term)
                                     || s.SupplierCode.Contains(term)
                                     || (s.Gstin != null && s.Gstin.Contains(term)));
        }
        if (request.LocationId is not null)
        {
            // A supplier with no location serves every location, so it stays visible.
            query = query.Where(s => s.LocationId == request.LocationId || s.LocationId == null);
        }
        if (request.IsActive is not null)
        {
            query = query.Where(s => s.IsActive == request.IsActive);
        }

        var totalCount = await query.CountAsync(ct);
        var rows = await Shape(query.OrderBy(s => s.SupplierName)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<SupplierDto>.Create(rows, request.Page, request.PageSize, totalCount);
    }

    public async Task<SupplierDto> GetSupplierAsync(int supplierId, CancellationToken ct = default) =>
        await Shape(db.Suppliers.AsNoTracking().Where(s => s.SupplierId == supplierId))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Supplier", supplierId);

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request,
        CancellationToken ct = default)
    {
        var code = request.SupplierCode.Trim().ToUpperInvariant();
        if (await db.Suppliers.AnyAsync(s => s.SupplierCode == code, ct))
        {
            throw new ConflictException($"Supplier code {code} is already in use.",
                ErrorCodes.DuplicateRecord);
        }
        await GuardLocationAsync(request.LocationId, ct);

        var supplier = new Supplier { SupplierCode = code };
        Apply(supplier, request);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetSupplierAsync(supplier.SupplierId, ct);
    }

    public async Task<SupplierDto> UpdateAsync(int supplierId, SaveSupplierRequest request,
        CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct)
            ?? throw new NotFoundException("Supplier", supplierId);
        await GuardLocationAsync(request.LocationId, ct);

        // The code is the stable identity used on printed purchase orders.
        var code = request.SupplierCode.Trim().ToUpperInvariant();
        if (code != supplier.SupplierCode
            && await db.Suppliers.AnyAsync(s => s.SupplierCode == code && s.SupplierId != supplierId, ct))
        {
            throw new ConflictException($"Supplier code {code} is already in use.",
                ErrorCodes.DuplicateRecord);
        }
        supplier.SupplierCode = code;
        Apply(supplier, request);
        await db.SaveChangesAsync(ct);
        return await GetSupplierAsync(supplierId, ct);
    }

    public async Task DeleteAsync(int supplierId, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct)
            ?? throw new NotFoundException("Supplier", supplierId);
        if (await db.PurchaseOrders.AnyAsync(p => p.SupplierId == supplierId, ct))
        {
            throw new BusinessRuleException(
                "This supplier has purchase orders and cannot be deleted — deactivate it instead.");
        }
        db.Suppliers.Remove(supplier); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private async Task GuardLocationAsync(int? locationId, CancellationToken ct)
    {
        if (locationId is not null
            && !await db.Locations.AnyAsync(l => l.LocationId == locationId, ct))
        {
            throw new NotFoundException("Location", locationId);
        }
    }

    private IQueryable<SupplierDto> Shape(IQueryable<Supplier> query) =>
        query.Select(s => new SupplierDto(
            s.SupplierId, s.SupplierCode, s.SupplierName, s.ContactPerson, s.Email,
            s.MobileNumber, s.Gstin, s.AddressLine, s.City, s.State, s.Pincode, s.PaymentTerms,
            s.LocationId,
            db.Locations.Where(l => l.LocationId == s.LocationId)
                .Select(l => l.LocationName).FirstOrDefault(),
            s.Notes,
            // "Open" is anything not yet fully received and not cancelled.
            db.PurchaseOrders.Count(p => p.SupplierId == s.SupplierId
                                         && p.Status != Domain.Enums.PurchaseOrderStatus.Received
                                         && p.Status != Domain.Enums.PurchaseOrderStatus.Cancelled),
            s.IsActive));

    private static void Apply(Supplier supplier, SaveSupplierRequest request)
    {
        supplier.SupplierName = request.SupplierName.Trim();
        supplier.ContactPerson = request.ContactPerson?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.MobileNumber = request.MobileNumber?.Trim();
        supplier.Gstin = request.Gstin?.Trim().ToUpperInvariant();
        supplier.AddressLine = request.AddressLine?.Trim();
        supplier.City = request.City?.Trim();
        supplier.State = request.State?.Trim();
        supplier.Pincode = request.Pincode?.Trim();
        supplier.PaymentTerms = request.PaymentTerms?.Trim();
        supplier.LocationId = request.LocationId;
        supplier.Notes = request.Notes?.Trim();
    }
}
