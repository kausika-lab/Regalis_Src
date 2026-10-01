using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

public record WalletStatementLine(string Number, DateTime Utc, string Narration, WalletCategory Category,
    WalletDirection Direction, decimal Amount, decimal BalanceAfter);

public record WalletStatementPdfModel(
    string CompanyName,
    string? CompanyAddress,
    string? CompanyGstin,
    string CustomerName,
    string CustomerEmail,
    string? CustomerMobile,
    DateOnly? From,
    DateOnly To,
    decimal Opening,
    decimal Closing,
    IReadOnlyList<WalletStatementLine> Lines,
    DateTime GeneratedUtc,
    byte[]? Logo = null);

/// <summary>A customer's wallet statement for a period: opening balance, every ledger
/// entry in date order, closing balance — the same letterhead and palette as the
/// invoice, receipt and quotation PDFs.</summary>
public static class WalletStatementPdfGenerator
{
    private const string Gold = "#CC9A3D";
    private const string GoldDeep = "#B8842B";
    private const string Black = "#0B0B0D";
    private const string Ink = "#241F19";
    private const string Muted = "#8C8272";
    private const string Border = "#EEE4D2";
    private const string Tint = "#FBF6EC";
    private const string Green = "#1F7A3A";
    private const string Red = "#B3261E";

    static WalletStatementPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string CategoryLabel(WalletCategory category) => category switch
    {
        WalletCategory.Recharge => "Recharge",
        WalletCategory.Booking => "Booking",
        WalletCategory.Refund => "Refund",
        WalletCategory.DayPass => "Day pass",
        WalletCategory.MonthlyPass => "Membership",
        WalletCategory.ServiceOrder => "Service",
        WalletCategory.SecurityDeposit => "Deposit",
        WalletCategory.AdminAdjustment => "Adjustment",
        WalletCategory.Cancellation => "Refund",
        _ => "Other"
    };

    public static byte[] Generate(WalletStatementPdfModel m)
    {
        var credits = m.Lines.Where(l => l.Direction == WalletDirection.Credit).Sum(l => l.Amount);
        var debits = m.Lines.Where(l => l.Direction == WalletDirection.Debit).Sum(l => l.Amount);
        var period = $"{(m.From is null ? "Account opening" : m.From.Value.ToString("dd MMM yyyy"))} – {m.To:dd MMM yyyy}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(s => s.FontSize(9.5f).FontColor(Ink));

                page.Header().Column(head =>
                {
                    head.Item().Row(row =>
                    {
                        if (m.Logo is { Length: > 0 })
                        {
                            PdfLogo.Compose(row, m.Logo);
                        }
                        else
                        {
                            row.ConstantItem(40).Height(40).Background(Black).AlignCenter().AlignMiddle()
                                .Text("R").Bold().FontSize(22).FontColor(Gold);
                            row.ConstantItem(10);
                        }
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(m.CompanyName).Bold().FontSize(16).FontColor(Black);
                            if (!string.IsNullOrWhiteSpace(m.CompanyAddress)) c.Item().Text(m.CompanyAddress).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(m.CompanyGstin)) c.Item().Text($"GSTIN: {m.CompanyGstin}").FontColor(Muted);
                        });
                        row.ConstantItem(210).Column(c =>
                        {
                            c.Item().AlignRight().Text("WALLET STATEMENT").Bold().FontSize(15).FontColor(Gold).LetterSpacing(0.06f);
                            c.Item().AlignRight().Text(period).FontColor(Muted);
                        });
                    });
                    head.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Gold);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Border(1).BorderColor(Border).Padding(9).Column(c =>
                        {
                            c.Item().PaddingBottom(3).Text("Customer").Bold().FontSize(8.5f).FontColor(GoldDeep);
                            c.Item().Text(m.CustomerName).Bold();
                            c.Item().Text(string.Join(" · ", new[] { m.CustomerEmail, m.CustomerMobile }
                                .Where(x => !string.IsNullOrWhiteSpace(x)))).FontColor(Muted);
                        });
                        foreach (var (label, value, colour) in new[]
                                 {
                                     ("Opening balance", m.Opening, Ink),
                                     ("Credits", credits, Green),
                                     ("Debits", debits, Red),
                                     ("Closing balance", m.Closing, Black)
                                 })
                        {
                            r.ConstantItem(92).Background(label == "Closing balance" ? Tint : Colors.White)
                                .Border(1).BorderColor(Border).Padding(8).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(8).FontColor(Muted);
                                    c.Item().Text($"Rs {value:N2}").Bold().FontColor(colour);
                                });
                        }
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(72);
                            c.ConstantColumn(78);
                            c.RelativeColumn(3);
                            c.ConstantColumn(62);
                            c.ConstantColumn(70);
                            c.ConstantColumn(72);
                        });
                        table.Header(h =>
                        {
                            foreach (var (text, right) in new[] { ("Txn", false), ("Date (IST)", false), ("Narration", false),
                                         ("Category", false), ("Amount", true), ("Balance", true) })
                            {
                                var cell = h.Cell().Background(Black).PaddingVertical(6).PaddingHorizontal(5);
                                (right ? cell.AlignRight() : cell).Text(text).Bold().FontSize(8.5f).FontColor(Gold);
                            }
                        });
                        if (m.Lines.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Padding(12).AlignCenter()
                                .Text("No transactions in this period.").FontColor(Muted);
                        }
                        foreach (var l in m.Lines)
                        {
                            static IContainer Body(IContainer x) => x.BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(5);
                            table.Cell().Element(Body).Text(l.Number).FontSize(8.5f);
                            table.Cell().Element(Body).Text(IstClock.ToIst(l.Utc).ToString("dd MMM yyyy HH:mm")).FontSize(8.5f);
                            table.Cell().Element(Body).Text(l.Narration).FontSize(8.5f);
                            table.Cell().Element(Body).Text(CategoryLabel(l.Category)).FontSize(8.5f).FontColor(Muted);
                            table.Cell().Element(Body).AlignRight()
                                .Text($"{(l.Direction == WalletDirection.Credit ? "+" : "−")} {l.Amount:N2}")
                                .FontSize(8.5f).FontColor(l.Direction == WalletDirection.Credit ? Green : Red);
                            table.Cell().Element(Body).AlignRight().Text($"{l.BalanceAfter:N2}").FontSize(8.5f);
                        }
                    });

                    col.Item().Background(Tint).Padding(8).Text(t =>
                    {
                        t.Span("Opening ").FontColor(Muted);
                        t.Span($"Rs {m.Opening:N2}").Bold();
                        t.Span("  +  credits ").FontColor(Muted);
                        t.Span($"Rs {credits:N2}").Bold().FontColor(Green);
                        t.Span("  −  debits ").FontColor(Muted);
                        t.Span($"Rs {debits:N2}").Bold().FontColor(Red);
                        t.Span("  =  closing ").FontColor(Muted);
                        t.Span($"Rs {m.Closing:N2}").Bold();
                    });
                    col.Item().Text("The wallet ledger is append-only: entries are never edited or deleted; corrections appear as adjustment entries.")
                        .FontSize(8).FontColor(Muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.Span($"Generated {IstClock.ToIst(m.GeneratedUtc):dd MMM yyyy, HH:mm} IST · Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
