namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_NumberSequence — atomic, gap-free counters for customer-visible
/// numbers (REG-000123, REG/INV/2026-27/00042). IDENTITY is not used for these
/// because it leaks volume and skips on rollback.</summary>
public class NumberSequence
{
    public int NumberSequenceId { get; set; }
    public string SequenceKey { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public long CurrentValue { get; set; }
    /// <summary>e.g. "2026-27" for invoice numbering; null for year-less sequences.</summary>
    public string? FinancialYear { get; set; }
}
