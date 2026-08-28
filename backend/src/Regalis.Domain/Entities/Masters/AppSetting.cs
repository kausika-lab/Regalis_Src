using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_AppSetting — the "no hard-coding" table (Design Invariant 6).</summary>
public class AppSetting : AuditableEntity
{
    public int AppSettingId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string SettingGroup { get; set; } = string.Empty;
    /// <summary>string | int | decimal | bool | json | time.</summary>
    public string DataType { get; set; } = "string";
    public string? Description { get; set; }
    public bool IsEditableByAdmin { get; set; } = true;
    /// <summary>Secret values are never returned by the API.</summary>
    public bool IsSecret { get; set; }
}
