using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Identity;

/// <summary>MST_Role.</summary>
public class Role : AuditableEntity
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>System roles cannot be deleted.</summary>
    public bool IsSystemRole { get; set; }
    public PortalType PortalType { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<RoleMenu> RoleMenus { get; set; } = new List<RoleMenu>();
}
