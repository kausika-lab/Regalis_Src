using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Reporting;

/// <summary>The customer-facing quotation as a PDF (QuestPDF, community license),
/// generated on demand from the snapshot taken when the quotation was sent — the copy
/// the customer downloads today is the one they were quoted, whatever the settings or
/// their profile say now. Deliberately mirrors InvoicePdfGenerator so the quotation and
/// the invoice it becomes look like the same document.</summary>
public static class QuotationPdfGenerator
{
    private const string Gold = "#C9A227";
    private const string Black = "#0B0B0D";

    static QuotationPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(Quotation quotation)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(quotation.CompanyName).Bold().FontSize(18).FontColor(Black);
                        if (!string.IsNullOrWhiteSpace(quotation.CompanyAddress))
                        {
                            column.Item().Text(quotation.CompanyAddress).FontColor(Colors.Grey.Darken1);
                        }
                        if (!string.IsNullOrWhiteSpace(quotation.CompanyGstin))
                        {
                            column.Item().Text($"GSTIN: {quotation.CompanyGstin}")
                                .FontColor(Colors.Grey.Darken1);
                        }
                    });
                    row.ConstantItem(180).Column(column =>
                    {
                        column.Item().AlignRight().Text("QUOTATION").Bold().FontSize(14).FontColor(Gold);
                        column.Item().AlignRight().Text(quotation.QuotationNumber).Bold();
                        column.Item().AlignRight().Text($"Date: {quotation.QuotationDate:dd MMM yyyy}");
                        column.Item().AlignRight().Text($"Valid until: {quotation.ValidUntil:dd MMM yyyy}");
                        column.Item().AlignRight().Text(StatusLabel(quotation.Status)).Bold()
                            .FontColor(StatusColour(quotation.Status));
                    });
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(quoteTo =>
                    {
                        quoteTo.Item().Text("Quotation for").Bold().FontColor(Colors.Grey.Darken2);
                        quoteTo.Item().Text(quotation.CustomerName).Bold();
                        quoteTo.Item().Text(quotation.CustomerEmail);
                        if (!string.IsNullOrWhiteSpace(quotation.CustomerMobile))
                        {
                            quoteTo.Item().Text(quotation.CustomerMobile);
                        }
                        if (!string.IsNullOrWhiteSpace(quotation.BillingAddress))
                        {
                            quoteTo.Item().Text(quotation.BillingAddress);
                        }
                        if (!string.IsNullOrWhiteSpace(quotation.CustomerGstin))
                        {
                            quoteTo.Item().Text($"GSTIN: {quotation.CustomerGstin}");
                        }
                        if (!string.IsNullOrWhiteSpace(quotation.PlaceOfSupply))
                        {
                            quoteTo.Item().Text($"Place of supply: {quotation.PlaceOfSupply}");
                        }
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.4f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#");
                            header.Cell().Element(HeaderCell).Text("Description");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Duration");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });
                        foreach (var line in quotation.Lines.OrderBy(l => l.LineNumber))
                        {
                            table.Cell().Element(BodyCell).Text(line.LineNumber.ToString());
                            table.Cell().Element(BodyCell).Text(line.Description);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.Quantity:0.###}");
                            table.Cell().Element(BodyCell).AlignRight().Text(
                                line.DurationValue is null
                                    ? "—"
                                    : $"{line.DurationValue:0.###} {line.DurationUnit}".Trim());
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.UnitPrice:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.LineAmount:N2}");
                        }
                    });

                    column.Item().AlignRight().Width(240).Column(totals =>
                    {
                        totals.Spacing(2);
                        TotalRow(totals, "Sub-total", quotation.SubTotal);
                        if (quotation.DiscountAmount > 0)
                        {
                            TotalRow(totals, "Discount", -quotation.DiscountAmount);
                        }
                        TotalRow(totals, $"CGST @ {quotation.CgstPercent:0.##}%", quotation.CgstAmount);
                        TotalRow(totals, $"SGST @ {quotation.SgstPercent:0.##}%", quotation.SgstAmount);
                        if (quotation.IgstAmount > 0)
                        {
                            TotalRow(totals, $"IGST @ {quotation.IgstPercent:0.##}%", quotation.IgstAmount);
                        }
                        totals.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Total").Bold();
                            row.ConstantItem(110).AlignRight().Text($"₹ {quotation.TotalAmount:N2}").Bold();
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(quotation.TermsAndConditions))
                    {
                        column.Item().PaddingTop(8).Column(terms =>
                        {
                            terms.Item().Text("Terms & conditions").Bold().FontColor(Colors.Grey.Darken2);
                            terms.Item().Text(quotation.TermsAndConditions).FontSize(9);
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(quotation.Remarks))
                    {
                        column.Item().Text($"Remarks: {quotation.Remarks}").FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("This is a quotation, not a tax invoice · ")
                        .FontColor(Colors.Grey.Darken1);
                    text.Span(quotation.CompanyName).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        static IContainer HeaderCell(IContainer container) =>
            container.Background(Black).Padding(6).DefaultTextStyle(s => s.FontColor(Colors.White).Bold());

        static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

        static void TotalRow(ColumnDescriptor totals, string label, decimal amount) =>
            totals.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken2);
                row.ConstantItem(110).AlignRight().Text($"₹ {amount:N2}");
            });
    }

    private static string StatusLabel(QuotationStatus status) => status switch
    {
        QuotationStatus.Draft => "DRAFT",
        QuotationStatus.Sent => "AWAITING APPROVAL",
        QuotationStatus.Viewed => "AWAITING APPROVAL",
        QuotationStatus.Approved => "APPROVED",
        QuotationStatus.Rejected => "REJECTED",
        QuotationStatus.Expired => "EXPIRED",
        _ => "CANCELLED"
    };

    private static string StatusColour(QuotationStatus status) => status switch
    {
        QuotationStatus.Approved => Colors.Green.Darken2,
        QuotationStatus.Rejected or QuotationStatus.Cancelled => Colors.Red.Darken2,
        QuotationStatus.Expired => Colors.Grey.Darken2,
        _ => Colors.Orange.Darken2
    };
}
