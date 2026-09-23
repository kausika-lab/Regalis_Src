using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>The few things the printed quotation needs that the row does not carry:
/// the salesperson's name and the portal link the customer approves through.</summary>
public record QuotationPdfContext(
    string? SalespersonName = null,
    string? PortalLink = null,
    string? CompanyEmail = null,
    string? CompanyPhone = null,
    // StatusOverride: the status to print. The copy frozen on send is rendered a
    // moment before the row flips to Sent, and it must not reach the customer
    // stamped DRAFT; everywhere else this is null and the row's own status is used.
    QuotationStatus? StatusOverride = null);

/// <summary>
/// Template "Regalis Standard – India GST" (document §3.8). The same A4 document
/// serves the admin preview, the download, the email attachment and the customer
/// portal, generated from the snapshot taken when the quotation was sent — the copy
/// the customer downloads today is the one they were quoted, whatever the settings or
/// their profile say now. Deliberately mirrors InvoicePdfGenerator so the quotation and
/// the invoice it becomes look like the same document.
/// </summary>
public static class QuotationPdfGenerator
{
    private const string Gold = "#CC9A3D";
    private const string GoldDeep = "#B8842B";
    private const string Black = "#0B0B0D";
    private const string Ink = "#241F19";
    private const string Muted = "#8C8272";
    private const string Border = "#EEE4D2";

    static QuotationPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(Quotation quotation) =>
        Generate(quotation, new QuotationPdfContext());

