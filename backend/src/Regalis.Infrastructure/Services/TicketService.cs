using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>Attachment storage location — outside the web root, streamed back only
/// through the authorized download endpoint.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";
    public string UploadRoot { get; set; } = "App_Data/uploads";
}

/// <summary>
/// Support tickets (M21). Row scoping: customers see only their own tickets and
/// never internal notes. Attachments are checked by extension, size AND leading
/// magic bytes — a renamed executable does not get stored.
/// </summary>
public class TicketService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<StorageOptions> storageOptions) : ITicketService
{
    private const int MaxAttachmentsPerTicket = 5;
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private bool IsCustomer => currentUser.UserType == UserType.Customer;

    public async Task<List<TicketCategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        (await db.TicketCategories.AsNoTracking()
            .Include(c => c.IssueTypes)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
            .ToListAsync(ct))
        .Select(c => new TicketCategoryDto(
            c.TicketCategoryId, c.CategoryName, c.DisplayOrder,
            c.IssueTypes.OrderBy(t => t.DisplayOrder).ThenBy(t => t.IssueTypeName)
                .Select(t => new TicketIssueTypeDto(t.TicketIssueTypeId, t.IssueTypeName, t.DisplayOrder))
                .ToList()))
        .ToList();

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request,
        CancellationToken ct = default)
    {
        var issueTypeValid = await db.TicketIssueTypes.AnyAsync(t =>
            t.TicketIssueTypeId == request.TicketIssueTypeId &&
            t.TicketCategoryId == request.TicketCategoryId, ct);
        if (!issueTypeValid)
        {
            throw new BusinessRuleException("The issue type does not belong to the selected category.");
        }

        var ticket = new Ticket
        {
            TicketNumber = await numberSequence.NextAsync(SequenceKeys.Ticket, ct),
            CustomerId = currentUser.UserId,
            TicketCategoryId = request.TicketCategoryId,
            TicketIssueTypeId = request.TicketIssueTypeId,
            LocationId = currentUser.LocationId
                ?? await db.Locations.Where(l => l.IsDefault).Select(l => l.LocationId).FirstAsync(ct),
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Priority = (TicketPriority)request.Priority,
            Status = TicketStatus.Open
        };
        db.Tickets.Add(ticket);

        db.Notifications.Add(new Notification
        {
            Audience = NotificationAudience.AllAdmins,
            Title = "New support ticket",
            Message = $"{ticket.TicketNumber}: {ticket.Subject}",
            Category = NotificationCategory.SupportTicket,
            Priority = ticket.Priority >= TicketPriority.High
                ? NotificationPriority.High
                : NotificationPriority.Normal,
            ReferenceType = "Ticket",
            ActionRoute = "/admin/tickets",
            ActionLabel = "Open triage",
            SentUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticket.TicketId, ct);
    }

    public async Task<PagedResult<TicketListItemDto>> GetMyAsync(PagedRequest request,
        CancellationToken ct = default) =>
        await PageAsync(db.Tickets.AsNoTracking()
            .Where(t => t.CustomerId == currentUser.UserId), request, ct);

    public async Task<PagedResult<TicketListItemDto>> GetTicketsAsync(TicketListRequest request,
        CancellationToken ct = default)
    {
        if (IsCustomer)
        {
            return await GetMyAsync(request, ct);
        }
        var query = db.Tickets.AsNoTracking();
        if (request.Status is not null)
        {
            query = query.Where(t => (byte)t.Status == request.Status);
        }
        if (request.Priority is not null)
        {
            query = query.Where(t => (byte)t.Priority == request.Priority);
        }
        if (request.TicketCategoryId is not null)
        {
            query = query.Where(t => t.TicketCategoryId == request.TicketCategoryId);
        }
        if (request.AssignedToUserId is not null)
        {
            query = query.Where(t => t.AssignedToUserId == request.AssignedToUserId);
        }
        if (request.From is not null)
        {
            var fromUtc = IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.CreatedDate >= fromUtc);
        }
        if (request.To is not null)
        {
            var toUtc = IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.CreatedDate < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t => t.TicketNumber.Contains(term) || t.Subject.Contains(term));
        }
        return await PageAsync(query, request, ct);
    }

    public async Task<TicketDetailDto> GetTicketAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(t => t.TicketCategory)
            .Include(t => t.TicketIssueType)
            .Include(t => t.Messages)
            .Include(t => t.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);
        EnsureCanSee(ticket);

        var userIds = ticket.Messages.Select(m => m.SenderUserId)
            .Append(ticket.CustomerId)
            .Concat(ticket.AssignedToUserId is null ? [] : [ticket.AssignedToUserId.Value])
            .Distinct()
            .ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var messages = ticket.Messages
            .Where(m => !m.IsInternalNote || !IsCustomer)
            .OrderBy(m => m.SentUtc)
            .Select(m => new TicketMessageDto(
                m.TicketMessageId, m.SenderUserId,
                names.GetValueOrDefault(m.SenderUserId, "—"),
                (byte)m.SenderType, m.Message, m.IsInternalNote, m.SentUtc))
            .ToList();
        var attachments = ticket.Attachments
            .OrderBy(a => a.UploadedUtc)
            .Select(a => new TicketAttachmentDto(
                a.TicketAttachmentId, a.TicketMessageId, a.FileName, a.ContentType,
                a.FileSizeBytes, a.UploadedByUserId, a.UploadedUtc))
            .ToList();

        return new TicketDetailDto(
            ticket.TicketId, ticket.TicketNumber, ticket.CustomerId,
            names.GetValueOrDefault(ticket.CustomerId, "—"),
            ticket.TicketCategoryId, ticket.TicketCategory.CategoryName,
            ticket.TicketIssueTypeId, ticket.TicketIssueType.IssueTypeName,
            ticket.Subject, ticket.Description, (byte)ticket.Priority, (byte)ticket.Status,
            ticket.AssignedToUserId,
            ticket.AssignedToUserId is null ? null : names.GetValueOrDefault(ticket.AssignedToUserId.Value),
            ticket.FirstResponseUtc, ticket.ResolvedUtc, ticket.ClosedUtc, ticket.ResolutionNotes,
            ticket.ReopenCount, ticket.CreatedDate, messages, attachments);
    }

    public async Task<TicketDetailDto> AddMessageAsync(long ticketId, AddTicketMessageRequest request,
        CancellationToken ct = default)
    {
        var ticket = await LoadForWriteAsync(ticketId, ct);
        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("This ticket is closed. Reopen it to continue the conversation.");
        }

        var nowUtc = clock.UtcNow;
        var senderType = IsCustomer ? TicketSenderType.Customer : TicketSenderType.Admin;
        db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.TicketId,
            SenderUserId = currentUser.UserId,
            SenderType = senderType,
            Message = request.Message.Trim(),
            IsInternalNote = request.IsInternalNote && !IsCustomer,
            SentUtc = nowUtc
        });

        if (senderType == TicketSenderType.Admin && !request.IsInternalNote)
        {
            ticket.FirstResponseUtc ??= nowUtc;
            if (ticket.Status is TicketStatus.Open or TicketStatus.Assigned or TicketStatus.Reopened)
            {
                ticket.Status = TicketStatus.InProgress;
            }
            NotifyCustomer(ticket, "Support replied",
                $"{ticket.TicketNumber}: a new reply on \"{ticket.Subject}\".");
        }
        else if (senderType == TicketSenderType.Customer)
        {
            // The customer answering a resolved ticket reopens the conversation.
            if (ticket.Status == TicketStatus.Resolved)
            {
                ticket.Status = TicketStatus.Reopened;
                ticket.ReopenCount++;
                ticket.ResolvedUtc = null;
            }
            else if (ticket.Status == TicketStatus.WaitingForCustomer)
            {
                ticket.Status = TicketStatus.InProgress;
            }
        }
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketDetailDto> AddAttachmentAsync(long ticketId, AttachmentUpload upload,
        CancellationToken ct = default)
    {
        var ticket = await LoadForWriteAsync(ticketId, ct);
        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("This ticket is closed.");
        }
        if (await db.TicketAttachments.CountAsync(a => a.TicketId == ticketId, ct) >=
            MaxAttachmentsPerTicket)
        {
            throw new BusinessRuleException($"A ticket carries at most {MaxAttachmentsPerTicket} attachments.");
        }
        if (upload.Length is <= 0 or > MaxAttachmentBytes)
        {
            throw new BusinessRuleException("Attachments are limited to 10 MB.");
        }
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedTypes.TryGetValue(extension, out var contentType))
        {
            throw new BusinessRuleException("Allowed file types: .png .jpg .jpeg .webp .pdf");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > MaxAttachmentBytes)
        {
            throw new BusinessRuleException("Attachments are limited to 10 MB.");
        }
        if (!MatchesMagicBytes(extension, bytes))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "tickets", ticket.TicketId.ToString());
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, storedFileName);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        db.TicketAttachments.Add(new TicketAttachment
        {
            TicketId = ticket.TicketId,
            FileName = Path.GetFileName(upload.FileName),
            StoredFileName = storedFileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketAttachmentContent> GetAttachmentAsync(long ticketId, long attachmentId,
        CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);
        EnsureCanSee(ticket);
        var attachment = await db.TicketAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TicketAttachmentId == attachmentId && a.TicketId == ticketId, ct)
            ?? throw new NotFoundException("TicketAttachment", attachmentId);
        if (!File.Exists(attachment.FilePath))
        {
            throw new NotFoundException("TicketAttachment", attachmentId);
        }
        return new TicketAttachmentContent(
            await File.ReadAllBytesAsync(attachment.FilePath, ct),
            attachment.ContentType, attachment.FileName);
    }

    public async Task<TicketDetailDto> AssignAsync(long ticketId, AssignTicketRequest request,
        CancellationToken ct = default)
    {
        var ticket = await LoadAdminTicketAsync(ticketId, ct);
        var assignee = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.AssignedToUserId, ct)
            ?? throw new NotFoundException("User", request.AssignedToUserId);
        if (assignee.UserType == UserType.Customer)
        {
            throw new BusinessRuleException("Tickets are assigned to admin-portal users.");
        }
        ticket.AssignedToUserId = assignee.UserId;
        ticket.AssignedUtc = clock.UtcNow;
        if (ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.Reopened)
        {
            ticket.Status = TicketStatus.Assigned;
        }
        db.Notifications.Add(new Notification
        {
            UserId = assignee.UserId,
            Audience = NotificationAudience.SingleUser,
            Title = "Ticket assigned to you",
            Message = $"{ticket.TicketNumber}: {ticket.Subject}",
            Category = NotificationCategory.SupportTicket,
            ReferenceType = "Ticket",
            ReferenceId = ticket.TicketId,
            SentUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(long ticketId,
        ChangeTicketStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAdminTicketAsync(ticketId, ct);
        var to = (TicketStatus)request.Status;
        var nowUtc = clock.UtcNow;
        if (to == ticket.Status)
        {
            return await GetTicketAsync(ticketId, ct);
        }
        switch (to)
        {
            case TicketStatus.Resolved:
                ticket.ResolvedUtc = nowUtc;
                ticket.ResolutionNotes = request.ResolutionNotes?.Trim() ?? ticket.ResolutionNotes;
                break;
            case TicketStatus.Closed:
                ticket.ClosedUtc = nowUtc;
                ticket.ResolutionNotes = request.ResolutionNotes?.Trim() ?? ticket.ResolutionNotes;
                break;
            case TicketStatus.Reopened:
                ticket.ReopenCount++;
                ticket.ResolvedUtc = null;
                ticket.ClosedUtc = null;
                break;
            case TicketStatus.Open:
            case TicketStatus.Assigned:
            case TicketStatus.InProgress:
            case TicketStatus.WaitingForCustomer:
                break;
            default:
                throw new BusinessRuleException("Unknown ticket status.");
        }
        ticket.Status = to;
        NotifyCustomer(ticket, "Ticket status updated",
            $"{ticket.TicketNumber} is now {to}.");
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketDetailDto> ChangePriorityAsync(long ticketId,
        ChangeTicketPriorityRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAdminTicketAsync(ticketId, ct);
        ticket.Priority = (TicketPriority)request.Priority;
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketDetailDto> CloseAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await LoadForWriteAsync(ticketId, ct);
        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("The ticket is already closed.");
        }
        ticket.Status = TicketStatus.Closed;
        ticket.ClosedUtc = clock.UtcNow;
        if (!IsCustomer)
        {
            NotifyCustomer(ticket, "Ticket closed", $"{ticket.TicketNumber} has been closed.");
        }
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketDetailDto> ReopenAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await LoadForWriteAsync(ticketId, ct);
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            throw new BusinessRuleException("Only a resolved or closed ticket can be reopened.");
        }
        ticket.Status = TicketStatus.Reopened;
        ticket.ReopenCount++;
        ticket.ResolvedUtc = null;
        ticket.ClosedUtc = null;
        await db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticketId, ct);
    }

    public async Task<TicketExportResult> ExportAsync(TicketListRequest request,
        CancellationToken ct = default)
    {
        request.Page = 1;
        request.PageSize = PagedRequest.MaxPageSize;
        var rows = new List<TicketListItemDto>();
        PagedResult<TicketListItemDto> page;
        do
        {
            page = await GetTicketsAsync(request, ct);
            rows.AddRange(page.Items);
            request.Page++;
        } while (page.HasNext && rows.Count < 10_000);

        var csv = new StringBuilder();
        csv.AppendLine("TicketNumber,Subject,Category,IssueType,Customer,Priority,Status,AssignedTo,CreatedIST,Messages");
        foreach (var r in rows)
        {
            csv.Append(r.TicketNumber).Append(',')
                .Append(Escape(r.Subject)).Append(',')
                .Append(Escape(r.CategoryName)).Append(',')
                .Append(Escape(r.IssueTypeName)).Append(',')
                .Append(Escape(r.CustomerName)).Append(',')
                .Append((TicketPriority)r.Priority).Append(',')
                .Append((TicketStatus)r.Status).Append(',')
                .Append(Escape(r.AssignedToName ?? "")).Append(',')
                .Append(IstClock.ToIst(r.CreatedDate).ToString("yyyy-MM-dd HH:mm")).Append(',')
                .Append(r.MessageCount)
                .AppendLine();
        }
        var content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new TicketExportResult(content, "text/csv",
            $"tickets_{IstClock.IstDateOf(clock.UtcNow):yyyyMMdd}.csv");
    }

    // ── Scoping & shaping ───────────────────────────────────────────────────

    private void EnsureCanSee(Ticket ticket)
    {
        if (IsCustomer && ticket.CustomerId != currentUser.UserId)
        {
            // 404, not 403 — existence of another customer's ticket is not disclosed.
            throw new NotFoundException("Ticket", ticket.TicketId);
        }
    }

    private async Task<Ticket> LoadForWriteAsync(long ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);
        EnsureCanSee(ticket);
        return ticket;
    }

    private async Task<Ticket> LoadAdminTicketAsync(long ticketId, CancellationToken ct)
    {
        if (IsCustomer)
        {
            throw new NotFoundException("Ticket", ticketId);
        }
        return await db.Tickets.FirstOrDefaultAsync(t => t.TicketId == ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);
    }

    private void NotifyCustomer(Ticket ticket, string title, string message) =>
        db.Notifications.Add(new Notification
        {
            UserId = ticket.CustomerId,
            Audience = NotificationAudience.SingleUser,
            Title = title,
            Message = message,
            Category = NotificationCategory.SupportTicket,
            ReferenceType = "Ticket",
            ReferenceId = ticket.TicketId,
            ActionRoute = $"/support/tickets/{ticket.TicketId}",
            ActionLabel = "View ticket",
            SentUtc = clock.UtcNow
        });

    private async Task<PagedResult<TicketListItemDto>> PageAsync(IQueryable<Ticket> query,
        PagedRequest request, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.TicketId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new
            {
                t.TicketId,
                t.TicketNumber,
                t.Subject,
                CategoryName = t.TicketCategory.CategoryName,
                IssueTypeName = t.TicketIssueType.IssueTypeName,
                t.CustomerId,
                CustomerName = db.Users.Where(u => u.UserId == t.CustomerId)
                    .Select(u => u.FullName).First(),
                t.Priority,
                t.Status,
                t.AssignedToUserId,
                AssignedToName = t.AssignedToUserId == null
                    ? null
                    : db.Users.Where(u => u.UserId == t.AssignedToUserId)
                        .Select(u => u.FullName).First(),
                t.CreatedDate,
                LastMessageUtc = t.Messages.Max(m => (DateTime?)m.SentUtc),
                MessageCount = t.Messages.Count(m => !m.IsInternalNote || !IsCustomer)
            })
            .ToListAsync(ct);
        var dtos = items.Select(x => new TicketListItemDto(
                x.TicketId, x.TicketNumber, x.Subject, x.CategoryName, x.IssueTypeName,
                x.CustomerId, x.CustomerName, (byte)x.Priority, (byte)x.Status,
                x.AssignedToUserId, x.AssignedToName, x.CreatedDate, x.LastMessageUtc, x.MessageCount))
            .ToList();
        return PagedResult<TicketListItemDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }

    private static bool MatchesMagicBytes(string extension, byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }
        return extension.ToLowerInvariant() switch
        {
            ".png" => bytes is [0x89, 0x50, 0x4E, 0x47, ..],
            ".jpg" or ".jpeg" => bytes is [0xFF, 0xD8, 0xFF, ..],
            ".webp" => bytes is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..],
            ".pdf" => bytes is [0x25, 0x50, 0x44, 0x46, ..],
            _ => false
        };
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
