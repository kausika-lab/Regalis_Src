using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Reporting;

/// <summary>The GST tax invoice as a PDF (QuestPDF, community license), generated on
/// demand from the snapshotted invoice — later profile or settings edits never
/// change what a customer already received.</summary>
public static class InvoicePdfGenerator
{
    private const string Gold = "#C9A227";
    private const string Black = "#0B0B0D";

    static InvoicePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(Invoice invoice)
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
                        column.Item().Text(invoice.CompanyName).Bold().FontSize(18).FontColor(Black);
                        if (!string.IsNullOrWhiteSpace(invoice.CompanyAddress))
                        {
                            column.Item().Text(invoice.CompanyAddress).FontColor(Colors.Grey.Darken1);
                        }
                        if (!string.IsNullOrWhiteSpace(invoice.CompanyGstin))
                        {
                            column.Item().Text($"GSTIN: {invoice.CompanyGstin}").FontColor(Colors.Grey.Darken1);
                        }
                    });
                    row.ConstantItem(180).Column(column =>
                    {
                        column.Item().AlignRight().Text("TAX INVOICE").Bold().FontSize(14).FontColor(Gold);
                        column.Item().AlignRight().Text(invoice.InvoiceNumber).Bold();
                        column.Item().AlignRight().Text($"Date: {invoice.InvoiceDate:dd MMM yyyy}");
                        column.Item().AlignRight().Text($"Due: {invoice.DueDate:dd MMM yyyy}");
                        column.Item().AlignRight().Text(StatusLabel(invoice.Status)).Bold()
                            .FontColor(invoice.Status == InvoiceStatus.Paid ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                    });
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(billTo =>
                    {
                        billTo.Item().Text("Billed to").Bold().FontColor(Colors.Grey.Darken2);
                        billTo.Item().Text(invoice.CustomerName).Bold();
                        billTo.Item().Text(invoice.CustomerEmail);
                        if (!string.IsNullOrWhiteSpace(invoice.CustomerMobile))
                        {
                            billTo.Item().Text(invoice.CustomerMobile);
                        }
                        if (!string.IsNullOrWhiteSpace(invoice.BillingAddress))
                        {
                            billTo.Item().Text(invoice.BillingAddress);
                        }
                        if (!string.IsNullOrWhiteSpace(invoice.CustomerGstin))
                        {
                            billTo.Item().Text($"GSTIN: {invoice.CustomerGstin}");
                        }
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.4f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#");
                            header.Cell().Element(HeaderCell).Text("Description");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty (h)");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });
                        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
                        {
                            table.Cell().Element(BodyCell).Text(line.LineNumber.ToString());
                            table.Cell().Element(BodyCell).Text(line.Description);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.Quantity:0.###}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.UnitPrice:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.LineAmount:N2}");
                        }
                    });

                    column.Item().AlignRight().Width(240).Column(totals =>
                    {
                        totals.Spacing(2);
                        TotalRow(totals, "Sub-total", invoice.SubTotal);
                        if (invoice.DiscountAmount > 0)
                        {
                            TotalRow(totals, "Discount", -invoice.DiscountAmount);
                        }
                        TotalRow(totals, $"CGST @ {invoice.CgstPercent:0.##}%", invoice.CgstAmount);
                        TotalRow(totals, $"SGST @ {invoice.SgstPercent:0.##}%", invoice.SgstAmount);
                        if (invoice.IgstAmount > 0)
                        {
                            TotalRow(totals, $"IGST @ {invoice.IgstPercent:0.##}%", invoice.IgstAmount);
                        }
                        if (invoice.TdsAmount > 0)
                        {
                            TotalRow(totals,
                                invoice.TdsLabel ?? $"TDS @ {invoice.TdsPercent:0.##}%",
                                -invoice.TdsAmount);
                        }
                        if (invoice.AdjustmentAmount != 0)
                        {
                            TotalRow(totals, invoice.AdjustmentLabel ?? "Adjustment",
                                invoice.AdjustmentAmount);
                        }
                        if (invoice.RoundOffAmount != 0)
                        {
                            TotalRow(totals, "Round off", invoice.RoundOffAmount);
                        }
                        totals.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Total").Bold();
                            row.ConstantItem(110).AlignRight().Text($"₹ {invoice.TotalAmount:N2}").Bold();
                        });
                        TotalRow(totals, "Paid", invoice.PaidAmount);
                        if (invoice.OutstandingAmount != 0)
                        {
                            TotalRow(totals, "Outstanding", invoice.OutstandingAmount);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Computer-generated invoice · ").FontColor(Colors.Grey.Darken1);
                    text.Span(invoice.CompanyName).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        static QuestPDF.Infrastructure.IContainer HeaderCell(QuestPDF.Infrastructure.IContainer container) =>
            container.Background(Black).Padding(6).DefaultTextStyle(s => s.FontColor(Colors.White).Bold());

        static QuestPDF.Infrastructure.IContainer BodyCell(QuestPDF.Infrastructure.IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

        static void TotalRow(ColumnDescriptor totals, string label, decimal amount) =>
            totals.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken2);
                row.ConstantItem(110).AlignRight().Text($"₹ {amount:N2}");
            });
    }

    private static string StatusLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Paid => "PAID",
        InvoiceStatus.PartiallyPaid => "PARTIALLY PAID",
        InvoiceStatus.Cancelled => "CANCELLED",
        InvoiceStatus.Refunded => "REFUNDED",
        _ => "PENDING"
    };
}
