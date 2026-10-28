using System;
using System.Collections.Generic;
using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_CashRegisterSession — one collector's drawer at one location for one
/// IST business day. Opened with a float; closed with a count. ExpectedCash is the float
/// plus the cash receipts (UPI and card are reconciled against the bank, not the
/// drawer). An Admin approves the close and takes the cash (HandedOver).</summary>
public class CashRegisterSession : AuditableEntity
{
    public long CashRegisterSessionId { get; set; }

    /// <summary>CRS-000123 — gap-free, from MST_NumberSequence.</summary>
    public string SessionNumber { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public long UserId { get; set; }
    public DateOnly BusinessDate { get; set; }

    public decimal OpeningFloat { get; set; }
    public DateTime OpenedUtc { get; set; }

    /// <summary>Set at close: float + recorded cash receipts.</summary>
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }

    /// <summary>Computed: CountedCash - ExpectedCash.</summary>
    public decimal? Variance { get; private set; }

    public string? VarianceReason { get; set; }

    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;
    public DateTime? ClosedUtc { get; set; }
    public long? HandedOverToUserId { get; set; }
    public DateTime? HandedOverUtc { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<CashCollection> Collections { get; set; } = new List<CashCollection>();
}
