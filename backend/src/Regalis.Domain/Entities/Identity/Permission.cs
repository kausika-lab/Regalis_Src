using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Identity;

/// <summary>MST_Permission — code format MODULE.ACTION.</summary>
public class Permission : AuditableEntity
{
    public int PermissionId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
