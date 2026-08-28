using FluentValidation;

namespace Regalis.Application.Administration;

public record RoleDto(
    int RoleId,
    string RoleCode,
    string RoleName,
    string? Description,
    byte PortalType,
    bool IsSystemRole,
    bool IsActive,
    int UserCount,
    int PermissionCount);

public record CreateRoleRequest(string RoleCode, string RoleName, string? Description, byte PortalType);

public record UpdateRoleRequest(string RoleName, string? Description, byte PortalType, bool IsActive);

/// <summary>One row of the permission-matrix editor: a permission with its grant flag.</summary>
public record RolePermissionRowDto(
    int PermissionId,
    string PermissionCode,
    string ModuleName,
    string ActionName,
    bool IsGranted);

public record UpdateRolePermissionsRequest(int[] PermissionIds);

public record RoleMenuRowDto(int MenuId, string MenuCode, string DisplayName, byte PortalType, bool IsGranted);

public record UpdateRoleMenusRequest(int[] MenuIds);

public record PermissionDto(int PermissionId, string PermissionCode, string ModuleName, string ActionName);

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(50).Matches("^[A-Z][A-Z0-9_]*$")
            .WithMessage("Role code must be uppercase letters, digits and underscores.");
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.PortalType).Must(p => p is 1 or 2 or 3)
            .WithMessage("PortalType must be 1 (Customer), 2 (Admin) or 3 (Both).");
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.PortalType).Must(p => p is 1 or 2 or 3)
            .WithMessage("PortalType must be 1 (Customer), 2 (Admin) or 3 (Both).");
    }
}

public interface IRoleAdminService
{
    Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDto> UpdateRoleAsync(int roleId, UpdateRoleRequest request, CancellationToken ct = default);
    Task DeleteRoleAsync(int roleId, CancellationToken ct = default);
    Task<List<RolePermissionRowDto>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default);
    Task UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequest request, CancellationToken ct = default);
    Task<List<RoleMenuRowDto>> GetRoleMenusAsync(int roleId, CancellationToken ct = default);
    Task UpdateRoleMenusAsync(int roleId, UpdateRoleMenusRequest request, CancellationToken ct = default);
    Task<List<PermissionDto>> GetPermissionCatalogueAsync(CancellationToken ct = default);
}
