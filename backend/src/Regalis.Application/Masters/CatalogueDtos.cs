using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Masters;

// ── Services & Coffee (M22) ─────────────────────────────────────────────────

public record ServiceItemDto(
    int ServiceItemId,
    string ItemCode,
    string ItemName,
    string Category,
    decimal Price,
    string? Unit,
    string? ImagePath,
    string? Description,
    bool IsAvailable,
    int DisplayOrder,
    int? LocationId,
    bool IsActive);

public record SaveServiceItemRequest(
    string ItemCode,
    string ItemName,
    string Category,
    decimal Price,
    string? Unit,
    string? ImagePath,
    string? Description,
    bool IsAvailable,
    int DisplayOrder,
    int? LocationId);

public class ServiceItemListRequest : PagedRequest
{
    public string? Category { get; set; }
    public int? LocationId { get; set; }
    /// <summary>Admin grids pass true; the customer catalogue sees available only.</summary>
    public bool? IncludeUnavailable { get; set; }
}

public class SaveServiceItemRequestValidator : AbstractValidator<SaveServiceItemRequest>
{
    public SaveServiceItemRequestValidator()
    {
        RuleFor(x => x.ItemCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.ImagePath).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public interface IServiceCatalogueService
{
    Task<PagedResult<ServiceItemDto>> GetItemsAsync(ServiceItemListRequest request,
        CancellationToken ct = default);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ServiceItemDto> GetItemAsync(int itemId, CancellationToken ct = default);
    Task<ServiceItemDto> CreateItemAsync(SaveServiceItemRequest request, CancellationToken ct = default);
    Task<ServiceItemDto> UpdateItemAsync(int itemId, SaveServiceItemRequest request,
        CancellationToken ct = default);
    Task DeleteItemAsync(int itemId, CancellationToken ct = default);
}

// ── Banners (M23) ───────────────────────────────────────────────────────────

public record BannerDto(
    int BannerId,
    string Title,
    string? Subtitle,
    string ImagePath,
    string? LinkUrl,
    decimal? PromotionalPrice,
    int DisplayOrder,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    int? LocationId,
    // 1 Customer, 2 Admin, 3 Both.
    byte PortalType,
    bool IsActive);

public record SaveBannerRequest(
    string Title,
    string? Subtitle,
    string ImagePath,
    string? LinkUrl,
    decimal? PromotionalPrice,
    int DisplayOrder,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    int? LocationId,
    byte PortalType);

public class SaveBannerRequestValidator : AbstractValidator<SaveBannerRequest>
{
    public SaveBannerRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Subtitle).MaximumLength(250);
        RuleFor(x => x.ImagePath).NotEmpty().MaximumLength(500);
        RuleFor(x => x.LinkUrl).MaximumLength(500);
        RuleFor(x => x.PromotionalPrice).GreaterThanOrEqualTo(0).When(x => x.PromotionalPrice is not null);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidToUtc).GreaterThan(x => x.ValidFromUtc)
            .WithMessage("The banner must stop showing after it starts.");
        RuleFor(x => x.PortalType).Must(p => p is 1 or 2 or 3)
            .WithMessage("PortalType must be 1 (Customer), 2 (Admin) or 3 (Both).");
    }
}

public interface IBannerService
{
    Task<PagedResult<BannerDto>> GetBannersAsync(PagedRequest request, CancellationToken ct = default);
    /// <summary>Banners inside their validity window for the caller's portal —
    /// the home-screen carousel source.</summary>
    Task<List<BannerDto>> GetActiveBannersAsync(int? locationId, CancellationToken ct = default);
    Task<BannerDto> CreateBannerAsync(SaveBannerRequest request, CancellationToken ct = default);
    Task<BannerDto> UpdateBannerAsync(int bannerId, SaveBannerRequest request,
        CancellationToken ct = default);
    Task DeleteBannerAsync(int bannerId, CancellationToken ct = default);
}

// ── Settings (M23) ──────────────────────────────────────────────────────────

public record AppSettingDto(
    int AppSettingId,
    string SettingKey,
    // Masked ("••••") when IsSecret — secret values never leave the server.
    string SettingValue,
    string SettingGroup,
    string DataType,
    string? Description,
    bool IsEditableByAdmin,
    bool IsSecret);

public record AppSettingValue(string SettingKey, string SettingValue);

public record UpdateAppSettingsRequest(List<AppSettingValue> Settings);

/// <summary>The anonymous Contact Us payload — support and company basics only,
/// nothing operational (API_PLAN: GET /settings/public).</summary>
public record PublicSettingsDto(
    string CompanyName,
    string? CompanyEmail,
    string? CompanyPhone,
    string? CompanyAddress,
    string? SupportContactNumber,
    string? SupportEmail,
    string? SupportHours,
    string? SupportMessage);

public class UpdateAppSettingsRequestValidator : AbstractValidator<UpdateAppSettingsRequest>
{
    public UpdateAppSettingsRequestValidator()
    {
        RuleFor(x => x.Settings).NotEmpty();
        RuleForEach(x => x.Settings).ChildRules(s =>
        {
            s.RuleFor(v => v.SettingKey).NotEmpty().MaximumLength(100);
            s.RuleFor(v => v.SettingValue).NotNull().MaximumLength(2000);
        });
    }
}

public interface ISettingAdminService
{
    Task<List<AppSettingDto>> GetSettingsAsync(CancellationToken ct = default);
    /// <summary>Updates editable, non-secret settings and evicts the read cache so
    /// the new values apply immediately.</summary>
    Task<List<AppSettingDto>> UpdateSettingsAsync(UpdateAppSettingsRequest request,
        CancellationToken ct = default);
    Task<PublicSettingsDto> GetPublicSettingsAsync(CancellationToken ct = default);
}
