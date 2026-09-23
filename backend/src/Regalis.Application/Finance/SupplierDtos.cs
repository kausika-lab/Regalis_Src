using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record SupplierDto(
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    string? ContactPerson,
    string? Email,
    string? MobileNumber,
    string? Gstin,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string? PaymentTerms,
    int? LocationId,
    string? LocationName,
    string? Notes,
    int OpenPurchaseOrders,
    bool IsActive);

public record SaveSupplierRequest(
    string SupplierCode,
    string SupplierName,
    string? ContactPerson,
    string? Email,
    string? MobileNumber,
    string? Gstin,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string? PaymentTerms,
    int? LocationId,
    string? Notes);

public class SupplierListRequest : PagedRequest
{
    public int? LocationId { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.SupplierCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactPerson).MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.MobileNumber).MaximumLength(20);
        // 15 characters: 2 state + 10 PAN + 1 entity + 1 'Z' + 1 checksum.
        RuleFor(x => x.Gstin).Length(15)
            .Matches("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
            .WithMessage("Enter a valid 15-character GSTIN.")
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin));
        RuleFor(x => x.AddressLine).MaximumLength(500);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Pincode).Matches("^[1-9][0-9]{5}$")
            .WithMessage("Enter a valid 6-digit pincode.")
            .When(x => !string.IsNullOrWhiteSpace(x.Pincode));
        RuleFor(x => x.PaymentTerms).MaximumLength(100);
        RuleFor(x => x.LocationId).GreaterThan(0).When(x => x.LocationId is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

/// <summary>Supplier master (document §21, internal finance chain). LocationId is
/// optional: a supplier with no location serves every location.</summary>
public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(SupplierListRequest request,
        CancellationToken ct = default);

    Task<SupplierDto> GetSupplierAsync(int supplierId, CancellationToken ct = default);

    Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken ct = default);

    Task<SupplierDto> UpdateAsync(int supplierId, SaveSupplierRequest request,
        CancellationToken ct = default);

    /// <summary>Soft delete, refused while the supplier has purchase orders.</summary>
    Task DeleteAsync(int supplierId, CancellationToken ct = default);
}
