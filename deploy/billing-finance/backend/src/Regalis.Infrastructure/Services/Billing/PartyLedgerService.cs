using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Ledger, outstanding and ageing for customers and suppliers (build doc §7), built once
/// with the party type as a parameter. Nothing is stored: every figure is assembled from
/// invoices, receipts, bills, vendor payments, TDS and advances when asked. Ageing counts
/// from each document's own due date, never its document date.
/// </summary>
public class PartyLedgerService(
    RegalisDbContext db,
    ISettingService settings,
    IQuotationDispatcher dispatcher,
    IDateTimeProvider clock) : IPartyLedgerService
{
    private sealed record Entry(DateOnly Date, string Kind, string Reference, string? Detail, decimal Debit, decimal Credit, string? DocType, long? DocId, long Order);

    private sealed record OpenDoc(string DocType, long DocId, string Number, long PartyId, DateOnly DocumentDate, DateOnly DueDate, decimal Total, decimal Balance);

    private static bool IsSupplier(string partyType) => partyType.Equals("supplier", StringComparison.OrdinalIgnoreCase)
        ? true
        : partyType.Equals("customer", StringComparison.OrdinalIgnoreCase) ? false
        : throw new BusinessRuleException("Party type must be customer or supplier.");

    private static readonly PurchaseBillStatus[] LedgerBills = { PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid, PurchaseBillStatus.Paid, PurchaseBillStatus.OnHold };

    public async Task<IReadOnlyList<PartyOptionDto>> SearchPartiesAsync(string partyType, string? search, CancellationToken ct = default)
    {
        string s = search?.Trim() ?? "";
        if (IsSupplier(partyType))
        {
            return await db.Suppliers.AsNoTracking().Where(x => s == "" || x.SupplierName.Contains(s) || x.SupplierCode.Contains(s))
                .OrderBy(x => x.SupplierName).Take(50).Select(x => new PartyOptionDto(x.SupplierId, x.SupplierName, x.SupplierCode)).ToListAsync(ct);
        }
        return await db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer && (s == "" || u.FullName.Contains(s) || u.Email.Contains(s) || u.MobileNumber.Contains(s)))
            .OrderBy(u => u.FullName).Take(50).Select(u => new PartyOptionDto(u.UserId, u.FullName, u.Email)).ToListAsync(ct);
    }

    private async Task<List<Entry>> EntriesAsync(bool supplier, long partyId, CancellationToken ct)
    {
        var list = new List<Entry>();
        if (supplier)
        {
            int sid = (int)partyId;
            var bills = await db.Set<PurchaseBill>().AsNoTracking().Where(b => b.SupplierId == sid && LedgerBills.Contains(b.Status))
                .Select(b => new { b.PurchaseBillId, b.BillDate, b.SupplierBillNo, b.BillNumber, b.BillTotal }).ToListAsync(ct);
            list.AddRange(bills.Select(b => new Entry(b.BillDate, "Bill", b.SupplierBillNo, b.BillNumber, 0m, b.BillTotal, "bill", b.PurchaseBillId, b.PurchaseBillId)));
            var payments = await db.Set<VendorPayment>().AsNoTracking().Where(v => v.SupplierId == sid)
                .Select(v => new { v.VendorPaymentId, v.PaidOn, v.PaymentNumber, v.Amount, v.TdsAmount, v.AdvanceAmount, v.Reference, v.Mode, v.IsReversed, v.ReversedUtc, v.ReverseReason })
                .ToListAsync(ct);
            foreach (var v in payments)
            {
                string mode = VendorPaymentService.ModeName(v.Mode);
                if (v.Amount > 0m)
                {
                    bool advanceOnly = v.AdvanceAmount == v.Amount;
                    list.Add(new Entry(v.PaidOn, advanceOnly ? "Advance" : "Payment", v.PaymentNumber, $"{mode}{(v.Reference != null ? " · " + v.Reference : "")}",
                        v.Amount, 0m, "vendor-payment", v.VendorPaymentId, v.VendorPaymentId));
                }
                if (v.TdsAmount > 0m)
                {
                    list.Add(new Entry(v.PaidOn, "TDS deduction", v.PaymentNumber, "Held back and owed to the government", v.TdsAmount, 0m, "vendor-payment", v.VendorPaymentId, v.VendorPaymentId));
                }
                if (v.IsReversed && v.ReversedUtc != null)
                {
                    DateOnly on = Shared.IstClock.IstDateOf(v.ReversedUtc.Value);
                    list.Add(new Entry(on, "Reversal", v.PaymentNumber, v.ReverseReason, 0m, v.Amount + v.TdsAmount, "vendor-payment", v.VendorPaymentId, v.VendorPaymentId));
                }
            }
        }
        else
        {
            var invoices = await db.Invoices.AsNoTracking().Where(i => i.CustomerId == partyId && i.Status != InvoiceStatus.Cancelled)
                .Select(i => new { i.InvoiceId, i.InvoiceDate, i.InvoiceNumber, i.TotalAmount }).ToListAsync(ct);
            list.AddRange(invoices.Select(i => new Entry(i.InvoiceDate, "Invoice", i.InvoiceNumber, null, i.TotalAmount, 0m, "invoice", i.InvoiceId, i.InvoiceId)));
            var receipts = await (from p in db.Set<InvoicePayment>().AsNoTracking()
                                  join i in db.Invoices on p.InvoiceId equals i.InvoiceId
                                  where p.CustomerId == partyId
                                  select new { p.InvoicePaymentId, p.PaidOn, p.ReceiptNumber, p.Amount, p.Mode, p.Reference, i.InvoiceNumber, p.ReversalOfId }).ToListAsync(ct);
            foreach (var r in receipts)
            {
                string kind = r.ReversalOfId != null ? "Reversal" : r.Mode == InvoicePaymentMode.TdsCredit ? "TDS by customer" : r.Mode == InvoicePaymentMode.WriteOff ? "Short payment accepted" : "Payment";
                string detail = $"{CustomerPaymentService.ModeName(r.Mode)} against {r.InvoiceNumber}" + (r.Reference != null ? " · " + r.Reference : "");
                list.Add(r.Amount >= 0m
                    ? new Entry(r.PaidOn, kind, r.ReceiptNumber, detail, 0m, r.Amount, "receipt", r.InvoicePaymentId, r.InvoicePaymentId)
                    : new Entry(r.PaidOn, kind, r.ReceiptNumber, detail, -r.Amount, 0m, "receipt", r.InvoicePaymentId, r.InvoicePaymentId));
            }
            var others = await (from p in db.Payments.AsNoTracking()
                                join i in db.Invoices on p.InvoiceId equals (long?)i.InvoiceId
                                where p.CustomerId == partyId && p.InvoiceId != null
                                      && (p.Status == PaymentStatus.Success || p.Status == PaymentStatus.Refunded)
                                      && !db.Set<InvoicePayment>().Any(x => x.PaymentId == p.PaymentId)
                                select new { p.PaymentId, p.PaidUtc, p.CreatedDate, p.PaymentNumber, p.Amount, p.Status, p.RefundedAmount, i.InvoiceNumber, p.PaymentMethod }).ToListAsync(ct);
            foreach (var p in others)
            {
                DateOnly on = Shared.IstClock.IstDateOf(p.PaidUtc ?? p.CreatedDate);
                list.Add(new Entry(on, "Payment", p.PaymentNumber, $"{p.PaymentMethod} against {p.InvoiceNumber}", 0m, p.Amount, "payment", p.PaymentId, p.PaymentId));
                if (p.Status == PaymentStatus.Refunded)
                {
                    list.Add(new Entry(on, "Refund", p.PaymentNumber, null, p.RefundedAmount ?? p.Amount, 0m, "payment", p.PaymentId, p.PaymentId));
                }
            }
        }
        return list;
    }

    public async Task<PartyLedgerDto> GetLedgerAsync(string partyType, long partyId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        bool supplier = IsSupplier(partyType);
        DateOnly today = FinanceRules.Today(clock);
        DateOnly f = from ?? Shared.IndianMoney.FinancialYearStart(today);
        DateOnly t = to ?? today;
        if (t < f) throw new BusinessRuleException("The end date cannot be before the start date.");

        string name, terms;
        string? gstin, taxId, email;
        if (supplier)
        {
            Supplier s = await db.Suppliers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == partyId, ct) ?? throw new NotFoundException("Supplier", partyId);
            SupplierTaxProfile? p = await db.Set<SupplierTaxProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == s.SupplierId, ct);
            name = s.SupplierName; gstin = s.Gstin; taxId = p?.Pan; email = s.Email;
            terms = p?.PaymentTermsDays is int d ? (d == 0 ? "Due on receipt" : $"Net {d}") : s.PaymentTerms ?? "";
        }
        else
        {
            var u = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.UserId == partyId)
                .Select(x => new { x.FullName, x.Email }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Customer", partyId);
            string? custGstin = await db.Invoices.AsNoTracking().Where(i => i.CustomerId == partyId && i.CustomerGstin != null)
                .OrderByDescending(i => i.InvoiceId).Select(i => i.CustomerGstin).FirstOrDefaultAsync(ct);
            name = u.FullName; gstin = custGstin; email = u.Email;
            taxId = await db.Set<CustomerTaxProfile>().Where(x => x.CustomerId == partyId).Select(x => x.Tan).FirstOrDefaultAsync(ct);
            terms = "";
        }

        List<Entry> all = await EntriesAsync(supplier, partyId, ct);
        // Customer: debit raises the balance; supplier: credit does.
        decimal Sign(Entry e) => supplier ? e.Credit - e.Debit : e.Debit - e.Credit;
        decimal opening = all.Where(e => e.Date < f).Sum(Sign);
        decimal running = opening;
        var rows = new List<LedgerRowDto>();
        foreach (Entry e in all.Where(e => e.Date >= f && e.Date <= t).OrderBy(e => e.Date).ThenBy(e => e.Kind == "Invoice" || e.Kind == "Bill" ? 0 : 1).ThenBy(e => e.Order))
        {
            running += Sign(e);
            rows.Add(new LedgerRowDto(e.Date, e.Kind, e.Reference, e.Detail, e.Debit, e.Credit, running, e.DocType, e.DocId));
        }
        return new PartyLedgerDto(supplier ? "supplier" : "customer", partyId, name, gstin, taxId, terms, email, f, t, opening,
            rows.Sum(r => r.Debit), rows.Sum(r => r.Credit), running, rows);
    }

    private async Task<List<OpenDoc>> OpenDocumentsAsync(bool supplier, CancellationToken ct)
    {
        if (supplier)
        {
            var bills = await db.Set<PurchaseBill>().AsNoTracking()
                .Where(b => LedgerBills.Contains(b.Status) || b.Status == PurchaseBillStatus.AwaitingApproval)
                .Select(b => new
                {
                    b.PurchaseBillId, b.SupplierBillNo, b.SupplierId, b.BillDate, b.DueDate, b.BillTotal,
                    Paid = PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m
                }).ToListAsync(ct);
            return bills.Where(b => b.BillTotal - b.Paid > 0m)
                .Select(b => new OpenDoc("bill", b.PurchaseBillId, b.SupplierBillNo, b.SupplierId, b.BillDate, b.DueDate, b.BillTotal, b.BillTotal - b.Paid)).ToList();
        }
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded && i.TotalAmount - i.PaidAmount > 0m)
            .Select(i => new { i.InvoiceId, i.InvoiceNumber, i.CustomerId, i.InvoiceDate, i.DueDate, i.TotalAmount, i.PaidAmount }).ToListAsync(ct);
        return invoices.Select(i => new OpenDoc("invoice", i.InvoiceId, i.InvoiceNumber, i.CustomerId, i.InvoiceDate, i.DueDate, i.TotalAmount, i.TotalAmount - i.PaidAmount)).ToList();
    }

    private async Task<Dictionary<long, string>> NamesAsync(bool supplier, IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (supplier)
        {
            var intIds = list.Select(x => (int)x).ToList();
            return await db.Suppliers.IgnoreQueryFilters().Where(s => intIds.Contains(s.SupplierId)).ToDictionaryAsync(s => (long)s.SupplierId, s => s.SupplierName, ct);
        }
        return await db.Users.IgnoreQueryFilters().Where(u => list.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
    }

    public async Task<IReadOnlyList<OutstandingPartyDto>> GetOutstandingAsync(string partyType, CancellationToken ct = default)
    {
        bool supplier = IsSupplier(partyType);
        DateOnly today = FinanceRules.Today(clock);
        List<OpenDoc> docs = await OpenDocumentsAsync(supplier, ct);
        var advances = new Dictionary<long, decimal>();
        if (supplier)
        {
            var adv = await db.Set<VendorPayment>().Where(v => !v.IsReversed).GroupBy(v => v.SupplierId)
                .Select(g => new { g.Key, Net = g.Sum(v => v.AdvanceAmount) - g.Sum(v => v.AdvanceAdjusted) }).ToListAsync(ct);
            foreach (var a in adv.Where(a => a.Net != 0m)) advances[a.Key] = a.Net;
        }
        var partyIds = docs.Select(d => d.PartyId).Concat(advances.Keys).Distinct().ToList();
        var names = await NamesAsync(supplier, partyIds, ct);
        return partyIds.Select(id =>
        {
            var mine = docs.Where(d => d.PartyId == id).ToList();
            decimal advance = advances.GetValueOrDefault(id);
            DateOnly? oldest = mine.Count == 0 ? null : mine.Min(d => d.DocumentDate);
            return new OutstandingPartyDto(id, names.GetValueOrDefault(id) ?? $"#{id}", mine.Sum(d => d.Total), mine.Count, oldest,
                oldest == null ? 0 : today.DayNumber - oldest.Value.DayNumber, advance, mine.Sum(d => d.Balance) - advance);
        }).Where(p => p.Balance != 0m).OrderByDescending(p => p.Balance).ToList();
    }

    private static string Bucket(OpenDoc d, DateOnly today)
    {
        int days = today.DayNumber - d.DueDate.DayNumber;
        return days <= 0 ? "not-due" : days <= 30 ? "1-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+";
    }

    public async Task<AgeingDto> GetAgeingAsync(string partyType, CancellationToken ct = default)
    {
        bool supplier = IsSupplier(partyType);
        DateOnly today = FinanceRules.Today(clock);
        List<OpenDoc> docs = await OpenDocumentsAsync(supplier, ct);
        var names = await NamesAsync(supplier, docs.Select(d => d.PartyId), ct);
        AgeingRowDto Row(long id, string name, IEnumerable<OpenDoc> ds)
        {
            var list = ds.ToList();
            decimal Sum(string b) => list.Where(d => Bucket(d, today) == b).Sum(d => d.Balance);
            return new AgeingRowDto(id, name, Sum("not-due"), Sum("1-30"), Sum("31-60"), Sum("61-90"), Sum("90+"), list.Sum(d => d.Balance));
        }
        var rows = docs.GroupBy(d => d.PartyId).Select(g => Row(g.Key, names.GetValueOrDefault(g.Key) ?? $"#{g.Key}", g))
            .OrderByDescending(r => r.Total).ToList();
        return new AgeingDto(supplier ? "supplier" : "customer", today, rows, Row(0, "Total", docs));
    }

    public async Task<IReadOnlyList<AgeingDocumentDto>> GetAgeingDocumentsAsync(string partyType, long? partyId, string? bucket, CancellationToken ct = default)
    {
        bool supplier = IsSupplier(partyType);
        DateOnly today = FinanceRules.Today(clock);
        var docs = (await OpenDocumentsAsync(supplier, ct))
            .Where(d => (partyId == null || d.PartyId == partyId) && (string.IsNullOrEmpty(bucket) || bucket == "total" || Bucket(d, today) == bucket)).ToList();
        var names = await NamesAsync(supplier, docs.Select(d => d.PartyId), ct);
        return docs.OrderBy(d => d.DueDate).Select(d => new AgeingDocumentDto(d.DocType, d.DocId, d.Number, d.PartyId, names.GetValueOrDefault(d.PartyId) ?? "",
            d.DocumentDate, d.DueDate, Math.Max(0, today.DayNumber - d.DueDate.DayNumber), d.Total, d.Balance)).ToList();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportLedgerAsync(string partyType, long partyId, DateOnly? from, DateOnly? to, string format, CancellationToken ct = default)
    {
        PartyLedgerDto ledger = await GetLedgerAsync(partyType, partyId, from, to, ct);
        string stem = $"Statement-{Safe(ledger.PartyName)}-{ledger.From:yyyyMMdd}-{ledger.To:yyyyMMdd}";
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (await StatementPdfAsync(ledger, ct), "application/pdf", stem + ".pdf");
        }
        using var wb = new XLWorkbook();
        IXLWorksheet ws = wb.AddWorksheet("Ledger");
        string[] head = { "Date", "What", "Reference", "Detail", "Debit", "Credit", "Balance" };
        for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        ws.Cell(2, 1).Value = ledger.From.ToDateTime(TimeOnly.MinValue);
        ws.Cell(2, 2).Value = "Opening balance";
        ws.Cell(2, 7).Value = ledger.OpeningBalance;
        int r = 3;
        foreach (LedgerRowDto row in ledger.Rows)
        {
            ws.Cell(r, 1).Value = row.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = row.Kind;
            ws.Cell(r, 3).Value = row.Reference;
            ws.Cell(r, 4).Value = row.Detail ?? "";
            ws.Cell(r, 5).Value = row.Debit;
            ws.Cell(r, 6).Value = row.Credit;
            ws.Cell(r, 7).Value = row.Balance;
            r++;
        }
        ws.Cell(r, 2).Value = "Closing balance";
        ws.Cell(r, 5).Value = ledger.TotalDebit;
        ws.Cell(r, 6).Value = ledger.TotalCredit;
        ws.Cell(r, 7).Value = ledger.ClosingBalance;
        ws.Row(r).Style.Font.Bold = true;
        ExcelStyle.Finish(ws, head.Length, r, new[] { 1 }, new[] { 5, 6, 7 }, totals: false);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", stem + ".xlsx");
    }

    public async Task<byte[]> ExportAgeingAsync(string partyType, CancellationToken ct = default)
    {
        AgeingDto ageing = await GetAgeingAsync(partyType, ct);
        using var wb = new XLWorkbook();
        IXLWorksheet ws = wb.AddWorksheet("Ageing");
        string[] head = { ageing.PartyType == "supplier" ? "Supplier" : "Customer", "Not due", "1-30 days", "31-60", "61-90", "90+", "Total" };
        for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        int r = 2;
        foreach (AgeingRowDto row in ageing.Rows)
        {
            ws.Cell(r, 1).Value = row.PartyName;
            ws.Cell(r, 2).Value = row.NotDue;
            ws.Cell(r, 3).Value = row.D1To30;
            ws.Cell(r, 4).Value = row.D31To60;
            ws.Cell(r, 5).Value = row.D61To90;
            ws.Cell(r, 6).Value = row.Over90;
            ws.Cell(r, 7).Value = row.Total;
            r++;
        }
        ExcelStyle.Finish(ws, head.Length, r - 1, Array.Empty<int>(), new[] { 2, 3, 4, 5, 6, 7 });
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<string> EmailStatementAsync(string partyType, long partyId, EmailStatementRequest request, CancellationToken ct = default)
    {
        PartyLedgerDto ledger = await GetLedgerAsync(partyType, partyId, request.From, request.To, ct);
        string? to = string.IsNullOrWhiteSpace(request.ToAddress) ? ledger.Email : request.ToAddress.Trim();
        if (string.IsNullOrWhiteSpace(to)) throw new BusinessRuleException($"{ledger.PartyName} has no email address on record. Enter one to send the statement to.");
        byte[] pdf = await StatementPdfAsync(ledger, ct);
        string company = await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct);
        string owes = ledger.PartyType == "supplier"
            ? $"By our books we owe you {FinanceRules.Money(ledger.ClosingBalance)} as on {ledger.To:dd MMM yyyy}."
            : $"By our books {FinanceRules.Money(ledger.ClosingBalance)} is due from you as on {ledger.To:dd MMM yyyy}.";
        QuotationDeliveryResult result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(to, null, null,
            $"Statement of account {ledger.From:dd MMM yyyy} - {ledger.To:dd MMM yyyy} - {company}",
            $"<p>Dear {WebUtility.HtmlEncode(ledger.PartyName)},</p><p>Please find attached your statement of account with {WebUtility.HtmlEncode(company)} " +
            $"from {ledger.From:dd MMM yyyy} to {ledger.To:dd MMM yyyy}. {owes}</p><p>Please confirm the balance, or tell us of any difference, by replying to this email.</p><p>{WebUtility.HtmlEncode(company)}</p>",
            new[] { new QuotationOutboundFile($"Statement-{Safe(ledger.PartyName)}.pdf", "application/pdf", pdf) }), ct);
        if (!result.Succeeded) throw new BusinessRuleException($"The statement could not be emailed: {result.FailureReason}");
        db.Set<ReminderLog>().Add(new ReminderLog
        {
            ReminderType = "Statement",
            DocumentType = ledger.PartyType,
            DocumentId = partyId,
            Recipient = to,
            Channel = "Email",
            SentUtc = clock.UtcNow,
            Delivered = result.Note == null,
            Error = result.Note
        });
        await db.SaveChangesAsync(ct);
        return result.Note ?? $"Statement emailed to {to}.";
    }

    private static string Safe(string s) => new string(s.Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());

    private async Task<byte[]> StatementPdfAsync(PartyLedgerDto l, CancellationToken ct)
    {
        Settings.License = LicenseType.Community;
        string company = await settings.GetStringAsync("Company.Name", "Regalis WorkSpace", ct);
        string address = await settings.GetStringAsync("Company.Address", "", ct);
        string gstin = await settings.GetStringAsync("Company.Gstin", "", ct);
        string side = l.PartyType == "supplier" ? "Amount Regalis owes you" : "Amount due from you";
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(s => s.FontSize(9).FontColor("#1F2328"));
            page.Header().Column(h =>
            {
                h.Item().Text(company).FontSize(16).Bold();
                if (!string.IsNullOrWhiteSpace(address)) h.Item().Text(address).FontColor("#6B7280");
                if (!string.IsNullOrWhiteSpace(gstin)) h.Item().Text($"GSTIN {gstin}").FontColor("#6B7280");
                h.Item().PaddingTop(10).Text("STATEMENT OF ACCOUNT").FontSize(12).Bold().FontColor("#2E75B6");
                h.Item().Text($"{l.PartyName}{(l.Gstin != null ? " · GSTIN " + l.Gstin : "")}{(l.TaxId != null ? " · " + (l.PartyType == "supplier" ? "PAN " : "TAN ") + l.TaxId : "")}");
                h.Item().Text($"{l.From:dd MMM yyyy} to {l.To:dd MMM yyyy}").FontColor("#6B7280");
            });
            page.Content().PaddingTop(12).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(62); c.ConstantColumn(80); c.RelativeColumn(2); c.RelativeColumn(3);
                    c.ConstantColumn(70); c.ConstantColumn(70); c.ConstantColumn(78);
                });
                foreach (string h in new[] { "Date", "What", "Reference", "Detail", "Debit", "Credit", "Balance" })
                    t.Cell().BorderBottom(1).BorderColor("#D9DEE3").PaddingVertical(4).Text(h).Bold();
                void Line(string date, string what, string reference, string detail, decimal? debit, decimal? credit, decimal balance, bool bold = false)
                {
                    IContainer Cell() => t.Cell().BorderBottom(0.5f).BorderColor("#EEF1F4").PaddingVertical(3);
                    var a = Cell().Text(date); var b = Cell().Text(what); var c = Cell().Text(reference); var d = Cell().Text(detail);
                    var e = Cell().AlignRight().Text(debit is > 0m ? debit.Value.ToString("N2") : "");
                    var f = Cell().AlignRight().Text(credit is > 0m ? credit.Value.ToString("N2") : "");
                    var g = Cell().AlignRight().Text(balance.ToString("N2"));
                    if (bold) { a.Bold(); b.Bold(); e.Bold(); f.Bold(); g.Bold(); }
                }
                Line(l.From.ToString("dd-MM-yyyy"), "Opening balance", "", "", null, null, l.OpeningBalance, true);
                foreach (LedgerRowDto r in l.Rows) Line(r.Date.ToString("dd-MM-yyyy"), r.Kind, r.Reference, r.Detail ?? "", r.Debit, r.Credit, r.Balance);
                Line(l.To.ToString("dd-MM-yyyy"), "Closing balance", "", "", l.TotalDebit, l.TotalCredit, l.ClosingBalance, true);
            });
            page.Footer().Column(f =>
            {
                f.Item().Text($"{side}: {FinanceRules.Money(l.ClosingBalance)}").Bold();
                f.Item().Text("Please confirm this balance, or tell us of any difference within 15 days. Disputes found early are easier to settle.").FontColor("#6B7280");
            });
        })).GeneratePdf();
    }
}
