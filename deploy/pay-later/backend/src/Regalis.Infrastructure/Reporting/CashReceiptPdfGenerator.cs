using System;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>The front-desk payment receipt for a pay-later booking (A5, QuestPDF community
/// license), in the invoice's colours. A voided receipt prints with a VOID stamp and the
/// reason, so a reprint can never pass for a valid one.</summary>
public static class CashReceiptPdfGenerator
{
    private const string Accent = "#2E75B6";
    private const string Rule = "#D9DEE3";
    private const string Muted = "#6B7280";
    private const string Ink = "#1F2328";
    private const string Void = "#B42318";

    public record Receipt(
        string ReceiptNumber,
        DateTime CollectedUtc,
        string CustomerName,
        string CustomerMobile,
        string BookingNumber,
        string WorkspaceName,
        DateTime StartUtc,
        DateTime EndUtc,
        string? InvoiceNumber,
        string? PaymentNumber,
        string Method,
        decimal Amount,
        decimal? AmountTendered,
        decimal? ChangeReturned,
        string? ReferenceNumber,
        decimal BookingTotal,
        decimal BookingPaid,
        string CollectedBy,
        bool IsVoided,
        string? VoidReason,
        string CompanyName,
        string CompanyAddress,
        string CompanyGstin);

    static CashReceiptPdfGenerator()
    {
        Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(Receipt r, byte[]? logo)
    {
        DateTime collectedIst = IstClock.ToIst(r.CollectedUtc);
        DateTime startIst = IstClock.ToIst(r.StartUtc);
        DateTime endIst = IstClock.ToIst(r.EndUtc);
        string period = startIst.Date == endIst.AddTicks(-1).Date
            ? $"{startIst:dd MMM yyyy}, {startIst:HH:mm}-{endIst:HH:mm} IST"
            : $"{startIst:dd MMM yyyy} to {endIst:dd MMM yyyy}";
        decimal balance = r.BookingTotal - r.BookingPaid;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(s => s.FontSize(9).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().AlignMiddle().Element(e =>
                        {
                            if (logo is { Length: > 0 })
                            {
                                e.Height(44).Image(logo).FitArea();
                            }
                            else
                            {
                                e.Text(r.CompanyName).Bold().FontSize(13);
                            }
                        });
                        row.RelativeItem().AlignRight().Column(company =>
                        {
                            company.Item().AlignRight().Text(r.CompanyName).Bold().FontSize(9.5f);
                            if (!string.IsNullOrWhiteSpace(r.CompanyAddress))
                            {
                                company.Item().AlignRight().Text(r.CompanyAddress).FontSize(7.5f).FontColor(Muted);
                            }
                            if (!string.IsNullOrWhiteSpace(r.CompanyGstin))
                            {
                                company.Item().AlignRight().Text("GSTIN " + r.CompanyGstin).FontSize(7.5f).FontColor(Muted);
                            }
                        });
                    });
                    header.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().LineHorizontal(1).LineColor(Rule);
                        row.AutoItem().PaddingHorizontal(10).Text(r.IsVoided ? "PAYMENT RECEIPT - VOID" : "PAYMENT RECEIPT")
                            .FontSize(12).FontColor(r.IsVoided ? Void : Accent);
                        row.RelativeItem().AlignMiddle().LineHorizontal(1).LineColor(Rule);
                    });
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Received from").FontColor(Muted).FontSize(7.5f);
                            c.Item().Text(r.CustomerName).Bold();
                            c.Item().Text(r.CustomerMobile);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            Fact(c, "Receipt", r.ReceiptNumber);
                            Fact(c, "Date", $"{collectedIst:dd MMM yyyy, HH:mm} IST");
                            if (!string.IsNullOrWhiteSpace(r.PaymentNumber))
                            {
                                Fact(c, "Payment", r.PaymentNumber);
                            }
                        });
                    });

                    column.Item().Border(1).BorderColor(Rule).Padding(10).Column(c =>
                    {
                        c.Spacing(4);
                        Line(c, "Booking", r.BookingNumber);
                        Line(c, "Workspace", r.WorkspaceName);
                        Line(c, "When", period);
                        if (!string.IsNullOrWhiteSpace(r.InvoiceNumber))
                        {
                            Line(c, "Invoice", r.InvoiceNumber);
                        }
                        Line(c, "Paid by", r.Method + (string.IsNullOrWhiteSpace(r.ReferenceNumber) ? "" : $" (ref. {r.ReferenceNumber})"));
                        if (r.AmountTendered.HasValue)
                        {
                            Line(c, "Cash handed over", $"₹{r.AmountTendered.Value:N2}");
                            Line(c, "Change returned", $"₹{r.ChangeReturned ?? 0m:N2}");
                        }
                    });

                    column.Item().Background("#EAF4FB").Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Amount received").FontColor(Muted).FontSize(7.5f);
                            c.Item().Text($"₹{r.Amount:N2}").Bold().FontSize(15).FontColor(r.IsVoided ? Void : Ink);
                            c.Item().Text(IndianMoney.ToWords(r.Amount)).FontSize(7.5f).FontColor(Muted);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            Fact(c, "Booking total", $"₹{r.BookingTotal:N2}");
                            Fact(c, "Paid so far", $"₹{r.BookingPaid:N2}");
                            Fact(c, "Balance due", $"₹{Math.Max(0m, balance):N2}");
                        });
                    });

                    if (r.IsVoided)
                    {
                        column.Item().Text($"This receipt is void: {r.VoidReason}").FontColor(Void).Bold();
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Received by {r.CollectedBy}").FontSize(7.5f).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text("Computer-generated receipt; no signature needed.").FontSize(7.5f).FontColor(Muted);
                });
            });
        }).GeneratePdf();
    }

    private static void Fact(ColumnDescriptor c, string label, string value)
    {
        c.Item().AlignRight().Text(t =>
        {
            t.Span(label + "  ").FontColor(Muted).FontSize(7.5f);
            t.Span(value).SemiBold();
        });
    }

    private static void Line(ColumnDescriptor c, string label, string value)
    {
        c.Item().Row(row =>
        {
            row.ConstantItem(90).Text(label).FontColor(Muted);
            row.RelativeItem().Text(value);
        });
    }
}