    public static byte[] Generate(Quotation quotation, QuotationPdfContext context)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Ink));

                // ── Letterhead ──────────────────────────────────────────────
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(company =>
                    {
                        company.Item().Text(quotation.CompanyName).Bold().FontSize(17)
                            .FontColor(Black);
                        if (!string.IsNullOrWhiteSpace(quotation.CompanyAddress))
                        {
                            company.Item().Text(quotation.CompanyAddress).FontColor(Muted);
                        }
                        if (!string.IsNullOrWhiteSpace(quotation.CompanyGstin))
                        {
                            company.Item().Text($"GSTIN: {quotation.CompanyGstin}").FontColor(Muted);
                        }
                        var contact = string.Join(" · ", new[] { context.CompanyPhone, context.CompanyEmail }
                            .Where(part => !string.IsNullOrWhiteSpace(part)));
                        if (!string.IsNullOrWhiteSpace(contact))
                        {
                            company.Item().Text(contact).FontColor(Muted);
                        }
                    });
                    row.ConstantItem(200).Column(meta =>
                    {
                        meta.Item().AlignRight().Text("QUOTATION").Bold().FontSize(20)
                            .FontColor(Gold).LetterSpacing(0.08f);
                        meta.Item().AlignRight().Text(quotation.QuotationNumber).Bold().FontSize(11);
                        meta.Item().AlignRight().Text($"Date: {quotation.QuotationDate:dd MMM yyyy}")
                            .FontColor(Muted);
                        meta.Item().AlignRight()
                            .Text($"Valid until: {quotation.ValidUntil:dd MMM yyyy}").FontColor(Muted);
                        var printedStatus = context.StatusOverride ?? quotation.Status;
                        meta.Item().PaddingTop(2).AlignRight().Text(StatusLabel(printedStatus))
                            .Bold().FontSize(8.5f).FontColor(StatusColour(printedStatus));
                    });
                });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(12);

                    // ── "Quotation for" and "Details", side by side ─────────
                    column.Item().Row(boxes =>
                    {
                        boxes.Spacing(12);
                        boxes.RelativeItem().Border(1).BorderColor(Border).Padding(10)
                            .Column(to =>
                            {
                                to.Item().PaddingBottom(4).Text("Quotation for").Bold()
                                    .FontSize(8.5f).FontColor(GoldDeep);
                                to.Item().Text(quotation.CustomerName).Bold();
                                if (!string.IsNullOrWhiteSpace(quotation.CustomerCompanyName))
                                {
                                    to.Item().Text(quotation.CustomerCompanyName);
                                }
                                if (!string.IsNullOrWhiteSpace(quotation.BillingAddress))
                                {
                                    to.Item().Text(quotation.BillingAddress).FontColor(Muted);
                                }
                                if (!string.IsNullOrWhiteSpace(quotation.CustomerGstin))
                                {
                                    to.Item().Text($"GSTIN: {quotation.CustomerGstin}");
                                }
                                if (!string.IsNullOrWhiteSpace(quotation.CustomerMobile))
                                {
                                    to.Item().Text(quotation.CustomerMobile).FontColor(Muted);
                                }
                                if (!string.IsNullOrWhiteSpace(quotation.CustomerEmail))
                                {
                                    to.Item().Text(quotation.CustomerEmail).FontColor(Muted);
                                }
                            });
                        boxes.RelativeItem().Border(1).BorderColor(Border).Padding(10)
                            .Column(details =>
                            {
                                details.Item().PaddingBottom(4).Text("Details").Bold()
                                    .FontSize(8.5f).FontColor(GoldDeep);
                                DetailRow(details, "Place of supply",
                                    string.IsNullOrWhiteSpace(quotation.PlaceOfSupply)
                                        ? "—"
                                        : quotation.PlaceOfSupply);
                                DetailRow(details, "Reference", quotation.ReferenceNumber ?? "—");
                                DetailRow(details, "Salesperson", context.SalespersonName ?? "—");
                                DetailRow(details, "Tax",
                                    quotation.TaxMode == QuotationTaxMode.IntraState
                                        ? "CGST + SGST (intra-state)"
                                        : "IGST (inter-state)");
                            });
                    });

                    if (!string.IsNullOrWhiteSpace(quotation.Subject))
                    {
                        column.Item().Text(text =>
                        {
                            text.Span("Subject: ").Bold().FontColor(GoldDeep);
                            text.Span(quotation.Subject);
                        });
                    }

                    // ── Service table ───────────────────────────────────────
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);
                            columns.RelativeColumn(3.6f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(1.3f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#");
                            header.Cell().Element(HeaderCell).Text("Service");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Duration");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("GST");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });

                        foreach (var line in quotation.Lines.OrderBy(l => l.LineNumber))
                        {
                            // A header line is a caption spanning the table, not a row
                            // of figures (document §3.3).
                            if (line.LineType == QuotationLineType.Header)
                            {
                                table.Cell().ColumnSpan(7).Element(GroupCell)
                                    .Text(line.Description).Bold().FontColor(GoldDeep);
                                continue;
                            }

                            table.Cell().Element(BodyCell).Text(line.LineNumber.ToString());
                            table.Cell().Element(BodyCell).Column(cell =>
                            {
                                cell.Item().Text(line.Description);
                                if (!string.IsNullOrWhiteSpace(line.LineNotes))
                                {
                                    cell.Item().Text(line.LineNotes).FontSize(8).FontColor(Muted);
                                }
                                if (!string.IsNullOrWhiteSpace(line.HsnSacCode))
                                {
                                    cell.Item().Text($"SAC {line.HsnSacCode}").FontSize(8)
                                        .FontColor(Muted);
                                }
                            });
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text($"{line.Quantity:0.###}");
                            table.Cell().Element(BodyCell).AlignRight().Text(
                                line.DurationValue is null
                                    ? "—"
                                    : $"{line.DurationValue:0.###} {line.DurationUnit}".Trim());
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.UnitPrice:N2}");
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text($"{line.TaxPercent:0.##}%");
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text($"{line.LineAmount:N2}").Bold();
                        }
                    });

                    // ── Amount in words, beside the totals ──────────────────
                    column.Item().Row(footerRow =>
                    {
                        footerRow.Spacing(16);
                        footerRow.RelativeItem().Column(words =>
                        {
                            words.Item().Text("Amount in words").Bold().FontSize(8.5f)
                                .FontColor(GoldDeep);
                            words.Item().Text(IndianMoney.ToWords(quotation.TotalAmount))
                                .FontSize(9);
                        });
                        footerRow.ConstantItem(250).Column(totals =>
                        {
                            totals.Spacing(2);
                            TotalRow(totals, "Sub total", quotation.SubTotal);
                            if (quotation.DiscountAmount > 0)
                            {
                                var label = quotation.DiscountType
                                            == QuotationDiscountType.Percentage
                                    ? $"Discount ({quotation.DiscountValue:0.##}%)"
                                    : "Discount";
                                TotalRow(totals, label, -quotation.DiscountAmount);
                            }
                            if (quotation.TaxMode == QuotationTaxMode.IntraState)
                            {
                                TotalRow(totals, $"CGST @ {quotation.CgstPercent:0.##}%",
                                    quotation.CgstAmount);
                                TotalRow(totals, $"SGST @ {quotation.SgstPercent:0.##}%",
                                    quotation.SgstAmount);
                            }
                            else
                            {
                                TotalRow(totals, $"IGST @ {quotation.IgstPercent:0.##}%",
                                    quotation.IgstAmount);
                            }
                            if (quotation.TdsAmount > 0)
                            {
                                TotalRow(totals,
                                    quotation.TdsLabel ?? $"TDS @ {quotation.TdsPercent:0.##}%",
                                    -quotation.TdsAmount);
                            }
                            if (quotation.AdjustmentAmount != 0)
                            {
                                TotalRow(totals, quotation.AdjustmentLabel ?? "Adjustment",
                                    quotation.AdjustmentAmount);
                            }
                            if (quotation.RoundOffAmount != 0)
                            {
                                TotalRow(totals, "Round off", quotation.RoundOffAmount);
                            }
                            // The grand total sits in a black band, as the document asks.
                            totals.Item().PaddingTop(4).Background(Black).Padding(8).Row(row =>
                            {
                                row.RelativeItem().Text("TOTAL").Bold()
                                    .FontColor(Colors.White).FontSize(11);
                                row.ConstantItem(120).AlignRight()
                                    .Text($"Rs {quotation.TotalAmount:N2}").Bold()
                                    .FontColor(Gold).FontSize(11);
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(quotation.Remarks))
                    {
                        column.Item().Column(notes =>
                        {
                            notes.Item().Text("Notes").Bold().FontSize(8.5f).FontColor(GoldDeep);
                            notes.Item().Text(quotation.Remarks).FontSize(9);
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(quotation.TermsAndConditions))
                    {
                        column.Item().Column(terms =>
                        {
                            terms.Item().Text("Terms & conditions").Bold().FontSize(8.5f)
                                .FontColor(GoldDeep);
                            terms.Item().Text(quotation.TermsAndConditions).FontSize(8.5f)
                                .FontColor(Muted);
                        });
                    }

                    column.Item().PaddingTop(6).Row(close =>
                    {
                        close.RelativeItem().Column(link =>
                        {
                            if (!string.IsNullOrWhiteSpace(context.PortalLink))
                            {
                                link.Item().Text("View & approve online").Bold().FontSize(8.5f)
                                    .FontColor(GoldDeep);
                                link.Item().Hyperlink(context.PortalLink)
                                    .Text(context.PortalLink).FontSize(8).FontColor(Muted);
                            }
                        });
                        close.ConstantItem(200).AlignRight().Column(sign =>
                        {
                            sign.Item().PaddingTop(18).BorderTop(1).BorderColor(Border)
                                .PaddingTop(4).AlignRight()
                                .Text($"For {quotation.CompanyName}").FontSize(8.5f);
                            sign.Item().AlignRight().Text("Authorised signatory").FontSize(8)
                                .FontColor(Muted);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span(" · This is a computer-generated quotation and needs no signature.");
                });
            });
        }).GeneratePdf();

        static IContainer HeaderCell(IContainer container) =>
            container.Background(Black).PaddingVertical(6).PaddingHorizontal(6)
                .DefaultTextStyle(style => style.FontColor(Gold).Bold().FontSize(9));

        static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(6);

        static IContainer GroupCell(IContainer container) =>
            container.Background("#FBF6EC").BorderBottom(1).BorderColor(Border)
                .PaddingVertical(5).PaddingHorizontal(6);

        static void DetailRow(ColumnDescriptor details, string label, string value) =>
            details.Item().Row(row =>
            {
                row.ConstantItem(92).Text(label).FontSize(8.5f).FontColor(Muted);
                row.RelativeItem().Text(value).FontSize(9);
            });

        static void TotalRow(ColumnDescriptor totals, string label, decimal amount) =>
            totals.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                row.ConstantItem(120).AlignRight().Text($"Rs {amount:N2}");
            });
    }

    /// <summary>What the customer should read on the document. Sent and Viewed both
    /// say "awaiting approval": the difference between them is the admin's business,
    /// not the customer's (document §3.7).</summary>
    private static string StatusLabel(QuotationStatus status) => status switch
    {
        QuotationStatus.Draft => "DRAFT",
        QuotationStatus.Sent or QuotationStatus.Viewed => "AWAITING APPROVAL",
        QuotationStatus.Approved => "APPROVED",
        QuotationStatus.Rejected => "REJECTED",
        QuotationStatus.Expired => "EXPIRED",
        QuotationStatus.Revised => "REPLACED",
        _ => "CANCELLED"
    };

    private static string StatusColour(QuotationStatus status) => status switch
    {
        QuotationStatus.Approved => Colors.Green.Darken2,
        QuotationStatus.Rejected or QuotationStatus.Cancelled => Colors.Red.Darken2,
        QuotationStatus.Expired or QuotationStatus.Revised => Colors.Grey.Darken2,
        _ => GoldDeep
    };
}
