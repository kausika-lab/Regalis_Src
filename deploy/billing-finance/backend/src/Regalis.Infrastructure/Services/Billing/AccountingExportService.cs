using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// The accounting export (build doc §20): one Excel workbook, one sheet per list, for the
/// accountant to load into Tally or Zoho Books. Regalis is not a ledger; this hands over
/// clean data instead. Tally XML and the Zoho CSV are left for later (doc §20.2).
/// </summary>
public class AccountingExportService(RegalisDbContext db, IPartyLedgerService ledgers) : IAccountingExportService
{
    private sealed class Sheet(IXLWorksheet ws, string[] head, int[] dates, int[] money)
    {
        private int _row = 2;

        public static Sheet Add(XLWorkbook wb, string name, string[] head, int[] dates, int[] money)
        {
            IXLWorksheet ws = wb.AddWorksheet(name);
            for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
            return new Sheet(ws, head, dates, money);
        }

        public void Row(params object?[] values)
        {
            for (int c = 0; c < values.Length; c++)
            {
                IXLCell cell = ws.Cell(_row, c + 1);
                switch (values[c])
                {
                    case null: break;
                    case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); break;
                    case decimal m: cell.Value = m; break;
                    case int i: cell.Value = i; break;
                    default: cell.Value = values[c]!.ToString(); break;
                }
            }
            _row++;
        }

