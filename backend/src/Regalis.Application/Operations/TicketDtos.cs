using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

// ── Masters ─────────────────────────────────────────────────────────────────

public record TicketIssueTypeDto(int TicketIssueTypeId, string IssueTypeName, int DisplayOrder);

public record TicketCategoryDto(
    int TicketCategoryId,
    string CategoryName,
    int DisplayOrder,
    List<TicketIssueTypeDto> IssueTypes);

// ── Tickets ─────────────────────────────────────────────────────────────────

public record TicketListItemDto(
    long TicketId,
    string TicketNumber,
    string Subject,
    string CategoryName,
    string IssueTypeName,
    long CustomerId,
    string CustomerName,
    // 1 Low, 2 Medium, 3 High, 4 Critical.
    byte Priority,
    // 1 Open, 2 Assigned, 3 InProgress, 4 WaitingForCustomer, 5 Resolved, 6 Closed, 7 Reopened.
    byte Status,
    long? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedDate,
    DateTime? LastMessageUtc,
    int MessageCount);

public record TicketMessageDto(
    long TicketMessageId,
    long SenderUserId,
    string SenderName,
    // 1 Customer, 2 Admin.
    byte SenderType,
    string Message,
    bool IsInternalNote,
    DateTime SentUtc);

public record TicketAttachmentDto(
    long TicketAttachmentId,
    long? TicketMessageId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    long UploadedByUserId,
    DateTime UploadedUtc);

public record TicketDetailDto(
    long TicketId,
    string TicketNumber,
    long CustomerId,
    string CustomerName,
    int TicketCategoryId,
    string CategoryName,
    int TicketIssueTypeId,
    string IssueTypeName,
    string Subject,
    string Description,
    byte Priority,
    byte Status,
    long? AssignedToUserId,
    string? AssignedToName,
    DateTime? FirstResponseUtc,
    DateTime? ResolvedUtc,
    DateTime? ClosedUtc,
    string? ResolutionNotes,
    int ReopenCount,
    DateTime CreatedDate,
    List<TicketMessageDto> Messages,
    List<TicketAttachmentDto> Attachments);

public record CreateTicketRequest(
    int TicketCategoryId,
    int TicketIssueTypeId,
    string Subject,
    string Description,
    byte Priority);

public record AddTicketMessageRequest(string Message, bool IsInternalNote);

public record AssignTicketRequest(long AssignedToUserId);

public record ChangeTicketStatusRequest(byte Status, string? ResolutionNotes);

public record ChangeTicketPriorityRequest(byte Priority);

public class TicketListRequest : PagedRequest
{
    public byte? Status { get; set; }
    public byte? Priority { get; set; }
    public int? TicketCategoryId { get; set; }
    public long? AssignedToUserId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>An uploaded file handed from the controller to the service — the
/// application layer never sees IFormFile.</summary>
public record AttachmentUpload(string FileName, string? ContentType, long Length, Stream Content);

public record TicketAttachmentContent(byte[] Content, string ContentType, string FileName);

public record TicketExportResult(byte[] Content, string ContentType, string FileName);

// ── Validators ──────────────────────────────────────────────────────────────

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.TicketCategoryId).GreaterThan(0);
        RuleFor(x => x.TicketIssueTypeId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Priority).InclusiveBetween((byte)1, (byte)4);
    }
}

public class AddTicketMessageRequestValidator : AbstractValidator<AddTicketMessageRequest>
{
    public AddTicketMessageRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(x => x.Status).InclusiveBetween((byte)1, (byte)7);
        RuleFor(x => x.ResolutionNotes).MaximumLength(2000);
    }
}

public class ChangeTicketPriorityRequestValidator : AbstractValidator<ChangeTicketPriorityRequest>
{
    public ChangeTicketPriorityRequestValidator()
    {
        RuleFor(x => x.Priority).InclusiveBetween((byte)1, (byte)4);
    }
}

// ── Service ─────────────────────────────────────────────────────────────────

/// <summary>Support tickets (M21). Customers are scoped to their own tickets and
/// never see internal notes; every attachment is validated by extension, size AND
/// magic bytes before it is stored outside the web root.</summary>
public interface ITicketService
{
    Task<List<TicketCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);

    Task<PagedResult<TicketListItemDto>> GetMyAsync(PagedRequest request, CancellationToken ct = default);

    /// <summary>The admin triage grid; customer callers fall back to own rows.</summary>
    Task<PagedResult<TicketListItemDto>> GetTicketsAsync(TicketListRequest request,
        CancellationToken ct = default);

    Task<TicketDetailDto> GetTicketAsync(long ticketId, CancellationToken ct = default);

    Task<TicketDetailDto> AddMessageAsync(long ticketId, AddTicketMessageRequest request,
        CancellationToken ct = default);

    Task<TicketDetailDto> AddAttachmentAsync(long ticketId, AttachmentUpload upload,
        CancellationToken ct = default);

    Task<TicketAttachmentContent> GetAttachmentAsync(long ticketId, long attachmentId,
        CancellationToken ct = default);

    Task<TicketDetailDto> AssignAsync(long ticketId, AssignTicketRequest request,
        CancellationToken ct = default);

    Task<TicketDetailDto> ChangeStatusAsync(long ticketId, ChangeTicketStatusRequest request,
        CancellationToken ct = default);

    Task<TicketDetailDto> ChangePriorityAsync(long ticketId, ChangeTicketPriorityRequest request,
        CancellationToken ct = default);

    Task<TicketDetailDto> CloseAsync(long ticketId, CancellationToken ct = default);

    Task<TicketDetailDto> ReopenAsync(long ticketId, CancellationToken ct = default);

    Task<TicketExportResult> ExportAsync(TicketListRequest request, CancellationToken ct = default);
}
