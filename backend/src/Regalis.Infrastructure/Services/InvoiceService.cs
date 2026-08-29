using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Reporting;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

public class InvoiceService(RegalisDbContext db, ICurrentUser currentUser) : IInvoiceService
{
    public async Task<InvoiceDto> GetInvoiceAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadScopedAsync(invoiceId, ct);
        return ToDto(invoice);
    }

    public async Task<PagedResult<InvoiceDto>> GetMyInvoicesAsync(InvoiceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == currentUser.UserId);
        query = request.Status?.ToLowerInvariant() switch
        {
            "pending" => query.Where(i =>
                i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.PartiallyPaid),
            "paid" => query.Where(i => i.Status == InvoiceStatus.Paid),
            _ => query
        };
        query = query.OrderByDescending(i => i.InvoiceId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(i => i.Lines)
            .ToListAsync(ct);
        return PagedResult<InvoiceDto>.Create(
            items.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(AdminInvoiceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Invoices.AsNoTracking();
        if (request.Status is not null)
        {
            query = query.Where(i => (byte)i.Status == request.Status);
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(i => i.CustomerId == request.CustomerId);
        }
        if (request.From is not null)
        {
            query = query.Where(i => i.InvoiceDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(i => i.InvoiceDate <= request.To);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term) ||
                                     i.CustomerName.Contains(term) ||
                                     i.CustomerEmail.Contains(term));
        }
        query = query.OrderByDescending(i => i.InvoiceId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(i => i.Lines)
            .ToListAsync(ct);
        return PagedResult<InvoiceDto>.Create(
            items.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<byte[]> GetInvoicePdfAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadScopedAsync(invoiceId, ct);
        return InvoicePdfGenerator.Generate(invoice);
    }

    /// <summary>Customers read their own invoices only, whatever id they pass.</summary>
    private async Task<Invoice> LoadScopedAsync(long invoiceId, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines.OrderBy(l => l.LineNumber))
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Invoice", invoiceId);
        if (currentUser.UserType == UserType.Customer && invoice.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Invoice", invoiceId);
        }
        return invoice;
    }

    private static InvoiceDto ToDto(Invoice i) => new(
        i.InvoiceId, i.InvoiceNumber, i.CustomerId, i.BookingId, i.InvoiceDate, i.DueDate,
        i.SubTotal, i.DiscountAmount, i.TaxableAmount,
        i.CgstPercent, i.CgstAmount, i.SgstPercent, i.SgstAmount,
        i.TotalAmount, i.PaidAmount, i.OutstandingAmount, (byte)i.Status,
        i.CustomerName, i.CustomerEmail, i.CompanyName, i.CompanyGstin,
        i.Lines.Select(l => new InvoiceLineDto(
            l.LineNumber, l.Description, l.HsnSacCode, l.Quantity, l.UnitPrice,
            l.LineAmount, l.TaxPercent, l.TaxAmount, l.TotalAmount)).ToList());
}