        public void Finish() => ExcelStyle.Finish(ws, head.Length, _row - 1, dates, money);
    }

    public async Task<byte[]> ExportAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        if (end < start) throw new BusinessRuleException("The end date cannot be before the start date.");
        if (end.DayNumber - start.DayNumber > 400) throw new BusinessRuleException("Export up to one financial year at a time.");
        using var wb = new XLWorkbook();

        var sales = Sheet.Add(wb, "Sales", new[] { "Date", "Invoice", "Customer", "GSTIN", "Place of supply", "Taxable", "CGST", "SGST", "IGST", "Total", "Status" },
            new[] { 1 }, new[] { 6, 7, 8, 9, 10 });
        foreach (var i in await db.Invoices.AsNoTracking().Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end && i.Status != InvoiceStatus.Cancelled)
                     .OrderBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceId).ToListAsync(ct))
            sales.Row(i.InvoiceDate, i.InvoiceNumber, i.CustomerCompanyName ?? i.CustomerName, i.CustomerGstin, i.IgstAmount > 0m ? "Inter-state" : "Intra-state",
                i.TaxableAmount, i.CgstAmount, i.SgstAmount, i.IgstAmount, i.TotalAmount, i.Status.ToString());
        sales.Finish();

        DateTime fromUtc = IstClock.ToUtc(start.ToDateTime(TimeOnly.MinValue)), toUtc = IstClock.ToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var receipts = Sheet.Add(wb, "Receipts", new[] { "Date", "Receipt", "Payment", "Customer", "Mode", "Reference", "Amount", "Invoice settled" }, new[] { 1 }, new[] { 7 });
        var recorded = await (from p in db.Set<InvoicePayment>().AsNoTracking()
                              where p.PaidOn >= start && p.PaidOn <= end
                              join i in db.Invoices on p.InvoiceId equals i.InvoiceId
                              join pay in db.Payments on p.PaymentId equals (long?)pay.PaymentId into pj
                              from pay in pj.DefaultIfEmpty()
                              orderby p.PaidOn, p.InvoicePaymentId
                              select new { p, i.InvoiceNumber, i.CustomerName, PaymentNumber = pay != null ? pay.PaymentNumber : null }).ToListAsync(ct);
        foreach (var r in recorded)
            receipts.Row(r.p.PaidOn, r.p.ReceiptNumber, r.PaymentNumber, r.CustomerName, CustomerPaymentService.ModeName(r.p.Mode), r.p.Reference, r.p.Amount, r.InvoiceNumber);
        var others = await (from p in db.Payments.AsNoTracking()
                            where p.Status == PaymentStatus.Success && (p.PaidUtc ?? p.CreatedDate) >= fromUtc && (p.PaidUtc ?? p.CreatedDate) < toUtc
                                  && !db.Set<InvoicePayment>().Any(x => x.PaymentId == p.PaymentId)
                            join u in db.Users on p.CustomerId equals u.UserId into uj
                            from u in uj.DefaultIfEmpty()
                            join i in db.Invoices on p.InvoiceId equals (long?)i.InvoiceId into ij
                            from i in ij.DefaultIfEmpty()
                            orderby p.PaidUtc
                            select new { p, FullName = u != null ? u.FullName : "(removed customer)", InvoiceNumber = i != null ? i.InvoiceNumber : null }).ToListAsync(ct);
        foreach (var r in others)
            receipts.Row(IstClock.IstDateOf(r.p.PaidUtc ?? r.p.CreatedDate), null, r.p.PaymentNumber, r.FullName, $"{r.p.PaymentMethod}{(r.p.Gateway != null ? " · " + r.p.Gateway : "")}",
                r.p.GatewayPaymentId, r.p.Amount, r.InvoiceNumber);
        receipts.Finish();

        var purchases = Sheet.Add(wb, "Purchases", new[] { "Date", "Bill", "Supplier", "GSTIN", "Their bill no.", "Taxable", "CGST", "SGST", "IGST", "Total", "TDS", "Status" },
            new[] { 1 }, new[] { 6, 7, 8, 9, 10, 11 });
        var billStates = new[] { PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid, PurchaseBillStatus.Paid, PurchaseBillStatus.OnHold };
        foreach (var b in await (from b in db.Set<PurchaseBill>().AsNoTracking()
                                 where b.BillDate >= start && b.BillDate <= end && billStates.Contains(b.Status)
                                 join s in db.Suppliers on b.SupplierId equals s.SupplierId
                                 orderby b.BillDate, b.PurchaseBillId
                                 select new { b, s.SupplierName }).ToListAsync(ct))
            purchases.Row(b.b.BillDate, b.b.BillNumber, b.SupplierName, b.b.SupplierGstin, b.b.SupplierBillNo, b.b.TaxableValue, b.b.CgstAmount, b.b.SgstAmount,
                b.b.IgstAmount, b.b.BillTotal, b.b.TdsAmount, PurchaseBillService.StatusName(b.b.Status));
        purchases.Finish();

        var paid = Sheet.Add(wb, "Payments made", new[] { "Date", "Payment", "Supplier", "Mode", "Reference", "Amount", "TDS", "Advance adjusted", "Bills settled", "Reversed" },
            new[] { 1 }, new[] { 6, 7, 8 });
        foreach (var v in await (from v in db.Set<VendorPayment>().AsNoTracking().Include(x => x.Allocations)
                                 where v.PaidOn >= start && v.PaidOn <= end
                                 join s in db.Suppliers on v.SupplierId equals s.SupplierId
                                 orderby v.PaidOn, v.VendorPaymentId
                                 select new { v, s.SupplierName }).ToListAsync(ct))
        {
            var billIds = v.v.Allocations.Select(a => a.PurchaseBillId).ToList();
            var numbers = await db.Set<PurchaseBill>().Where(b => billIds.Contains(b.PurchaseBillId)).Select(b => b.SupplierBillNo).ToListAsync(ct);
            paid.Row(v.v.PaidOn, v.v.PaymentNumber, v.SupplierName, VendorPaymentService.ModeName(v.v.Mode) + (v.v.FromPettyCash ? " (petty cash)" : ""), v.v.Reference,
                v.v.Amount, v.v.TdsAmount, v.v.AdvanceAdjusted, numbers.Count == 0 ? "Advance" : string.Join(", ", numbers), v.v.IsReversed ? "Yes" : "");
        }
        paid.Finish();

        var exp = Sheet.Add(wb, "Expenses", new[] { "Date", "Voucher", "Category", "Department", "Description", "Amount", "Supplier", "Paid from", "Status" },
            new[] { 1 }, new[] { 6 });
        foreach (var x in await (from e in db.Expenses.AsNoTracking()
                                 where e.ExpenseDate >= start && e.ExpenseDate <= end
                                 join c in db.Set<ExpenseControl>() on e.ExpenseId equals c.ExpenseId into cj
                                 from c in cj.DefaultIfEmpty()
                                 join d in db.Set<Department>() on (c != null ? c.DepartmentId : null) equals (int?)d.DepartmentId into dj
                                 from d in dj.DefaultIfEmpty()
                                 join s in db.Suppliers on e.SupplierId equals (int?)s.SupplierId into sj
                                 from s in sj.DefaultIfEmpty()
                                 where c == null || (c.ApprovalStatus == ExpenseApprovalStatus.Approved && !c.IsRecurringDraft)
                                 orderby e.ExpenseDate, e.ExpenseId
                                 select new { e, Dept = d != null ? d.Name : null, Supplier = s != null ? s.SupplierName : null,
                                     From = c == null ? "Bank" : c.PaidFrom == ExpensePaidFrom.PettyCash ? "Petty cash" : c.PaidFrom == ExpensePaidFrom.Staff ? "Staff (reimbursed)" : "Bank" })
                     .ToListAsync(ct))
            exp.Row(x.e.ExpenseDate, x.e.ExpenseNumber, x.e.Category, x.Dept, x.e.Description, x.e.Amount + x.e.AdjustmentTotal, x.Supplier, x.From, "Approved");
        exp.Finish();

        foreach (var (dir, name) in new[] { (TdsDirection.Payable, "TDS payable"), (TdsDirection.Receivable, "TDS receivable") })
        {
            var tds = Sheet.Add(wb, name, new[] { "Date", dir == TdsDirection.Payable ? "Supplier" : "Customer", dir == TdsDirection.Payable ? "PAN" : "TAN", "Section", "Base",
                "Rate %", "TDS", "Quarter", dir == TdsDirection.Payable ? "Challan" : "Certificate" }, new[] { 1 }, new[] { 5, 7 });
            foreach (var t in await (from t in db.Set<TdsEntry>().AsNoTracking()
                                     where t.Direction == dir && t.DeductedOn >= start && t.DeductedOn <= end
                                     join c in db.Set<TdsChallan>() on t.TdsChallanId equals (long?)c.TdsChallanId into cj
                                     from c in cj.DefaultIfEmpty()
                                     orderby t.DeductedOn
                                     select new { t, Challan = c != null ? c.ChallanNo : null }).ToListAsync(ct))
                tds.Row(t.t.DeductedOn, t.t.PartyName, t.t.PartyTaxId, t.t.Section, t.t.BaseAmount, t.t.Rate, t.t.TdsAmount, $"Q{t.t.Quarter} {t.t.FinancialYear}",
                    dir == TdsDirection.Payable ? t.Challan ?? "Not paid" : t.t.CertificateNo ?? "Missing");
            tds.Finish();
        }

        var bank = Sheet.Add(wb, "Bank", new[] { "Date", "Account", "In", "Out", "Narration", "Bank reference", "Matched to", "Differences" }, new[] { 1 }, new[] { 3, 4 });
        var lines = await (from t in db.BankTransactions.AsNoTracking()
                           where t.TransactionDate >= start && t.TransactionDate <= end && t.ReconciliationStatus == ReconciliationStatus.Reconciled
                           join a in db.BankAccounts on t.BankAccountId equals a.BankAccountId
                           orderby t.TransactionDate, t.BankTransactionId
                           select new { t, a.AccountCode }).ToListAsync(ct);
        var ids = lines.Select(l => l.t.BankTransactionId).ToList();
        var allocs = await db.Set<BankMatchAllocation>().AsNoTracking().Where(m => ids.Contains(m.BankTransactionId)).ToListAsync(ct);
        var diffs = await db.Set<BankMatchDifference>().AsNoTracking().Where(m => ids.Contains(m.BankTransactionId)).ToListAsync(ct);
        foreach (var l in lines)
        {
            bool credit = l.t.Direction == BankTransactionDirection.Credit;
            string matched = string.Join(", ", allocs.Where(a => a.BankTransactionId == l.t.BankTransactionId).Select(a => $"{a.DocNumber} ({a.Amount:N2})"));
            string explained = string.Join(", ", diffs.Where(d => d.BankTransactionId == l.t.BankTransactionId).Select(d => $"{d.Reason} {d.SignedAmount:N2}"));
            bank.Row(l.t.TransactionDate, l.AccountCode, credit ? l.t.Amount : null, credit ? null : l.t.Amount, l.t.Narration, l.t.BankReferenceId ?? l.t.ReferenceNumber,
                matched.Length > 0 ? matched : "(matched before this export existed)", explained);
        }
        bank.Finish();

        var parties = Sheet.Add(wb, "Parties", new[] { "Type", "Name", "GSTIN", "PAN / TAN", "Closing balance" }, Array.Empty<int>(), new[] { 5 });
        foreach (var p in await ledgers.GetOutstandingAsync("customer", ct))
        {
            string? gstin = await db.Invoices.Where(i => i.CustomerId == p.PartyId && i.CustomerGstin != null).Select(i => i.CustomerGstin).FirstOrDefaultAsync(ct);
            string? tan = await db.Set<CustomerTaxProfile>().Where(x => x.CustomerId == p.PartyId).Select(x => x.Tan).FirstOrDefaultAsync(ct);
            parties.Row("Customer", p.PartyName, gstin, tan, p.Balance);
        }
        foreach (var s in await (from s in db.Suppliers.AsNoTracking()
                                 join tp in db.Set<SupplierTaxProfile>() on s.SupplierId equals tp.SupplierId into tj
                                 from tp in tj.DefaultIfEmpty()
                                 orderby s.SupplierName
                                 select new { s.SupplierId, s.SupplierName, s.Gstin, Pan = tp != null ? tp.Pan : null }).ToListAsync(ct))
        {
            PartyLedgerDto ledger = await ledgers.GetLedgerAsync("supplier", s.SupplierId, new DateOnly(2000, 1, 1), end, ct);
            parties.Row("Supplier", s.SupplierName, s.Gstin, s.Pan, ledger.ClosingBalance);
        }
        parties.Finish();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
