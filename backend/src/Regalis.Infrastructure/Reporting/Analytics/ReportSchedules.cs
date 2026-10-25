using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Reporting;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>
/// When a schedule runs and which dates it covers — always in IST (spec §12.2 NOTE: a
/// monthly report for the 1st at 07:00 compared in UTC would go out on the last day of
/// the month before).
/// </summary>
public static class ScheduleClock
{
    public static DateTime NextRunUtc(ReportFrequency frequency, byte? dayOfWeek, byte? dayOfMonth, TimeOnly timeIst,
        DateTime afterUtc)
    {
        var afterIst = IstClock.ToIst(afterUtc);
        var start = DateOnly.FromDateTime(afterIst);
        for (var i = 0; i < 400; i++)
        {
            var day = start.AddDays(i);
            var matches = frequency switch
            {
                ReportFrequency.Daily => true,
                ReportFrequency.Weekly => (int)day.DayOfWeek == dayOfWeek,
                _ => dayOfMonth == 0 ? day.AddDays(1).Day == 1 : day.Day == dayOfMonth
            };
            var candidate = day.ToDateTime(timeIst);
            if (matches && candidate > afterIst)
            {
                return IstClock.ToUtc(candidate);
            }
        }
        throw new InvalidOperationException("No run date found within a year.");
    }

    /// <summary>The rolling period a run covers: yesterday, last week (Mon–Sun) or last
    /// month — the dates move with each run and are never frozen (§12.1).</summary>
    public static (DateOnly From, DateOnly To) PeriodFor(ReportFrequency frequency, DateTime runUtc)
    {
        var day = IstClock.IstDateOf(runUtc);
        switch (frequency)
        {
            case ReportFrequency.Daily:
                return (day.AddDays(-1), day.AddDays(-1));
            case ReportFrequency.Weekly:
                var monday = ReportMath.WeekStart(day);
                return (monday.AddDays(-7), monday.AddDays(-1));
            default:
                var first = new DateOnly(day.Year, day.Month, 1);
                return (first.AddMonths(-1), first.AddDays(-1));
        }
    }

    public static string HowOften(ReportFrequency frequency, byte? dayOfWeek, byte? dayOfMonth, TimeOnly time)
    {
        var at = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        return frequency switch
        {
            ReportFrequency.Daily => $"Daily, {at}",
            ReportFrequency.Weekly =>
                $"Weekly on {CultureInfo.InvariantCulture.DateTimeFormat.GetDayName((DayOfWeek)(dayOfWeek ?? 1))}, {at}",
            _ => dayOfMonth == 0 ? $"Monthly on the last day, {at}" : $"Monthly on the {Ordinal(dayOfMonth ?? 1)}, {at}"
        };
    }

    private static string Ordinal(int n) => n switch
    {
        1 or 21 => $"{n}st",
        2 or 22 => $"{n}nd",
        3 or 23 => $"{n}rd",
        _ => $"{n}th"
    };
}

