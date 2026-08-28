namespace Regalis.Domain.Common;

/// <summary>Marker for entities carrying the standard audit columns,
/// set by the EF Core save-changes interceptor — never by hand.</summary>
public interface IAuditableEntity
{
    long? CreatedBy { get; set; }
    DateTime CreatedDate { get; set; }
    long? ModifiedBy { get; set; }
    DateTime? ModifiedDate { get; set; }
}

/// <summary>Marker for soft-deletable entities. A global query filter hides
/// deleted rows; nothing in Phase 1 issues a hard DELETE against a business table.</summary>
public interface ISoftDeletableEntity
{
    bool IsActive { get; set; }
    bool IsDeleted { get; set; }
    long? DeletedBy { get; set; }
    DateTime? DeletedDate { get; set; }
}

/// <summary>The audit columns present on every business table (DATABASE_DESIGN §1).</summary>
public abstract class AuditableEntity : IAuditableEntity, ISoftDeletableEntity
{
    public long? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public long? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
}
