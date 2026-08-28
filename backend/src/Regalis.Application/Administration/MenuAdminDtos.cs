using FluentValidation;

namespace Regalis.Application.Administration;

public record MenuDto(
    int MenuId,
    int? ParentMenuId,
    string MenuCode,
    string MenuName,
    string DisplayName,
    string? Route,
    string? Icon,
    int DisplayOrder,
    int? PermissionId,
    byte PortalType,
    byte MenuLevel,
    bool IsVisible,
    bool IsActive);

public record CreateMenuRequest(
    int? ParentMenuId,
    string MenuCode,
    string MenuName,
    string DisplayName,
    string? Route,
    string? Icon,
    int DisplayOrder,
    int? PermissionId,
    byte PortalType,
    bool IsVisible);

public record UpdateMenuRequest(
    int? ParentMenuId,
    string MenuName,
    string DisplayName,
    string? Route,
    string? Icon,
    int DisplayOrder,
    int? PermissionId,
    byte PortalType,
    bool IsVisible);

public class CreateMenuRequestValidator : AbstractValidator<CreateMenuRequest>
{
    public CreateMenuRequestValidator()
    {
        RuleFor(x => x.MenuCode).NotEmpty().MaximumLength(60).Matches("^[A-Z][A-Z0-9_]*$")
            .WithMessage("Menu code must be uppercase letters, digits and underscores.");
        RuleFor(x => x.MenuName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Route).MaximumLength(200);
        RuleFor(x => x.Icon).MaximumLength(60);
        RuleFor(x => x.PortalType).Must(p => p is 1 or 2)
            .WithMessage("PortalType must be 1 (Customer) or 2 (Admin).");
    }
}

public class UpdateMenuRequestValidator : AbstractValidator<UpdateMenuRequest>
{
    public UpdateMenuRequestValidator()
    {
        RuleFor(x => x.MenuName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Route).MaximumLength(200);
        RuleFor(x => x.Icon).MaximumLength(60);
        RuleFor(x => x.PortalType).Must(p => p is 1 or 2)
            .WithMessage("PortalType must be 1 (Customer) or 2 (Admin).");
    }
}

public interface IMenuAdminService
{
    Task<List<MenuDto>> GetMenusAsync(byte? portalType, CancellationToken ct = default);
    Task<MenuDto> CreateMenuAsync(CreateMenuRequest request, CancellationToken ct = default);
    Task<MenuDto> UpdateMenuAsync(int menuId, UpdateMenuRequest request, CancellationToken ct = default);
    Task DeleteMenuAsync(int menuId, CancellationToken ct = default);
}
