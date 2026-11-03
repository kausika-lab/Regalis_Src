using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

/// <summary>MST_ComplianceObligation - a recurring statutory date, as a row so a new one
/// needs no release. Frequency: Monthly (DueDay of the next month), Quarterly (DueMonths
/// lists the months, e.g. "1,5,7,10" for 26Q), Yearly.</summary>
public class ComplianceObligation : AuditableEntity
{
    public int ComplianceObligationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Monthly";
    public int DueDay { get; set; }
    public string? DueMonths { get; set; }

    /// <summary>TdsUnpaid, GstCash, Setting:&lt;key&gt;, or null for no amount.</summary>
    public string? AmountSource { get; set; }

    public string? OwnerRole { get; set; }
    public int DisplayOrder { get; set; }
}

public enum ComplianceState : byte
{
    NotStarted = 1,
    NotDone = 2,
    ReadyToFile = 3,
    Filed = 4
}

/// <summary>TRN_ComplianceStatus - what was done about one obligation for one period.</summary>
public class ComplianceStatus : AuditableEntity
{
    public long ComplianceStatusId { get; set; }
    public int ComplianceObligationId { get; set; }

    /// <summary>yyyy-MM of the due date.</summary>
    public string Period { get; set; } = string.Empty;

    public ComplianceState Status { get; set; } = ComplianceState.NotStarted;
    public DateOnly? CompletedOn { get; set; }
    public long? CompletedByUserId { get; set; }
    public string? Reference { get; set; }
}

/// <summary>TRN_PeriodLock - a filed month. Nothing dated inside a locked month can be
/// created, edited, cancelled or reversed (enforced by the services and by triggers on the
/// existing finance tables). Unlocking needs FINANCE.UNLOCK and a reason.</summary>
public class PeriodLock : AuditableEntity
{
    public int PeriodLockId { get; set; }

    /// <summary>yyyy-MM.</summary>
    public string Period { get; set; } = string.Empty;

    public bool IsLocked { get; set; }
    public long? LockedByUserId { get; set; }
    public DateTime? LockedUtc { get; set; }
    public long? UnlockedByUserId { get; set; }
    public DateTime? UnlockedUtc { get; set; }
    public string? UnlockReason { get; set; }
}

/// <summary>TRN_ReminderLog - every reminder: what, to whom, when, whether it was delivered.</summary>
public class ReminderLog
{
    public long ReminderLogId { get; set; }
    public string ReminderType { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime SentUtc { get; set; }
    public bool Delivered { get; set; }
    public string? Error { get; set; }
}
