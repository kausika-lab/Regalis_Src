using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>What the printed PO needs beyond its own row: the company letterhead (from
/// the Company.* settings), the delivery location's name, the approver and an optional
/// logo (Company.LogoPath, PNG/JPG).</summary>
public record PurchaseOrderPdfContext(
    string CompanyName,
    string? CompanyAddress = null,
    string? CompanyGstin = null,
    string? CompanyPhone = null,
    string? CompanyEmail = null,
    string? LocationName = null,
    string? ApprovedByName = null,
    byte[]? Logo = null);

/// <summary>
/// The purchase order as sent to the supplier — A4, the same letterhead, palette and
/// table style as QuotationPdfGenerator and InvoicePdfGenerator, so every document
/// Regalis issues looks like it came from the same place.
/// </summary>
public static class PurchaseOrderPdfGenerator
{
    private const string Gold = "#CC9A3D";
    private const string GoldDeep = "#B8842B";
    private const string Black = "#0B0B0D";
    private const string Ink = "#241F19";
    private const string Muted = "#8C8272";
    private const string Border = "#EEE4D2";
    private const string Tint = "#FBF6EC";

    static PurchaseOrderPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(PurchaseOrder order, PurchaseOrderPdfContext context)
    {
        var supplier = order.Supplier;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Ink));

                // ── Letterhead ──────────────────────────────────────────────
                page.Header().Column(head =>
                {
                    head.Item().Row(row =>
                    {
                        row.RelativeItem().Row(brand =>
                        {
                            if (context.Logo is { Length: > 0 })
                            {
                                brand.ConstantItem(46).Height(46).Image(context.Logo).FitArea();
                            }
                            else
                            {
                                // The app's mark: a gold R on a black tile.
                                brand.ConstantItem(40).Height(40).Background(Black)
                                    .AlignCenter().AlignMiddle()
                                    .Text("R").Bold().FontSize(22).FontColor(Gold);
                            }
                            brand.ConstantItem(10);
                            brand.RelativeItem().Column(company =>
                            {
                                company.Item().Text(context.CompanyName).Bold().FontSize(16)
                                    .FontColor(Black);
                                if (!string.IsNullOrWhiteSpace(context.CompanyAddress))
                                {
                                    company.Item().Text(context.CompanyAddress).FontColor(Muted);
                                }
                                if (!string.IsNullOrWhiteSpace(context.CompanyGstin))
                                {
                                    company.Item().Text($"GSTIN: {context.CompanyGstin}").FontColor(Muted);
                                }
                                var contact = string.Join(" · ", new[] { context.CompanyPhone, context.CompanyEmail }
                                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                                if (!string.IsNullOrWhiteSpace(contact))
                                {
                                    company.Item().Text(contact).FontColor(Muted);
                                }
                            });
                        });
                        row.ConstantItem(200).Column(meta =>
                        {
                            meta.Item().AlignRight().Text("PURCHASE ORDER").Bold().FontSize(17)
                                .FontColor(Gold).LetterSpacing(0.06f);
                            meta.Item().AlignRight().Text(order.PoNumber).Bold().FontSize(11);
                            meta.Item().AlignRight().Text($"Date: {order.PoDate:dd MMM yyyy}")
                                .FontColor(Muted);
                            if (order.ExpectedDeliveryDate is not null)
                            {
                                meta.Item().AlignRight()
                                    .Text($"Deliver by: {order.ExpectedDeliveryDate:dd MMM yyyy}")
                                    .FontColor(Muted);
                            }
                            meta.Item().PaddingTop(2).AlignRight().Text(StatusLabel(order.Status))
                                .Bold().FontSize(8.5f).FontColor(StatusColour(order.Status));
                        });
                    });
                    head.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Gold);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Spacing(12);

                    // ── Supplier, delivery and details ──────────────────────
                    column.Item().Row(boxes =>
                    {
                        boxes.Spacing(10);
                        boxes.RelativeItem().Border(1).BorderColor(Border).Padding(9).Column(to =>
                        {
                            Caption(to, "Supplier");
                            to.Item().Text(supplier.SupplierName).Bold();
                            var address = string.Join(", ", new[]
                            {
                                supplier.AddressLine, supplier.City,
                                string.Join(" ", new[] { supplier.State, supplier.Pincode }
                                    .Where(p => !string.IsNullOrWhiteSpace(p)))
                            }.Where(p => !string.IsNullOrWhiteSpace(p)));
                            if (!string.IsNullOrWhiteSpace(address))
                            {
                                to.Item().Text(address).FontColor(Muted);
                            }
                            if (!string.IsNullOrWhiteSpace(supplier.Gstin))
                            {
                                to.Item().Text($"GSTIN: {supplier.Gstin}");
                            }
                            var contact = string.Join(" · ", new[] { supplier.MobileNumber, supplier.Email }
                                .Where(p => !string.IsNullOrWhiteSpace(p)));
                            if (!string.IsNullOrWhiteSpace(contact))
                            {
                                to.Item().Text(contact).FontColor(Muted);
                            }
                        });
                        boxes.RelativeItem().Border(1).BorderColor(Border).Padding(9).Column(ship =>
                        {
                            Caption(ship, "Deliver to");
                            ship.Item().Text(context.CompanyName).Bold();
                            if (!string.IsNullOrWhiteSpace(context.LocationName))
                            {
                                ship.Item().Text(context.LocationName);
                            }
                            if (!string.IsNullOrWhiteSpace(order.DeliveryAddress))
                            {
                                ship.Item().Text(order.DeliveryAddress).FontColor(Muted);
                            }
                        });
                        boxes.ConstantItem(170).Border(1).BorderColor(Border).Padding(9).Column(details =>
                        {
                            Caption(details, "Details");
                            DetailRow(details, "Reference", order.ReferenceNumber ?? "—");
                            DetailRow(details, "Shipment", order.ShipmentPreference ?? "—");
                            DetailRow(details, "Tax", order.IsInterState ? "IGST (inter-state)" : "CGST + SGST");
                            if (order.IsReverseCharge)
                            {
                                DetailRow(details, "Reverse charge", "Yes");
                            }
                        });
                    });

                    // ── Item table ──────────────────────────────────────────
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);
                            columns.RelativeColumn(3.6f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.3f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#");
                            header.Cell().Element(HeaderCell).Text("Item & description");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("GST");
                            header.Cell().Element(HeaderCell).AlignRight().Text("GST amt");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });

                        foreach (var line in order.Lines.OrderBy(l => l.LineNumber))
                        {
                            table.Cell().Element(BodyCell).Text(line.LineNumber.ToString());
                            table.Cell().Element(BodyCell).Column(cell =>
                            {
                                cell.Item().Text(line.ItemName).SemiBold();
                                if (!string.IsNullOrWhiteSpace(line.Remarks))
                                {
                                    cell.Item().Text(line.Remarks).FontSize(8).FontColor(Muted);
                                }
                                var meta = string.Join(" · ", new[]
                                {
                                    string.IsNullOrWhiteSpace(line.HsnSacCode) ? null : $"HSN/SAC {line.HsnSacCode}",
                                    line.AccountCategory
                                }.Where(p => !string.IsNullOrWhiteSpace(p)));
                                if (!string.IsNullOrWhiteSpace(meta))
                                {
                                    cell.Item().Text(meta).FontSize(8).FontColor(Muted);
                                }
                            });
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text($"{line.Quantity:0.###} {line.Unit}".Trim());
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.Rate:N2}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.TaxPercent:0.##}%");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.TaxAmount:N2}");
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text($"{line.Quantity * line.Rate:N2}").SemiBold();
                        }
                    });

                    // ── Amount in words, beside the totals ──────────────────
                    column.Item().Row(footerRow =>
                    {
                        footerRow.Spacing(16);
                        footerRow.RelativeItem().Column(words =>
                        {
                            words.Item().Text("Amount in words").Bold().FontSize(8.5f).FontColor(GoldDeep);
                            words.Item().Text(IndianMoney.ToWords(order.TotalAmount)).FontSize(9);
                            if (order.IsReverseCharge)
                            {
                                words.Item().PaddingTop(6).Text(
                                        "Reverse charge applies: GST on this order is payable by the " +
                                        "buyer to the government and is not part of the amount payable " +
                                        "to the supplier.")
                                    .FontSize(8).FontColor(Muted);
                            }
                        });
                        footerRow.ConstantItem(250).Column(totals =>
                        {
                            totals.Spacing(2);
                            TotalRow(totals, "Sub total", order.SubTotal);
                            if (order.DiscountAmount > 0)
                            {
                                TotalRow(totals, order.DiscountType == PurchaseOrderDiscountType.Percentage
                                    ? $"Discount ({order.DiscountValue:0.##}%)"
                                    : "Discount", -order.DiscountAmount);
                            }
                            var gstNote = order.IsReverseCharge ? " (RCM)" : "";
                            if (order.IsInterState)
                            {
                                TotalRow(totals, $"IGST{gstNote}", order.IgstAmount);
                            }
                            else
                            {
                                TotalRow(totals, $"CGST{gstNote}", order.CgstAmount);
                                TotalRow(totals, $"SGST{gstNote}", order.SgstAmount);
                            }
                            if (order.TdsAmount > 0)
                            {
                                TotalRow(totals, order.TdsLabel ?? $"TDS @ {order.TdsPercent:0.##}%",
                                    -order.TdsAmount);
                            }
                            if (order.AdjustmentAmount != 0)
                            {
                                TotalRow(totals, order.AdjustmentLabel ?? "Adjustment", order.AdjustmentAmount);
                            }
                            totals.Item().PaddingTop(4).Background(Black).Padding(8).Row(row =>
                            {
                                row.RelativeItem().Text("TOTAL").Bold().FontColor(Colors.White).FontSize(11);
                                row.ConstantItem(130).AlignRight()
                                    .Text($"Rs {order.TotalAmount:N2}").Bold().FontColor(Gold).FontSize(11);
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(order.Remarks))
                    {
                        column.Item().Background(Tint).Padding(8).Column(notes =>
                        {
                            notes.Item().Text("Notes").Bold().FontSize(8.5f).FontColor(GoldDeep);
                            notes.Item().Text(order.Remarks).FontSize(9);
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(order.TermsAndConditions))
                    {
                        column.Item().Column(terms =>
                        {
                            terms.Item().Text("Terms & conditions").Bold().FontSize(8.5f).FontColor(GoldDeep);
                            terms.Item().Text(order.TermsAndConditions).FontSize(8.5f).FontColor(Muted);
                        });
                    }

                    // ── Signatory ───────────────────────────────────────────
                    column.Item().PaddingTop(10).Row(close =>
                    {
                        close.RelativeItem().Column(approval =>
                        {
                            if (!string.IsNullOrWhiteSpace(context.ApprovedByName) && order.ApprovedUtc is not null)
                            {
                                approval.Item().Text("Approved by").Bold().FontSize(8.5f).FontColor(GoldDeep);
                                approval.Item().Text(
                                        $"{context.ApprovedByName} · {IstClock.ToIst(order.ApprovedUtc.Value):dd MMM yyyy}")
                                    .FontSize(9);
                            }
                        });
                        close.ConstantItem(200).AlignRight().Column(sign =>
                        {
                            sign.Item().PaddingTop(26).BorderTop(1).BorderColor(Border).PaddingTop(4)
                                .AlignRight().Text($"For {context.CompanyName}").FontSize(8.5f);
                            sign.Item().AlignRight().Text("Authorised signatory").FontSize(8).FontColor(Muted);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span($"{order.PoNumber} · Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span(" · Please quote this PO number and our GSTIN on your invoice.");
                });
            });
        }).GeneratePdf();

        static void Caption(ColumnDescriptor column, string text) =>
            column.Item().PaddingBottom(4).Text(text).Bold().FontSize(8.5f).FontColor(GoldDeep);

        static IContainer HeaderCell(IContainer container) =>
            container.Background(Black).PaddingVertical(6).PaddingHorizontal(6)
                .DefaultTextStyle(style => style.FontColor(Gold).Bold().FontSize(9));

        static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(6);

        static void DetailRow(ColumnDescriptor details, string label, string value) =>
            details.Item().Row(row =>
            {
                row.ConstantItem(70).Text(label).FontSize(8.5f).FontColor(Muted);
                row.RelativeItem().Text(value).FontSize(9);
            });

        static void TotalRow(ColumnDescriptor totals, string label, decimal amount) =>
            totals.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                row.ConstantItem(130).AlignRight().Text($"Rs {amount:N2}");
            });
    }

    private static string StatusLabel(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "DRAFT — NOT FOR ISSUE",
        PurchaseOrderStatus.PendingApproval => "AWAITING APPROVAL — NOT FOR ISSUE",
        PurchaseOrderStatus.Approved => "APPROVED",
        PurchaseOrderStatus.Sent => "ISSUED",
        PurchaseOrderStatus.PartiallyReceived => "PARTIALLY RECEIVED",
        PurchaseOrderStatus.Received => "RECEIVED",
        _ => "CANCELLED"
    };

    private static string StatusColour(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Approved or PurchaseOrderStatus.Sent or PurchaseOrderStatus.Received
            => Colors.Green.Darken2,
        PurchaseOrderStatus.Cancelled => Colors.Red.Darken2,
        _ => GoldDeep
    };
}
