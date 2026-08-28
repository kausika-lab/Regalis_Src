namespace Regalis.Domain.Entities.Identity;

/// <summary>MAP_RoleMenu.</summary>
public class RoleMenu
{
    public long RoleMenuId { get; set; }
    public int RoleId { get; set; }
    public int MenuId { get; set; }

    public Role Role { get; set; } = null!;
    public Menu Menu { get; set; } = null!;
}
