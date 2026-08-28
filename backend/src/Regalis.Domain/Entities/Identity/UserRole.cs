namespace Regalis.Domain.Entities.Identity;

/// <summary>MAP_UserRole.</summary>
public class UserRole
{
    public long UserRoleId { get; set; }
    public long UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedDateUtc { get; set; }
    public long? AssignedBy { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
