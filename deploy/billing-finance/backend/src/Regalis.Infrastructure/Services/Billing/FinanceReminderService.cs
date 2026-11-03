using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// What gets chased automatically (build doc §13.2). Off until Reminders.Enabled is set:
/// email from production has to be proven first. Every reminder is logged with whether it
/// was delivered; chasing stops when an invoice is paid, cancelled or the customer is
/// marked "do not chase"; never more than three per invoice; each kind has its own switch.
/// </summary>
public class FinanceReminderService(
    RegalisDbContext db,
    ISettingService settings,
    IQuotationDispatcher dispatcher,
    IComplianceService compliance,
    IDateTimeProvider clock,
    ILogger<FinanceReminderService> logger) : IFinanceReminderService
{
    private sealed class Run
    {
        public int Sent;
        public int Failed;
        public readonly List<string> Notes = new();
    }

    private static int[] Days(string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s.Trim(), out int d) ? d : -1).Where(d => d >= 0).Distinct().ToArray();

    public async Task<ReminderRunResultDto> RunAsync(bool force, CancellationToken ct = default)
    {
        var run = new Run();
        if (!await settings.GetBoolAsync("Reminders.Enabled", false, ct))
        {
            return new ReminderRunResultDto(false, 0, 0, new[] { "Reminders are switched off (Settings > Reminders.Enabled)." });
        }
        DateOnly today = FinanceRules.Today(clock);
        string company = await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct);
        string financeMail = await settings.GetStringAsync("Reminders.FinanceEmail", await settings.GetStringAsync("Company.Email", "", ct), ct);

        if (await settings.GetBoolAsync("Reminders.InvoiceDueSoon.Enabled", true, ct))
            await InvoicesAsync(run, today, company, dueSoon: true, ct);
        if (await settings.GetBoolAsync("Reminders.InvoiceOverdue.Enabled", true, ct))
            await InvoicesAsync(run, today, company, dueSoon: false, ct);
        if (await settings.GetBoolAsync("Reminders.BillDue.Enabled", true, ct))
            await BillsAsync(run, today, financeMail, ct);
        if (await settings.GetBoolAsync("Reminders.ApprovalDigest.Enabled", true, ct))
            await DigestAsync(run, today, ct);
        if (await settings.GetBoolAsync("Reminders.Compliance.Enabled", true, ct))
            await ComplianceAsync(run, today, financeMail, ct);
        await db.SaveChangesAsync(ct);
        return new ReminderRunResultDto(true, run.Sent, run.Failed, run.Notes);
    }

    private async Task<bool> SendAsync(Run run, string type, string docType, long docId, string? docNumber, string to, string subject, string body, CancellationToken ct)
    {
        QuotationDeliveryResult result;
        try
        {
            result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(to, null, null, subject, body, Array.Empty<QuotationOutboundFile>()), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reminder {Type} to {To} failed", type, to);
            result = QuotationDeliveryResult.Fail(ex.Message);
        }
        bool delivered = result.Succeeded && result.Note == null;
        string? error = result.FailureReason ?? result.Note;
        db.Set<ReminderLog>().Add(new ReminderLog
        {
            ReminderType = type, DocumentType = docType, DocumentId = docId, DocumentNumber = docNumber, Recipient = to, Channel = "Email",
            SentUtc = clock.UtcNow, Delivered = delivered, Error = delivered ? null : error?.Length > 500 ? error[..500] : error
        });
        if (delivered) run.Sent++; else run.Failed++;
        return delivered;
    }

    private async Task InvoicesAsync(Run run, DateOnly today, string company, bool dueSoon, CancellationToken ct)
    {
        int before = await settings.GetIntAsync("Reminders.InvoiceDaysBefore", 3, ct);
        int[] overdueDays = Days(await settings.GetStringAsync("Reminders.InvoiceOverdueDays", "1,7,15", ct));
        var targets = dueSoon ? new[] { today.AddDays(before) } : overdueDays.Select(d => today.AddDays(-d)).ToArray();
        var invoices = await (from i in db.Invoices.AsNoTracking()
                              where (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid)
                                    && i.TotalAmount - i.PaidAmount > 0m && targets.Contains(i.DueDate)
                                    && !db.Set<CustomerTaxProfile>().Any(p => p.CustomerId == i.CustomerId && p.DoNotChase)
                              select new { i.InvoiceId, i.InvoiceNumber, i.CustomerName, i.CustomerEmail, i.DueDate, Outstanding = i.TotalAmount - i.PaidAmount })
            .ToListAsync(ct);
        foreach (var i in invoices)
        {
            int late = today.DayNumber - i.DueDate.DayNumber;
            string type = dueSoon ? "InvoiceDueSoon" : $"InvoiceOverdue{late}";
            var sent = await db.Set<ReminderLog>().Where(r => r.DocumentType == "Invoice" && r.DocumentId == i.InvoiceId && r.ReminderType.StartsWith("Invoice"))
                .Select(r => r.ReminderType).ToListAsync(ct);
            if (sent.Contains(type) || sent.Count >= 3 || string.IsNullOrWhiteSpace(i.CustomerEmail)) continue;
            string money = FinanceRules.Money(i.Outstanding);
            string subject = dueSoon ? $"Invoice {i.InvoiceNumber} is due on {i.DueDate:dd MMM yyyy}" : $"Invoice {i.InvoiceNumber} is overdue";
            string body = $"<p>Dear {WebUtility.HtmlEncode(i.CustomerName)},</p><p>" + (dueSoon
                ? $"This is a reminder that invoice {i.InvoiceNumber} for {money} is due on {i.DueDate:dd MMM yyyy}."
                : $"Invoice {i.InvoiceNumber} was due on {i.DueDate:dd MMM yyyy} and {money} is still outstanding, {late} day{(late == 1 ? "" : "s")} late.")
                + "</p><p>You can pay at the front desk (cash, UPI or card) or by bank transfer. If you have already paid, please ignore this email and accept our thanks.</p>"
                + $"<p>{WebUtility.HtmlEncode(company)}</p>";
            await SendAsync(run, type, "Invoice", i.InvoiceId, i.InvoiceNumber, i.CustomerEmail, subject, body, ct);
        }
    }

    private async Task BillsAsync(Run run, DateOnly today, string financeMail, CancellationToken ct)
    {
        DateOnly soon = today.AddDays(3);
        var bills = await (from b in db.Set<PurchaseBill>().AsNoTracking()
                           where (b.Status == PurchaseBillStatus.Approved || b.Status == PurchaseBillStatus.PartPaid) && (b.DueDate == today || b.DueDate == soon)
                           join s in db.Suppliers on b.SupplierId equals s.SupplierId
                           select new { b.PurchaseBillId, b.SupplierBillNo, s.SupplierName, b.DueDate, b.BillTotal,
                               Paid = PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m })
            .ToListAsync(ct);
        foreach (var b in bills.Where(b => b.BillTotal - b.Paid > 0m))
        {
            string type = b.DueDate == today ? "BillDueToday" : "BillDueSoon";
            if (await db.Set<ReminderLog>().AnyAsync(r => r.DocumentType == "PurchaseBill" && r.DocumentId == b.PurchaseBillId && r.ReminderType == type, ct)) continue;
            string text = $"{b.SupplierName} bill {b.SupplierBillNo}: {FinanceRules.Money(b.BillTotal - b.Paid)} due {(b.DueDate == today ? "today" : $"on {b.DueDate:dd MMM}")}.";
            AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Purchase, "BILL_DUE", "Supplier bill due", text, AdminNotificationSeverity.ActionNeeded,
                ReferenceType: "PurchaseBill", ReferenceId: b.PurchaseBillId, ReferenceLabel: b.SupplierBillNo, ActionLabel: "Open bill",
                ActionRoute: $"/admin/billing/purchase-bills/{b.PurchaseBillId}"), clock.UtcNow);
            if (!string.IsNullOrWhiteSpace(financeMail))
                await SendAsync(run, type, "PurchaseBill", b.PurchaseBillId, b.SupplierBillNo, financeMail, $"Supplier bill due: {b.SupplierBillNo}", $"<p>{WebUtility.HtmlEncode(text)}</p>", ct);
            else
                db.Set<ReminderLog>().Add(new ReminderLog { ReminderType = type, DocumentType = "PurchaseBill", DocumentId = b.PurchaseBillId, DocumentNumber = b.SupplierBillNo,
                    Recipient = "Admin notifications", Channel = "InApp", SentUtc = clock.UtcNow, Delivered = true });
        }
    }

    private async Task<List<(string Email, string Name)>> HoldersAsync(string[] permissions, CancellationToken ct)
    {
        var rows = await (from ur in db.UserRoles
                          join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                          join p in db.Permissions on rp.PermissionId equals p.PermissionId
                          join u in db.Users on ur.UserId equals u.UserId
                          where permissions.Contains(p.PermissionCode) && u.IsActive && u.Email != ""
                          select new { u.Email, u.FullName }).Distinct().ToListAsync(ct);
        return rows.Select(r => (r.Email, r.FullName)).ToList();
    }

    private async Task DigestAsync(Run run, DateOnly today, CancellationToken ct)
    {
        if (IstClock.ToIst(clock.UtcNow).Hour < 9) return;
        long key = long.Parse(today.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (await db.Set<ReminderLog>().AnyAsync(r => r.ReminderType == "ApprovalDigest" && r.DocumentId == key, ct)) return;
        int bills = await db.Set<PurchaseBill>().CountAsync(b => b.Status == PurchaseBillStatus.AwaitingApproval, ct);
        int expenses = await db.Set<ExpenseControl>().CountAsync(c => c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval && !c.IsRecurringDraft, ct);
        int drafts = await db.Set<ExpenseControl>().CountAsync(c => c.IsRecurringDraft, ct);
        if (bills + expenses + drafts == 0) return;
        string text = $"Waiting for you: {bills} supplier bill{(bills == 1 ? "" : "s")}, {expenses} expense{(expenses == 1 ? "" : "s")} to approve"
                      + (drafts > 0 ? $", and {drafts} recurring expense draft{(drafts == 1 ? "" : "s")} to confirm." : ".");
        AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Purchase, "APPROVAL_DIGEST", "Waiting for approval", text, AdminNotificationSeverity.Info,
            ActionLabel: "Open purchase bills", ActionRoute: "/admin/billing/purchase-bills"), clock.UtcNow);
        foreach (var (email, name) in await HoldersAsync(new[] { "BILL.APPROVE", "EXPENSE.APPROVE" }, ct))
            await SendAsync(run, "ApprovalDigest", "Digest", key, today.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), email,
                "Waiting for your approval", $"<p>Dear {WebUtility.HtmlEncode(name)},</p><p>{WebUtility.HtmlEncode(text)}</p>", ct);
    }

    private async Task ComplianceAsync(Run run, DateOnly today, string financeMail, CancellationToken ct)
    {
        int[] before = Days(await settings.GetStringAsync("Reminders.ComplianceDaysBefore", "7,2,0", ct));
        string accountant = await settings.GetStringAsync("Reminders.AccountantEmail", "", ct);
        var recipients = new[] { financeMail, accountant }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (recipients.Count == 0) { run.Notes.Add("No finance or accountant email is set, so compliance reminders were not sent."); return; }
        var items = await compliance.GetCalendarAsync(FinanceRules.Period(today), 2, ct);
        foreach (var item in items.Where(i => i.ObligationId != null && i.Status != 4 && before.Contains(i.DaysToGo)))
        {
            string type = $"Compliance{item.DaysToGo}";
            string number = $"{item.ObligationId}:{item.Period}";
            if (await db.Set<ReminderLog>().AnyAsync(r => r.DocumentType == "Compliance" && r.DocumentNumber == number && r.ReminderType == type, ct)) continue;
            string text = $"{item.Name} for {item.PeriodLabel} is due {(item.DaysToGo == 0 ? "today" : $"in {item.DaysToGo} days, on {item.DueDate:dd MMM yyyy}")}"
                          + (item.Amount is > 0m ? $" - {FinanceRules.Money(item.Amount.Value)}." : ".");
            foreach (string to in recipients)
                await SendAsync(run, type, "Compliance", item.ObligationId!.Value, number, to, $"Due {(item.DaysToGo == 0 ? "today" : $"in {item.DaysToGo} days")}: {item.Name}", $"<p>{WebUtility.HtmlEncode(text)}</p>", ct);
        }
    }
}

/// <summary>Runs the daily finance jobs: recurring-expense drafts, and reminders once
/// a day after 09:00 IST.</summary>
public class BillingFinanceJobs(IServiceScopeFactory scopes, ILogger<BillingFinanceJobs> logger) : BackgroundService
{
    private DateOnly _lastRun;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTime ist = IstClock.ToIst(DateTime.UtcNow);
                DateOnly today = DateOnly.FromDateTime(ist);
                if (ist.Hour >= 9 && _lastRun != today)
                {
                    using IServiceScope scope = scopes.CreateScope();
                    int drafts = await scope.ServiceProvider.GetRequiredService<IExpenseControlService>().RaiseRecurringDraftsAsync(stoppingToken);
                    ReminderRunResultDto result = await scope.ServiceProvider.GetRequiredService<IFinanceReminderService>().RunAsync(false, stoppingToken);
                    logger.LogInformation("Billing jobs: {Drafts} recurring drafts; reminders enabled={Enabled} sent={Sent} failed={Failed}", drafts, result.Enabled, result.Sent, result.Failed);
                    _lastRun = today;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Billing finance jobs failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}
