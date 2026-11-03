using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// GST (build doc §11). The output side comes from invoices, the input side from approved
/// purchase bills with a supplier GSTIN. Credit is only treated as claimable once the bill
/// appears in the uploaded GSTR-2B; the rest is held back. Nothing is filed from here.
/// </summary>
public class GstService(RegalisDbContext db, ISettingService settings, ICurrentUser currentUser, IDateTimeProvider clock) : IGstService
{
    private static readonly PurchaseBillStatus[] OnBooks = { PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid, PurchaseBillStatus.Paid, PurchaseBillStatus.OnHold };

    private static (DateOnly From, DateOnly To) Range(string period)
    {
        if (!DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly from))
            throw new BusinessRuleException("Choose a month.");
        return (from, from.AddMonths(1).AddDays(-1));
    }

    private IQueryable<Invoice> Sales(string period)
    {
        var (from, to) = Range(period);
        return db.Invoices.AsNoTracking().Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Cancelled);
    }

    private IQueryable<PurchaseBill> Purchases(string period)
    {
        var (from, to) = Range(period);
        return db.Set<PurchaseBill>().AsNoTracking().Where(b => b.BillDate >= from && b.BillDate <= to && OnBooks.Contains(b.Status) && b.SupplierGstin != null);
    }

    public async Task<GstSummaryDto> GetSummaryAsync(string period, CancellationToken ct = default)
    {
        var (from, _) = Range(period);
        var output = await Sales(period).GroupBy(_ => 1).Select(g => new { C = g.Sum(i => i.CgstAmount), S = g.Sum(i => i.SgstAmount), I = g.Sum(i => i.IgstAmount), N = g.Count() }).FirstOrDefaultAsync(ct);
        var input = await Purchases(period).GroupBy(_ => 1).Select(g => new { C = g.Sum(b => b.CgstAmount), S = g.Sum(b => b.SgstAmount), I = g.Sum(b => b.IgstAmount), N = g.Count() }).FirstOrDefaultAsync(ct);
        bool has2b = await db.Set<Gstr2bLine>().AnyAsync(l => l.Period == period, ct);
        var held = has2b
            ? await (from l in db.Set<Gstr2bLine>().AsNoTracking()
                     where l.Period == period && (l.Result == Gstr2bResult.MissingIn2b || l.Result == Gstr2bResult.Differs) && l.MatchedBillId != null
                     join b in db.Set<PurchaseBill>() on l.MatchedBillId equals (long?)b.PurchaseBillId
                     select b.CgstAmount + b.SgstAmount + b.IgstAmount).ToListAsync(ct)
            : new List<decimal>();
        decimal outTotal = (output?.C ?? 0m) + (output?.S ?? 0m) + (output?.I ?? 0m);
        decimal inTotal = (input?.C ?? 0m) + (input?.S ?? 0m) + (input?.I ?? 0m);
        decimal claimable = inTotal - held.Sum();
        decimal net = outTotal - claimable;
        return new GstSummaryDto(period, new GstTaxTotalsDto(output?.C ?? 0m, output?.S ?? 0m, output?.I ?? 0m, outTotal),
            new GstTaxTotalsDto(input?.C ?? 0m, input?.S ?? 0m, input?.I ?? 0m, inTotal), held.Sum(), held.Count, claimable,
            Math.Max(0m, net), Math.Max(0m, -net), from.AddMonths(1).AddDays(19), has2b, output?.N ?? 0, input?.N ?? 0,
            await settings.GetStringAsync("Company.Gstin", "", ct) is { Length: > 0 } g ? g : null);
    }

    public async Task<GstRegisterDto> GetSalesRegisterAsync(string period, CancellationToken ct = default)
    {
        var rows = await Sales(period).OrderBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceId)
            .Select(i => new GstSalesRowDto(i.InvoiceId, i.InvoiceNumber, i.InvoiceDate, i.CustomerName, i.CustomerGstin, i.CgstPercent + i.SgstPercent + i.IgstPercent,
                i.TaxableAmount, i.CgstAmount, i.SgstAmount, i.IgstAmount, i.TotalAmount, (byte)i.Status)).ToListAsync(ct);
        var byRate = rows.GroupBy(r => r.Rate).OrderByDescending(g => g.Key)
            .Select(g => new GstRateRowDto(g.Key, g.Sum(r => r.Taxable), g.Sum(r => r.Cgst), g.Sum(r => r.Sgst), g.Sum(r => r.Igst), g.Count())).ToList();
        return new GstRegisterDto(period, byRate, Total(byRate), rows, Array.Empty<GstPurchaseRowDto>());
    }

    private static GstRateRowDto Total(List<GstRateRowDto> rows) =>
        new(-1m, rows.Sum(r => r.Taxable), rows.Sum(r => r.Cgst), rows.Sum(r => r.Sgst), rows.Sum(r => r.Igst), rows.Sum(r => r.Documents));

    public async Task<GstRegisterDto> GetPurchaseRegisterAsync(string period, CancellationToken ct = default)
    {
        var bills = await (from b in Purchases(period)
                           join s in db.Suppliers on b.SupplierId equals s.SupplierId
                           orderby b.BillDate, b.PurchaseBillId
                           select new { b, s.SupplierName }).ToListAsync(ct);
        var ids = bills.Select(x => x.b.PurchaseBillId).ToList();
        var lines = await db.Set<PurchaseBillLine>().AsNoTracking().Where(l => ids.Contains(l.PurchaseBillId)).ToListAsync(ct);
        var results = await db.Set<Gstr2bLine>().AsNoTracking().Where(l => l.Period == period && l.MatchedBillId != null)
            .ToDictionaryAsync(l => l.MatchedBillId!.Value, l => l.Result, ct);
        var byRate = lines.GroupBy(l => l.GstRate).OrderByDescending(g => g.Key)
            .Select(g => new GstRateRowDto(g.Key, g.Sum(l => l.TaxableValue), g.Sum(l => l.CgstAmount), g.Sum(l => l.SgstAmount), g.Sum(l => l.IgstAmount),
                g.Select(l => l.PurchaseBillId).Distinct().Count())).ToList();
        var rows = bills.Select(x => new GstPurchaseRowDto(x.b.PurchaseBillId, x.b.BillNumber, x.b.SupplierBillNo, x.b.BillDate, x.SupplierName, x.b.SupplierGstin,
            x.b.TaxableValue, x.b.CgstAmount, x.b.SgstAmount, x.b.IgstAmount, x.b.BillTotal,
            results.TryGetValue(x.b.PurchaseBillId, out Gstr2bResult r) ? ResultLabel(r) : null)).ToList();
        var total = Total(byRate) with { Documents = bills.Count };
        return new GstRegisterDto(period, byRate, total, Array.Empty<GstSalesRowDto>(), rows);
    }

    public async Task<byte[]> ExportRegisterAsync(string kind, string period, CancellationToken ct = default)
    {
        bool sales = string.Equals(kind, "sales", StringComparison.OrdinalIgnoreCase);
        GstRegisterDto reg = sales ? await GetSalesRegisterAsync(period, ct) : await GetPurchaseRegisterAsync(period, ct);
        using var wb = new XLWorkbook();
        IXLWorksheet summary = wb.AddWorksheet(sales ? "Outward supplies by rate" : "Eligible ITC by rate");
        string[] h1 = { "GST rate %", "Taxable value", "CGST", "SGST", "IGST", sales ? "Invoices" : "Bills" };
        for (int c = 0; c < h1.Length; c++) summary.Cell(1, c + 1).Value = h1[c];
        int r = 2;
        foreach (GstRateRowDto row in reg.ByRate)
        {
            summary.Cell(r, 1).Value = row.Rate == 0m ? "Exempt / nil" : row.Rate.ToString("0.##", CultureInfo.InvariantCulture);
            summary.Cell(r, 2).Value = row.Taxable; summary.Cell(r, 3).Value = row.Cgst; summary.Cell(r, 4).Value = row.Sgst; summary.Cell(r, 5).Value = row.Igst; summary.Cell(r, 6).Value = row.Documents;
            r++;
        }
        ExcelStyle.Finish(summary, h1.Length, r - 1, Array.Empty<int>(), new[] { 2, 3, 4, 5 });
        IXLWorksheet detail = wb.AddWorksheet(sales ? "Invoices" : "Bills");
        string[] h2 = sales
            ? new[] { "Invoice", "Date", "Customer", "GSTIN", "Rate %", "Taxable", "CGST", "SGST", "IGST", "Total" }
            : new[] { "Bill", "Supplier bill no.", "Date", "Supplier", "GSTIN", "Taxable", "CGST", "SGST", "IGST", "Total", "GSTR-2B" };
        for (int c = 0; c < h2.Length; c++) detail.Cell(1, c + 1).Value = h2[c];
        r = 2;
        if (sales)
        {
            foreach (GstSalesRowDto s in reg.Sales)
            {
                detail.Cell(r, 1).Value = s.InvoiceNumber; detail.Cell(r, 2).Value = s.InvoiceDate.ToDateTime(TimeOnly.MinValue); detail.Cell(r, 3).Value = s.CustomerName;
                detail.Cell(r, 4).Value = s.CustomerGstin ?? ""; detail.Cell(r, 5).Value = s.Rate; detail.Cell(r, 6).Value = s.Taxable; detail.Cell(r, 7).Value = s.Cgst;
                detail.Cell(r, 8).Value = s.Sgst; detail.Cell(r, 9).Value = s.Igst; detail.Cell(r, 10).Value = s.Total;
                r++;
            }
            ExcelStyle.Finish(detail, h2.Length, r - 1, new[] { 2 }, new[] { 6, 7, 8, 9, 10 });
        }
        else
        {
            foreach (GstPurchaseRowDto p in reg.Purchases)
            {
                detail.Cell(r, 1).Value = p.BillNumber; detail.Cell(r, 2).Value = p.SupplierBillNo; detail.Cell(r, 3).Value = p.BillDate.ToDateTime(TimeOnly.MinValue);
                detail.Cell(r, 4).Value = p.SupplierName; detail.Cell(r, 5).Value = p.SupplierGstin ?? ""; detail.Cell(r, 6).Value = p.Taxable; detail.Cell(r, 7).Value = p.Cgst;
                detail.Cell(r, 8).Value = p.Sgst; detail.Cell(r, 9).Value = p.Igst; detail.Cell(r, 10).Value = p.Total; detail.Cell(r, 11).Value = p.Gstr2bResult ?? "Not compared";
                r++;
            }
            ExcelStyle.Finish(detail, h2.Length, r - 1, new[] { 3 }, new[] { 6, 7, 8, 9, 10 });
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── GSTR-2B ─────────────────────────────────────────────────────────────────

    private sealed record TwoBLine(string Gstin, string? Name, string BillNo, DateOnly? Date, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst);

    private static decimal Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;

    private static List<TwoBLine> ParseJson(Stream s)
    {
        using JsonDocument doc = JsonDocument.Parse(s);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("data", out JsonElement data)) root = data;
        if (root.TryGetProperty("docdata", out JsonElement docdata)) root = docdata;
        if (!root.TryGetProperty("b2b", out JsonElement b2b)) throw new BusinessRuleException("This JSON has no B2B section. Download the GSTR-2B JSON from the GST portal.");
        var list = new List<TwoBLine>();
        foreach (JsonElement sup in b2b.EnumerateArray())
        {
            string gstin = sup.GetProperty("ctin").GetString() ?? "";
            string? name = sup.TryGetProperty("trdnm", out JsonElement n) ? n.GetString() : null;
            if (!sup.TryGetProperty("inv", out JsonElement invs)) continue;
            foreach (JsonElement inv in invs.EnumerateArray())
            {
                string no = inv.GetProperty("inum").GetString() ?? "";
                DateOnly? dt = inv.TryGetProperty("dt", out JsonElement d) && DateOnly.TryParseExact(d.GetString(), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly p) ? p : null;
                decimal tx = 0m, c = 0m, sg = 0m, ig = 0m;
                if (inv.TryGetProperty("items", out JsonElement items))
                    foreach (JsonElement it in items.EnumerateArray()) { tx += Num(it, "txval"); c += Num(it, "cgst"); sg += Num(it, "sgst"); ig += Num(it, "igst"); }
                else { tx = Num(inv, "txval"); c = Num(inv, "cgst"); sg = Num(inv, "sgst"); ig = Num(inv, "igst"); }
                list.Add(new TwoBLine(gstin.ToUpperInvariant(), name, no, dt, tx, c, sg, ig));
            }
        }
        return list;
    }

    private static List<TwoBLine> ParseExcel(Stream s)
    {
        using var wb = new XLWorkbook(s);
        IXLWorksheet ws = wb.Worksheets.FirstOrDefault(w => w.Name.Contains("B2B", StringComparison.OrdinalIgnoreCase) && !w.Name.Contains("CDN", StringComparison.OrdinalIgnoreCase))
                          ?? wb.Worksheets.First();
        IXLRange? used = ws.RangeUsed();
        if (used == null) return new List<TwoBLine>();
        int lastRow = used.LastRow().RowNumber(), lastCol = used.LastColumn().ColumnNumber();
        int header = 0;
        var cols = new Dictionary<string, int>();
        for (int r = 1; r <= Math.Min(lastRow, 15) && header == 0; r++)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                string v = ws.Cell(r, c).GetFormattedString().ToLowerInvariant();
                if (v.Contains("gstin")) cols.TryAdd("gstin", c);
                if (v.Contains("trade") || v.Contains("legal name")) cols.TryAdd("name", c);
                if (v.Contains("invoice number") || v.Contains("invoice no")) cols.TryAdd("no", c);
                if (v.Contains("invoice date")) cols.TryAdd("date", c);
                if (v.Contains("taxable value")) cols.TryAdd("tx", c);
                if (v.Contains("integrated tax")) cols.TryAdd("igst", c);
                if (v.Contains("central tax")) cols.TryAdd("cgst", c);
                if (v.Contains("state/ut tax") || v.Contains("state tax")) cols.TryAdd("sgst", c);
            }
            if (cols.ContainsKey("gstin") && cols.ContainsKey("no")) header = r;
            else cols.Clear();
        }
        if (header == 0) throw new BusinessRuleException("No B2B table was found in this Excel. Use the GSTR-2B Excel downloaded from the GST portal.");
        // A two-row header (Tax amount / Integrated tax) - look one more row down for the tax columns.
        for (int c = 1; c <= lastCol; c++)
        {
            string v = ws.Cell(header + 1, c).GetFormattedString().ToLowerInvariant();
            if (v.Contains("integrated tax")) cols["igst"] = c;
            if (v.Contains("central tax")) cols["cgst"] = c;
            if (v.Contains("state/ut tax") || v.Contains("state tax")) cols["sgst"] = c;
            if (v.Contains("taxable value")) cols["tx"] = c;
        }
        decimal Dec(int r, string k) => cols.TryGetValue(k, out int c) && decimal.TryParse(ws.Cell(r, c).GetFormattedString().Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
        var list = new List<TwoBLine>();
        for (int r = header + 1; r <= lastRow; r++)
        {
            string gstin = ws.Cell(r, cols["gstin"]).GetFormattedString().Trim().ToUpperInvariant();
            string no = ws.Cell(r, cols["no"]).GetFormattedString().Trim();
            if (gstin.Length != 15 || no.Length == 0) continue;
            DateOnly? date = null;
            if (cols.TryGetValue("date", out int dc))
            {
                IXLCell cell = ws.Cell(r, dc);
                if (cell.DataType == XLDataType.DateTime) date = DateOnly.FromDateTime(cell.GetDateTime());
                else if (DateOnly.TryParseExact(cell.GetFormattedString().Trim(), new[] { "dd-MM-yyyy", "dd/MM/yyyy", "dd-MMM-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly p)) date = p;
            }
            list.Add(new TwoBLine(gstin, cols.TryGetValue("name", out int nc) ? ws.Cell(r, nc).GetFormattedString().Trim() : null, no, date,
                Dec(r, "tx"), Dec(r, "cgst"), Dec(r, "sgst"), Dec(r, "igst")));
        }
        return list;
    }

    private static string ResultLabel(Gstr2bResult r) => r switch
    {
        Gstr2bResult.Matches => "Matches",
        Gstr2bResult.MissingIn2b => "Missing in 2B",
        Gstr2bResult.Differs => "Differs",
        _ => "Bill missing here"
    };

    private static string Action(Gstr2bResult r) => r switch
    {
        Gstr2bResult.Matches => "Claim",
        Gstr2bResult.MissingIn2b => "Hold - the supplier has not filed",
        Gstr2bResult.Differs => "Query the supplier",
        _ => "Enter it - credit going unclaimed"
    };

    public async Task<Gstr2bResultDto> Upload2bAsync(string period, BankUploadInput file, CancellationToken ct = default)
    {
        Range(period);
        if (file.Length <= 0) throw new BusinessRuleException("Choose the GSTR-2B file downloaded from the GST portal.");
        using var ms = new MemoryStream();
        await file.Content.CopyToAsync(ms, ct);
        ms.Position = 0;
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        List<TwoBLine> lines;
        try
        {
            lines = ext == ".json" ? ParseJson(ms) : ext is ".xlsx" or ".xlsm" ? ParseExcel(ms)
                : throw new BusinessRuleException("Upload the GSTR-2B as JSON or Excel (.xlsx).");
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("This JSON file cannot be read. Download it again from the GST portal.");
        }

        var bills = await Purchases(period).Select(b => new { b.PurchaseBillId, b.SupplierGstin, b.SupplierBillNoKey, b.CgstAmount, b.SgstAmount, b.IgstAmount }).ToListAsync(ct);
        DateTime now = clock.UtcNow;
        var rows = new List<Gstr2bLine>();
        var matchedBills = new HashSet<long>();
        foreach (TwoBLine l in lines)
        {
            string key = FinanceRules.BillNoKey(l.BillNo);
            var bill = bills.FirstOrDefault(b => string.Equals(b.SupplierGstin, l.Gstin, StringComparison.OrdinalIgnoreCase) && b.SupplierBillNoKey == key);
            decimal tax2b = l.Cgst + l.Sgst + l.Igst;
            Gstr2bResult result = bill == null ? Gstr2bResult.MissingHere
                : Math.Abs(bill.CgstAmount + bill.SgstAmount + bill.IgstAmount - tax2b) <= 1m ? Gstr2bResult.Matches : Gstr2bResult.Differs;
            if (bill != null) matchedBills.Add(bill.PurchaseBillId);
            rows.Add(new Gstr2bLine { Period = period, SupplierGstin = l.Gstin, SupplierName = l.Name, BillNo = l.BillNo.Length > 60 ? l.BillNo[..60] : l.BillNo, BillDate = l.Date,
                TaxableValue = l.Taxable, Cgst = l.Cgst, Sgst = l.Sgst, Igst = l.Igst, MatchedBillId = bill?.PurchaseBillId, Result = result, UploadedUtc = now, UploadedByUserId = currentUser.UserId });
        }
        foreach (var b in bills.Where(b => !matchedBills.Contains(b.PurchaseBillId)))
        {
            rows.Add(new Gstr2bLine { Period = period, SupplierGstin = b.SupplierGstin!, BillNo = b.SupplierBillNoKey, MatchedBillId = b.PurchaseBillId,
                Result = Gstr2bResult.MissingIn2b, UploadedUtc = now, UploadedByUserId = currentUser.UserId });
        }
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            // A re-upload replaces the comparison for the month.
            await db.Set<Gstr2bLine>().Where(l => l.Period == period).ExecuteDeleteAsync(ct);
            db.Set<Gstr2bLine>().AddRange(rows);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await Get2bAsync(period, ct);
    }

    public async Task<Gstr2bResultDto> Get2bAsync(string period, CancellationToken ct = default)
    {
        var lines = await (from l in db.Set<Gstr2bLine>().AsNoTracking()
                           where l.Period == period
                           join b in db.Set<PurchaseBill>() on l.MatchedBillId equals (long?)b.PurchaseBillId into bj
                           from b in bj.DefaultIfEmpty()
                           join s in db.Suppliers on (b != null ? b.SupplierId : 0) equals s.SupplierId into sj
                           from s in sj.DefaultIfEmpty()
                           orderby l.Result, l.SupplierGstin
                           select new { l, Bill = b, SupplierName = s != null ? s.SupplierName : null }).ToListAsync(ct);
        var rows = lines.Select(x => new Gstr2bRowDto(x.l.Gstr2bLineId, x.l.SupplierGstin, x.SupplierName ?? x.l.SupplierName,
            x.Bill?.SupplierBillNo ?? x.l.BillNo, x.l.BillDate ?? x.Bill?.BillDate,
            x.Bill != null ? x.Bill.CgstAmount + x.Bill.SgstAmount + x.Bill.IgstAmount : 0m, x.l.Cgst + x.l.Sgst + x.l.Igst,
            (byte)x.l.Result, x.l.Result == Gstr2bResult.Differs && x.Bill != null
                ? $"Differs by {FinanceRules.Money(Math.Abs(x.Bill.CgstAmount + x.Bill.SgstAmount + x.Bill.IgstAmount - (x.l.Cgst + x.l.Sgst + x.l.Igst)))}"
                : ResultLabel(x.l.Result),
            Action(x.l.Result), x.l.MatchedBillId)).ToList();
        return new Gstr2bResultDto(period, lines.Count == 0 ? null : lines.Max(x => x.l.UploadedUtc),
            rows.Count(r => r.Result == (byte)Gstr2bResult.Matches), rows.Count(r => r.Result == (byte)Gstr2bResult.MissingIn2b),
            rows.Count(r => r.Result == (byte)Gstr2bResult.Differs), rows.Count(r => r.Result == (byte)Gstr2bResult.MissingHere),
            rows.Where(r => r.Result is (byte)Gstr2bResult.MissingIn2b or (byte)Gstr2bResult.Differs).Sum(r => r.YourTax),
            rows.Where(r => r.Result == (byte)Gstr2bResult.MissingHere).Sum(r => r.Tax2b), rows);
    }

    // ── Return tracker ──────────────────────────────────────────────────────────

    private static DateOnly DueFor(string period, string type)
    {
        var (from, _) = Range(period);
        return from.AddMonths(1).AddDays(type == "GSTR1" ? 10 : 19);
    }

    public async Task<IReadOnlyList<GstReturnRowDto>> GetReturnsAsync(string? financialYear, CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        string fy = string.IsNullOrWhiteSpace(financialYear) ? FinanceRules.FinancialYear(today) : financialYear;
        int y = int.Parse(fy[..4]);
        var periods = Enumerable.Range(0, 12).Select(i => new DateOnly(y, 4, 1).AddMonths(i)).Where(d => d <= today).Select(FinanceRules.Period).ToList();
        var saved = await (from r in db.Set<GstReturn>().AsNoTracking()
                           where periods.Contains(r.Period)
                           join u in db.Users on r.FiledByUserId equals (long?)u.UserId into uj
                           from u in uj.DefaultIfEmpty()
                           select new { r, By = u != null ? u.FullName : null }).ToListAsync(ct);
        var rows = new List<GstReturnRowDto>();
        foreach (string p in Enumerable.Reverse(periods))
        {
            foreach (string type in new[] { "GSTR1", "GSTR3B" })
            {
                var s = saved.FirstOrDefault(x => x.r.Period == p && x.r.ReturnType == type);
                bool monthOver = Range(p).To < today;
                byte status = s != null ? (byte)s.r.Status : monthOver ? (byte)GstReturnStatus.Ready : (byte)GstReturnStatus.NotStarted;
                rows.Add(new GstReturnRowDto(p, type, status, s?.r.Arn, s?.r.FiledOn, s?.By, DueFor(p, type)));
            }
        }
        return rows;
    }

    public async Task<GstReturnRowDto> SaveReturnAsync(SaveGstReturnRequest request, CancellationToken ct = default)
    {
        Range(request.Period);
        string type = (request.ReturnType ?? "").ToUpperInvariant().Replace("-", "");
        if (type is not ("GSTR1" or "GSTR3B")) throw new BusinessRuleException("The return is GSTR-1 or GSTR-3B.");
        if (request.Status < 1 || request.Status > 3) throw new BusinessRuleException("Choose the status.");
        var status = (GstReturnStatus)request.Status;
        if (status == GstReturnStatus.Filed)
        {
            if (string.IsNullOrWhiteSpace(request.Arn) || request.FiledOn == null) throw new BusinessRuleException("Enter the ARN and the date it was filed.");
            FinanceRules.EnsureNotFuture(request.FiledOn.Value, clock, "The filing date cannot be in the future.");
        }
        GstReturn? row = await db.Set<GstReturn>().FirstOrDefaultAsync(r => r.Period == request.Period && r.ReturnType == type, ct);
        if (row == null)
        {
            row = new GstReturn { Period = request.Period, ReturnType = type };
            db.Set<GstReturn>().Add(row);
        }
        row.Status = status;
        row.Arn = status == GstReturnStatus.Filed ? request.Arn!.Trim() : null;
        row.FiledOn = status == GstReturnStatus.Filed ? request.FiledOn : null;
        row.FiledByUserId = status == GstReturnStatus.Filed ? currentUser.UserId : null;
        await db.SaveChangesAsync(ct);
        string fy = FinanceRules.FinancialYear(Range(request.Period).From);
        return (await GetReturnsAsync(fy, ct)).First(r => r.Period == request.Period && r.ReturnType == type);
    }

    public async Task<IReadOnlyList<GstRateOptionRowDto>> GetRatesAsync(CancellationToken ct = default)
    {
        return await db.Set<GstRate>().AsNoTracking().OrderByDescending(r => r.Rate).ThenByDescending(r => r.EffectiveFrom)
            .Select(r => new GstRateOptionRowDto(r.GstRateId, r.Rate, r.Label, r.EffectiveFrom, r.EffectiveTo, r.IsActive)).ToListAsync(ct);
    }

    public async Task<GstRateOptionRowDto> SaveRateAsync(int? id, SaveGstRateRequest request, CancellationToken ct = default)
    {
        if (request.Rate < 0m || request.Rate > 40m) throw new BusinessRuleException("A GST rate is between 0% and 40%.");
        if (request.EffectiveTo != null && request.EffectiveTo < request.EffectiveFrom) throw new BusinessRuleException("The end date cannot be before the start date.");
        GstRate row;
        if (id == null)
        {
            row = new GstRate();
            db.Set<GstRate>().Add(row);
        }
        else
        {
            row = await db.Set<GstRate>().FirstOrDefaultAsync(r => r.GstRateId == id, ct) ?? throw new NotFoundException("GST rate", id.Value);
        }
        row.Rate = request.Rate;
        row.Label = string.IsNullOrWhiteSpace(request.Label) ? $"{request.Rate:0.##}%" : request.Label.Trim();
        row.EffectiveFrom = request.EffectiveFrom;
        row.EffectiveTo = request.EffectiveTo;
        row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new GstRateOptionRowDto(row.GstRateId, row.Rate, row.Label, row.EffectiveFrom, row.EffectiveTo, row.IsActive);
    }
}
