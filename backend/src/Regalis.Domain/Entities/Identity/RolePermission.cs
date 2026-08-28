namespace Regalis.Domain.Entities.Identity;

/// <summary>MAP_RolePermission.</summary>
public class RolePermission
{
    public long RolePermissionId { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
