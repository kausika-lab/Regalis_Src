using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Identity;

/// <summary>MST_Menu — dynamic navigation, four levels via self-reference:
/// Module → Menu → Sub-menu → Screen. Adding a screen is a data insert, not a redeploy.</summary>
public class Menu : AuditableEntity
{
    public int MenuId { get; set; }
    public int? ParentMenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Null for a pure grouping node.</summary>
    public string? Route { get; set; }
    /// <summary>lucide-react icon name, resolved by the frontend.</summary>
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>The permission that reveals this menu.</summary>
    public int? PermissionId { get; set; }
    public PortalType PortalType { get; set; }
    public MenuLevel MenuLevel { get; set; }
    public bool IsVisible { get; set; } = true;

    public Menu? ParentMenu { get; set; }
    public ICollection<Menu> Children { get; set; } = new List<Menu>();
    public Permission? Permission { get; set; }
    public ICollection<RoleMenu> RoleMenus { get; set; } = new List<RoleMenu>();
}
