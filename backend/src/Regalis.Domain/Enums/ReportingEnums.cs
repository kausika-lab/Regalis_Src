namespace Regalis.Domain.Enums;

public enum ReportFrequency : byte
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum ReportFormat : byte
{
    Excel = 1,
    Pdf = 2
}

public enum ReportRunOutcome : byte
{
    Sent = 1,
    Failed = 2,
    /// <summary>"Only if there is data" was ticked and the run found no rows.</summary>
    SkippedEmpty = 3
}

public enum ReportExportDelivery : byte
{
    Download = 1,
    EmailedLink = 2,
    Scheduled = 3
}

public enum ReportExportJobStatus : byte
{
    Queued = 1,
    Ready = 2,
    Failed = 3,
    Expired = 4
}