internal sealed class ReportScheduleService(
    RegalisDbContext db,
    ICurrentUser user,
    IDateTimeProvider clock) : IReportScheduleService
{
    public async Task<List<ReportScheduleDto>> ListAsync(CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.Report.View))
        {
            throw new ForbiddenException("You do not have access to Reports & Analysis.");
        }
        var keys = ReportRegistry.All.Where(h => ReportRegistry.CanSee(user, h)).Select(h => h.Key).ToList();
        var schedules = await (from s in db.ReportSchedules.AsNoTracking()
                where keys.Contains(s.ReportKey)
                from u in db.Users.IgnoreQueryFilters().Where(u => u.UserId == s.CreatedByUserId)
                orderby s.IsActive descending, s.NextRunUtc
                select new { Schedule = s, CreatedBy = u.FullName })
            .ToListAsync(ct);
        return schedules.Select(x => ToDto(x.Schedule, x.CreatedBy)).ToList();
    }

    private ReportScheduleDto ToDto(ReportSchedule s, string createdBy)
    {
        var handler = ReportRegistry.Find(s.ReportKey);
        var filters = ReportJson.Deserialize(s.FiltersJson);
        return new ReportScheduleDto(s.ReportScheduleId, s.ReportKey, handler?.Name ?? s.ReportKey,
            s.Frequency.ToString().ToLowerInvariant(), s.DayOfWeek, s.DayOfMonth,
            s.TimeIst.ToString("HH:mm", CultureInfo.InvariantCulture),
            ScheduleClock.HowOften(s.Frequency, s.DayOfWeek, s.DayOfMonth, s.TimeIst),
            s.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            s.Format == ReportFormat.Pdf ? "pdf" : "xlsx", s.OnlyIfData, s.IsActive,
            s.IsActive ? s.NextRunUtc : null,
            s.IsActive ? ReportMath.FriendlyIst(s.NextRunUtc, clock.UtcNow) : null,
            s.LastRunUtc,
            s.LastRunStatus switch
            {
                ReportRunOutcome.Sent => "sent",
                ReportRunOutcome.Failed => "failed",
                ReportRunOutcome.SkippedEmpty => "skipped",
                _ => null
            },
            s.LastRunStatus == ReportRunOutcome.Failed ? s.LastError : null,
            s.DisabledReason,
            s.CreatedByUserId, createdBy, CanEdit(s), filters);
    }

    private bool CanEdit(ReportSchedule s) =>
        user.HasPermission(PermissionCodes.Report.Manage) ||
        (s.CreatedByUserId == user.UserId && user.HasPermission(PermissionCodes.Report.Export));

    public async Task<ReportScheduleDto> CreateAsync(ReportScheduleRequest request, CancellationToken ct = default)
    {
        // Anyone can be a recipient, including people who are not admins — which is why
        // creating a schedule takes the export permission (spec §12.1).
        if (!user.HasPermission(PermissionCodes.Report.Export))
        {
            throw new ForbiddenException("You need permission to export reports to schedule one.");
        }
        var schedule = new ReportSchedule
        {
            CreatedByUserId = user.UserId,
            CreatedUtc = clock.UtcNow
        };
        await ApplyAsync(schedule, request, ct);
        db.ReportSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return ToDto(schedule, user.FullName ?? "");
    }

    public async Task<ReportScheduleDto> UpdateAsync(long scheduleId, ReportScheduleRequest request,
        CancellationToken ct = default)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.ReportScheduleId == scheduleId, ct)
                       ?? throw new NotFoundException("Schedule", scheduleId);
        if (!CanEdit(schedule))
        {
            throw new ForbiddenException("Only the admin who created this schedule, or one who manages reports, can change it.");
        }
        if (string.IsNullOrWhiteSpace(request.ReportKey))
        {
            request.ReportKey = schedule.ReportKey;
        }
        await ApplyAsync(schedule, request, ct);
        schedule.ModifiedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        var createdBy = await db.Users.IgnoreQueryFilters().Where(u => u.UserId == schedule.CreatedByUserId)
            .Select(u => u.FullName).FirstAsync(ct);
        return ToDto(schedule, createdBy);
    }

    /// <summary>Stop keeps the schedule, so it can be turned back on (spec §5.4).</summary>
    public async Task StopAsync(long scheduleId, CancellationToken ct = default)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.ReportScheduleId == scheduleId, ct)
                       ?? throw new NotFoundException("Schedule", scheduleId);
        if (!CanEdit(schedule))
        {
            throw new ForbiddenException("Only the admin who created this schedule, or one who manages reports, can stop it.");
        }
        schedule.IsActive = false;
        schedule.RetryAtUtc = null;
        schedule.Attempt = 0;
        schedule.ModifiedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(ReportSchedule schedule, ReportScheduleRequest request, CancellationToken ct)
    {
        var handler = ReportRegistry.Find(request.ReportKey) ?? throw new NotFoundException("Report", request.ReportKey);
        if (!ReportRegistry.CanSee(user, handler))
        {
            throw new ForbiddenException("You do not have access to this report.");
        }
        var errors = new List<ApiError>();
        var frequency = request.Frequency?.ToLowerInvariant() switch
        {
            "daily" => ReportFrequency.Daily,
            "weekly" => ReportFrequency.Weekly,
            "monthly" => ReportFrequency.Monthly,
            _ => (ReportFrequency?)null
        };
        if (frequency is null)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Choose daily, weekly or monthly.", "frequency"));
        }
        if (frequency == ReportFrequency.Weekly && request.DayOfWeek is not (>= 0 and <= 6))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Choose the day of the week.", "dayOfWeek"));
        }
        if (frequency == ReportFrequency.Monthly && request.DayOfMonth is not (>= 0 and <= 28))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Choose a date from the 1st to the 28th, or the last day.", "dayOfMonth"));
        }
        if (!TimeOnly.TryParseExact(request.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
            time.Minute is not (0 or 30))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Choose a time on the hour or half hour.", "time"));
        }
        var recipients = request.Recipients.Select(r => r.Trim()).Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0 && user.Email is { } own)
        {
            recipients.Add(own);
        }
        if (recipients.Count is 0 or > 20)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, "Add between one and twenty email addresses.", "recipients"));
        }
        foreach (var address in recipients.Where(r => !MailAddress.TryCreate(r, out _)))
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, $"{address} is not an email address.", "recipients"));
        }
        ReportFormat format = ReportFormat.Excel;
        try
        {
            format = ReportRunService.ParseFormat(request.Format);
        }
        catch (BusinessRuleException ex)
        {
            errors.Add(new ApiError(ErrorCodes.ValidationError, ex.Message, "format"));
        }
        if (errors.Count > 0)
        {
            throw new AppValidationException(errors);
        }

        // The filters are kept without dates: every run covers the period before it.
        var filters = ReportFilterParser.Normalise(request.Filters);
        filters.Remove("from");
        filters.Remove("to");
        var lookups = await ReportLookups.LoadAsync(db, ct);
        var (_, _, resolved) = ReportFilterParser.Resolve(handler, lookups, filters, IstClock.IstDateOf(clock.UtcNow));
        resolved.Remove("from");
        resolved.Remove("to");

        var wasActive = schedule.IsActive && schedule.ReportScheduleId != 0;
        schedule.ReportKey = handler.Key;
        schedule.FiltersJson = ReportJson.Serialize(resolved);
        schedule.Frequency = frequency!.Value;
        schedule.DayOfWeek = frequency == ReportFrequency.Weekly ? (byte)request.DayOfWeek!.Value : null;
        schedule.DayOfMonth = frequency == ReportFrequency.Monthly ? (byte)request.DayOfMonth!.Value : null;
        schedule.TimeIst = time;
        schedule.Recipients = string.Join(",", recipients);
        schedule.Format = format;
        schedule.OnlyIfData = request.OnlyIfData;
        schedule.IsActive = request.IsActive ?? (schedule.ReportScheduleId == 0 || schedule.IsActive);
        if (schedule.IsActive && (!wasActive || schedule.ReportScheduleId == 0))
        {
            // Turned (back) on: a clean slate.
            schedule.DisabledReason = null;
            schedule.FailingSinceUtc = null;
            schedule.Attempt = 0;
            schedule.RetryAtUtc = null;
        }
        schedule.NextRunUtc = ScheduleClock.NextRunUtc(schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth,
            schedule.TimeIst, clock.UtcNow);
    }
}

/// <summary>
/// The background half: schedules that are due, exports too large to build while the
/// browser waits, and expired export files. Called every minute by ReportWorker.
/// </summary>
internal sealed class ReportBackgroundService(
    RegalisDbContext db,
    IDateTimeProvider clock,
    ReportFileBuilder files,
    IReportMailer mailer,
    ISettingService settings,
    IOptions<StorageOptions> storage,
    ILogger<ReportBackgroundService> logger) : IReportBackgroundService
{
    private static readonly TimeSpan RetryGap = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 3;

    public async Task ProcessDueSchedulesAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var due = await db.ReportSchedules.AsNoTracking()
            .Where(s => s.IsActive && s.NextRunUtc <= now && (s.RetryAtUtc == null || s.RetryAtUtc <= now))
            .Select(s => new { s.ReportScheduleId, s.RetryAtUtc })
            .ToListAsync(ct);
        foreach (var item in due)
        {
            // Claim it: a second app instance (an overlapping IIS recycle) sees the new
            // RetryAtUtc and leaves it alone, so a report is never sent twice.
            var lease = now.AddMinutes(9);
            var claimed = await db.ReportSchedules
                .Where(s => s.ReportScheduleId == item.ReportScheduleId && s.RetryAtUtc == item.RetryAtUtc)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.RetryAtUtc, lease), ct);
            if (claimed == 1)
            {
                await RunScheduleAsync(item.ReportScheduleId, ct);
            }
        }
    }

    private async Task RunScheduleAsync(long scheduleId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var schedule = await db.ReportSchedules.FirstAsync(s => s.ReportScheduleId == scheduleId, ct);
        var now = clock.UtcNow;
        var (from, to) = ScheduleClock.PeriodFor(schedule.Frequency, schedule.NextRunUtc);
        var recipients = schedule.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var attempt = (byte)(schedule.Attempt + 1);
        var rowCount = 0;
        string? error;
        var outcome = ReportRunOutcome.Sent;
        try
        {
            (rowCount, outcome, error) = await BuildAndSendAsync(schedule, from, to, recipients, now, ct);
        }
        catch (AppException ex)
        {
            error = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled report {ScheduleId} ({Report}) failed", schedule.ReportScheduleId, schedule.ReportKey);
            error = "The report could not be built.";
        }

        db.ReportScheduleRuns.Add(new ReportScheduleRun
        {
            ReportScheduleId = schedule.ReportScheduleId,
            RunUtc = now,
            PeriodFrom = from,
            PeriodTo = to,
            RowCount = rowCount,
            Recipients = schedule.Recipients,
            Outcome = error is null ? outcome : ReportRunOutcome.Failed,
            Attempt = attempt,
            Error = error
        });

        if (error is null)
        {
            schedule.LastRunUtc = now;
            schedule.LastRunStatus = outcome;
            schedule.LastError = null;
            schedule.Attempt = 0;
            schedule.RetryAtUtc = null;
            schedule.FailingSinceUtc = null;
            schedule.NextRunUtc = Next(schedule, now);
            await db.SaveChangesAsync(ct);
            return;
        }

        schedule.FailingSinceUtc ??= now;
        if (attempt < MaxAttempts)
        {
            // Retried twice, ten minutes apart, before anyone is told (spec §12.2).
            schedule.Attempt = attempt;
            schedule.RetryAtUtc = now + RetryGap;
            schedule.LastError = Clip(error);
            await db.SaveChangesAsync(ct);
            return;
        }

        schedule.Attempt = 0;
        schedule.RetryAtUtc = null;
        schedule.LastRunUtc = now;
        schedule.LastRunStatus = ReportRunOutcome.Failed;
        schedule.LastError = Clip(error);
        schedule.NextRunUtc = Next(schedule, now);
        var disabled = now - schedule.FailingSinceUtc.Value >= TimeSpan.FromDays(7);
        if (disabled)
        {
            schedule.IsActive = false;
            schedule.DisabledReason = Clip($"Stopped automatically after failing for a week. Last error: {error}", 300);
        }
        await db.SaveChangesAsync(ct);
        await TellCreatorAsync(schedule, error, disabled, ct);
    }

    private static DateTime Next(ReportSchedule s, DateTime now) =>
        ScheduleClock.NextRunUtc(s.Frequency, s.DayOfWeek, s.DayOfMonth, s.TimeIst, s.NextRunUtc > now ? s.NextRunUtc : now);

    private async Task<(int Rows, ReportRunOutcome Outcome, string? Error)> BuildAndSendAsync(ReportSchedule schedule,
        DateOnly from, DateOnly to, List<string> recipients, DateTime now, CancellationToken ct)
    {
        var handler = ReportRegistry.Find(schedule.ReportKey)
                      ?? throw new BusinessRuleException("This report no longer exists.");
        var creator = await db.Users.AsNoTracking()
            .Where(u => u.UserId == schedule.CreatedByUserId && u.Status == UserStatus.Active)
            .Select(u => new { u.FullName, u.Email }).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("The admin who created this schedule is no longer active.");
        var permissions = (await db.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == schedule.CreatedByUserId)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.PermissionCode))
                .ToListAsync(ct))
            .ToHashSet();
        // The schedule runs with its creator's rights, checked again at every run.
        if (!ReportRegistry.CanSee(permissions, handler) || !permissions.Contains(PermissionCodes.Report.Export))
        {
            throw new BusinessRuleException($"{creator.FullName} no longer has permission to export this report.");
        }

        var values = ReportJson.Deserialize(schedule.FiltersJson);
        values["from"] = [from.ToString("yyyy-MM-dd")];
        values["to"] = [to.ToString("yyyy-MM-dd")];
        var lookups = await ReportLookups.LoadAsync(db, ct);
        var today = IstClock.IstDateOf(now);
        var (f, t, resolved) = ReportFilterParser.Resolve(handler, lookups, values, today);
        var ctx = new ReportRunContext { Db = db, From = f, To = t, Today = today, Values = resolved, Lookups = lookups };

        var (count, _) = await handler.TotalsAsync(ctx, ct);
        if (count == 0 && schedule.OnlyIfData)
        {
            return (0, ReportRunOutcome.SkippedEmpty, null);
        }
        var chips = await ReportChips.BuildAsync(db, handler, lookups, resolved, f, t, today, ct);
        var built = await files.BuildAsync(handler, ctx, schedule.Format, handler.DefaultSort, creator.FullName,
            ReportChips.Sentence(f, t, chips), null, now, ct);
        if (built.Content.Length > ReportMailer.MaxAttachmentBytes)
        {
            return (built.RowCount, ReportRunOutcome.Failed,
                $"The file is too large to email ({built.Content.Length / 1024 / 1024} MB). Narrow the filters or choose PDF.");
        }
        var (_, totals) = await handler.TotalsAsync(ctx, ct);
        var sentence = handler.SentenceFor(ctx, count, totals);
        var how = ScheduleClock.HowOften(schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.TimeIst);
        var html =
            $"<p>{WebUtility.HtmlEncode(sentence)}</p>" +
            $"<p>The {WebUtility.HtmlEncode(handler.Name)} for {WebUtility.HtmlEncode(ReportMath.Range(f, t))} is attached.</p>" +
            $"<p style=\"color:#6B655C;font-size:12px\">Sent by Regalis WorkSpace ({WebUtility.HtmlEncode(how)} IST) because " +
            $"{WebUtility.HtmlEncode(creator.FullName)} scheduled it. To change or stop it, open Reports &amp; analysis → Report library.</p>";
        var failure = await mailer.SendAsync(recipients, $"{handler.Name} — {ReportMath.Range(f, t)}", html,
            [(built.FileName, built.ContentType, built.Content)], ct);
        if (failure is not null)
        {
            return (built.RowCount, ReportRunOutcome.Failed, failure);
        }
        db.ReportExports.Add(new ReportExport
        {
            UserId = schedule.CreatedByUserId, ReportKey = handler.Key, FiltersJson = ReportJson.Serialize(resolved),
            RowCount = built.RowCount, Format = schedule.Format, Delivery = ReportExportDelivery.Scheduled, CreatedUtc = now
        });
        return (built.RowCount, ReportRunOutcome.Sent, null);
    }

    /// <summary>A silent failed schedule is worse than none: the creator hears about it.</summary>
    private async Task TellCreatorAsync(ReportSchedule schedule, string error, bool disabled, CancellationToken ct)
    {
        var creator = await db.Users.AsNoTracking().Where(u => u.UserId == schedule.CreatedByUserId)
            .Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(creator))
        {
            return;
        }
        var name = ReportRegistry.Find(schedule.ReportKey)?.Name ?? schedule.ReportKey;
        var body = disabled
            ? $"<p>The scheduled {WebUtility.HtmlEncode(name)} has failed for a week, so it has been stopped.</p>"
            : $"<p>The scheduled {WebUtility.HtmlEncode(name)} could not be sent after three attempts.</p>";
        body += $"<p>Reason: {WebUtility.HtmlEncode(error)}</p><p>Open Reports &amp; analysis → Report library to fix or turn it back on.</p>";
        var failure = await mailer.SendAsync([creator], disabled ? $"Stopped: {name}" : $"Last run failed: {name}", body, [], ct);
        if (failure is not null)
        {
            logger.LogWarning("Could not tell {Creator} that schedule {Id} failed: {Reason}", creator,
                schedule.ReportScheduleId, failure);
        }
    }

    public async Task ProcessExportJobsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var queued = await db.ReportExportJobs.Where(j => j.Status == ReportExportJobStatus.Queued)
            .OrderBy(j => j.CreatedUtc).Take(3).ToListAsync(ct);
        foreach (var job in queued)
        {
            await BuildExportAsync(job, now, ct);
        }

        // Links expire after seven days; the file goes with them.
        var expired = await db.ReportExportJobs
            .Where(j => j.Status == ReportExportJobStatus.Ready && j.ExpiresUtc < now).ToListAsync(ct);
        foreach (var job in expired)
        {
            if (job.FilePath is not null && File.Exists(job.FilePath))
            {
                File.Delete(job.FilePath);
            }
            job.Status = ReportExportJobStatus.Expired;
        }
        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task BuildExportAsync(ReportExportJob job, DateTime now, CancellationToken ct)
    {
        try
        {
            var handler = ReportRegistry.Find(job.ReportKey) ?? throw new BusinessRuleException("This report no longer exists.");
            var owner = await db.Users.AsNoTracking().Where(u => u.UserId == job.UserId)
                .Select(u => new { u.FullName, u.Email }).FirstAsync(ct);
            var lookups = await ReportLookups.LoadAsync(db, ct);
            var today = IstClock.IstDateOf(now);
            var (from, to, values) = ReportFilterParser.Resolve(handler, lookups, ReportJson.Deserialize(job.FiltersJson), today);
            var ctx = new ReportRunContext { Db = db, From = from, To = to, Today = today, Values = values, Lookups = lookups };
            var chips = await ReportChips.BuildAsync(db, handler, lookups, values, from, to, today, ct);
            var built = await files.BuildAsync(handler, ctx, job.Format, handler.DefaultSort, owner.FullName,
                ReportChips.Sentence(from, to, chips), null, now, ct);

            var directory = Path.Combine(storage.Value.UploadRoot, "report-exports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, job.Token + Path.GetExtension(built.FileName));
            await File.WriteAllBytesAsync(path, built.Content, ct);
            job.FilePath = path;
            job.FileName = built.FileName;
            job.RowCount = built.RowCount;
            job.Status = ReportExportJobStatus.Ready;
            job.CompletedUtc = clock.UtcNow;
            job.ExpiresUtc = clock.UtcNow.AddDays(7);
            await db.SaveChangesAsync(ct);

            var baseUrl = (job.PortalBaseUrl ?? await settings.GetStringAsync(AppSettingKeys.Quotation.PortalBaseUrl, "", ct))
                .TrimEnd('/');
            var link = $"{baseUrl}/admin/reports/download/{job.Token}";
            var failure = await mailer.SendAsync([owner.Email], $"Your export is ready: {handler.Name}",
                $"<p>Your {WebUtility.HtmlEncode(handler.Name)} ({built.RowCount:N0} rows) is ready.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Download it</a> — the link works for seven days, once you are signed in.</p>",
                [], ct);
            if (failure is not null)
            {
                job.Error = Clip($"Built, but the email was not sent: {failure}");
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Background export {JobId} ({Report}) failed", job.ReportExportJobId, job.ReportKey);
            job.Status = ReportExportJobStatus.Failed;
            job.Error = Clip(ex is AppException app ? app.Message : "The export could not be built.");
            job.CompletedUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string Clip(string text, int max = 500) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
