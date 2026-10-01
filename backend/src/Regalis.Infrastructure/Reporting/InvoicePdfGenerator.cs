using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>What the tax invoice prints besides the invoice row itself: the logo and the
/// company-level details (place of supply, bank strip, default terms) read from settings
/// when the PDF is made.</summary>
public sealed record InvoicePdfExtras(
    byte[]? Logo = null,
    string? PlaceOfSupply = null,
    string? BankAccountName = null,
    string? BankAccountNumber = null,
    string? BankIfsc = null,
    string? BankBranch = null,
    string? DefaultTerms = null);

/// <summary>The GST tax invoice as a PDF (QuestPDF, community license), generated on
/// demand from the snapshotted invoice — later profile or settings edits never
/// change who was billed or for what. Layout: logo, status stamp and company block;
/// a "TAX INVOICE" rule; bill-to beside the invoice facts; a ruled item grid with the
/// CGST/SGST (or IGST) split per line; totals with the amount in words; bank details,
/// terms and signature lines.</summary>
public static class InvoicePdfGenerator
{
    private const string Accent = "#2E75B6";
    private const string HeadFill = "#EAF4FB";
    private const string Rule = "#D9DEE3";
    private const string Muted = "#6B7280";
    private const string Ink = "#1F2328";
    private const string Watermark = "Regalis WorkSpace";

    static InvoicePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(Invoice invoice, byte[]? logo = null) =>
        Generate(invoice, new InvoicePdfExtras(Logo: logo));

    public static byte[] Generate(Invoice invoice, InvoicePdfExtras extras)
    {
        var igst = invoice.IgstAmount > 0;
        var lines = invoice.Lines.OrderBy(l => l.LineNumber).ToList();
        var terms = string.IsNullOrWhiteSpace(invoice.TermsAndConditions)
            ? extras.DefaultTerms
            : invoice.TermsAndConditions;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(32);
                page.DefaultTextStyle(style => style.FontSize(9).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem(1.1f).AlignLeft().AlignMiddle().Element(e =>
                        {
                            if (extras.Logo is { Length: > 0 })
                            {
                                e.Height(68).Image(extras.Logo).FitArea();
                            }
                            else
                            {
                                e.Text(invoice.CompanyName).Bold().FontSize(16);
                            }
                        });
                        row.RelativeItem(0.8f).AlignCenter().AlignMiddle().Element(e => Stamp(e, invoice.Status));
                        row.RelativeItem(1.3f).AlignRight().Column(company =>
                        {
                            company.Item().AlignRight().Text(invoice.CompanyName).Bold().FontSize(10.5f);
                            if (!string.IsNullOrWhiteSpace(invoice.CompanyAddress))
                            {
                                company.Item().AlignRight().Text(invoice.CompanyAddress).FontSize(8.5f);
                            }
                            if (!string.IsNullOrWhiteSpace(invoice.CompanyGstin))
                            {
                                company.Item().AlignRight().Text($"GSTIN {invoice.CompanyGstin}").FontSize(8.5f);
                            }
                        });
                    });

                    header.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().LineHorizontal(1).LineColor(Rule);
                        row.AutoItem().PaddingHorizontal(14).Text("TAX INVOICE").FontSize(15).FontColor(Accent);
                        row.RelativeItem().AlignMiddle().LineHorizontal(1).LineColor(Rule);
                    });
                });

                page.Content().PaddingTop(14).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(16).Column(billTo =>
                        {
                            billTo.Spacing(2);
                            billTo.Item().Text("Bill To").FontSize(9.5f);
                            if (!string.IsNullOrWhiteSpace(invoice.CustomerCompanyName))
                            {
                                billTo.Item().Text(invoice.CustomerCompanyName).Bold().FontSize(10.5f);
                                billTo.Item().Text(invoice.CustomerName);
                            }
                            else
                            {
                                billTo.Item().Text(invoice.CustomerName).Bold().FontSize(10.5f);
                            }
                            if (!string.IsNullOrWhiteSpace(invoice.BillingAddress))
                            {
                                billTo.Item().Text(invoice.BillingAddress);
                            }
                            var contact = string.Join("  |  ", new[] { invoice.CustomerMobile, invoice.CustomerEmail }
                                .Where(v => !string.IsNullOrWhiteSpace(v)));
                            if (contact.Length > 0)
                            {
                                billTo.Item().Text(contact).FontColor(Muted);
                            }
                            if (!string.IsNullOrWhiteSpace(invoice.CustomerGstin))
                            {
                                billTo.Item().PaddingTop(2).Text(text =>
                                {
                                    text.Span("GSTIN ").FontColor(Muted);
                                    text.Span(invoice.CustomerGstin).SemiBold();
                                });
                            }
                        });
                        row.ConstantItem(230).Table(facts =>
                        {
                            facts.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.15f);
                            });
                            Fact(facts, "Invoice#", invoice.InvoiceNumber);
                            Fact(facts, "Invoice Date", invoice.InvoiceDate.ToString("dd MMM yyyy"));
                            Fact(facts, "Terms", TermsLabel(invoice));
                            Fact(facts, "Due Date", invoice.DueDate.ToString("dd MMM yyyy"));
                            if (invoice.PaidUtc is { } paid)
                            {
                                Fact(facts, "Paid On", IstClock.IstDateOf(paid).ToString("dd MMM yyyy"));
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(extras.PlaceOfSupply))
                    {
                        column.Item().Text($"Place Of Supply: {extras.PlaceOfSupply}").FontSize(8.5f);
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(4);
                            c.ConstantColumn(54);
                            c.ConstantColumn(40);
                            c.ConstantColumn(60);
                            if (igst)
                            {
                                c.ConstantColumn(64);
                            }
                            else
                            {
                                c.ConstantColumn(56);
                                c.ConstantColumn(56);
                            }
                            c.ConstantColumn(70);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(Head).AlignCenter().Text("#");
                            h.Cell().Element(Head).Text("Item & Description");
                            h.Cell().Element(Head).AlignRight().Text("HSN/SAC");
                            h.Cell().Element(Head).AlignRight().Text("Qty");
                            h.Cell().Element(Head).AlignRight().Text("Rate");
                            if (igst)
                            {
                                h.Cell().Element(Head).AlignRight().Text("IGST");
                            }
                            else
                            {
                                h.Cell().Element(Head).AlignRight().Text("CGST");
                                h.Cell().Element(Head).AlignRight().Text("SGST");
                            }
                            h.Cell().Element(Head).AlignRight().Text("Amount");
                        });
                        foreach (var line in lines)
                        {
                            var (title, detail) = SplitDescription(line.Description);
                            table.Cell().Element(Body).AlignCenter().Text(line.LineNumber.ToString());
                            table.Cell().Element(Body).Column(d =>
                            {
                                d.Item().Text(title);
                                if (detail is not null)
                                {
                                    d.Item().Text(detail).FontSize(8).FontColor(Muted);
                                }
                            });
                            table.Cell().Element(Body).AlignRight().Text(line.HsnSacCode ?? "");
                            table.Cell().Element(Body).AlignRight().Text($"{line.Quantity:0.##}");
                            table.Cell().Element(Body).AlignRight().Text($"{line.UnitPrice:N2}");
                            if (igst)
                            {
                                TaxCell(table, line.TaxAmount, line.TaxPercent);
                            }
                            else
                            {
                                var cgst = Math.Round(line.TaxAmount / 2, 2, MidpointRounding.AwayFromZero);
                                TaxCell(table, cgst, line.TaxPercent / 2);
                                TaxCell(table, line.TaxAmount - cgst, line.TaxPercent / 2);
                            }
                            table.Cell().Element(Body).AlignRight().Text($"{line.LineAmount:N2}");
                        }
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(16).PaddingTop(4).Text(
                            !string.IsNullOrWhiteSpace(invoice.Notes) ? invoice.Notes
                            : invoice.Status == InvoiceStatus.Paid ? "Thank you for the payment."
                            : "Thank you for your business.");
                        row.ConstantItem(250).Column(totals =>
                        {
                            Total(totals, "Sub Total", Money(invoice.SubTotal));
                            if (invoice.DiscountAmount > 0)
                            {
                                Total(totals, "Discount", $"(-) {invoice.DiscountAmount:N2}");
                            }
                            if (invoice.CgstAmount > 0)
                            {
                                Total(totals, $"CGST ({invoice.CgstPercent:0.##}%)", Money(invoice.CgstAmount));
                            }
                            if (invoice.SgstAmount > 0)
                            {
                                Total(totals, $"SGST ({invoice.SgstPercent:0.##}%)", Money(invoice.SgstAmount));
                            }
                            if (invoice.IgstAmount > 0)
                            {
                                Total(totals, $"IGST ({invoice.IgstPercent:0.##}%)", Money(invoice.IgstAmount));
                            }
                            if (invoice.TdsAmount > 0)
                            {
                                Total(totals, invoice.TdsLabel ?? $"TDS ({invoice.TdsPercent:0.##}%)",
                                    $"(-) {invoice.TdsAmount:N2}");
                            }
                            if (invoice.AdjustmentAmount != 0)
                            {
                                Total(totals, invoice.AdjustmentLabel ?? "Adjustment", Signed(invoice.AdjustmentAmount));
                            }
                            if (invoice.RoundOffAmount != 0)
                            {
                                Total(totals, "Round Off", Signed(invoice.RoundOffAmount));
                            }
                            totals.Item().BorderTop(1).BorderColor(Rule).PaddingVertical(5).Row(r =>
                            {
                                r.RelativeItem().AlignRight().PaddingRight(12).Text("Total").Bold();
                                r.ConstantItem(100).AlignRight().Text($"₹{invoice.TotalAmount:N2}").Bold();
                            });
                            if (invoice.PaidAmount > 0)
                            {
                                Total(totals, "Payment Made", $"(-) {invoice.PaidAmount:N2}", Colors.Red.Darken1);
                            }
                            totals.Item().BorderTop(1).BorderBottom(1).BorderColor(Rule).PaddingVertical(6).Row(r =>
                            {
                                r.RelativeItem().AlignRight().PaddingRight(12).Text("Balance Due").Bold().FontColor(Accent);
                                r.ConstantItem(100).AlignRight().Text($"₹{invoice.OutstandingAmount:N2}").Bold().FontColor(Accent);
                            });
                            totals.Item().PaddingTop(6).Row(r =>
                            {
                                r.ConstantItem(82).Text("Total In Words:").FontSize(8.5f);
                                r.RelativeItem().Text(IndianMoney.ToWords(invoice.TotalAmount))
                                    .Bold().Italic().FontSize(8.5f);
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(extras.BankAccountNumber))
                    {
                        column.Item().PaddingTop(4).Column(bank =>
                        {
                            bank.Spacing(4);
                            bank.Item().Text(text =>
                            {
                                text.Span("BANK DETAILS").SemiBold();
                                if (!string.IsNullOrWhiteSpace(extras.BankAccountName))
                                {
                                    text.Span($"   |   {extras.BankAccountName}");
                                }
                            });
                            var parts = new List<string> { $"Account No: {extras.BankAccountNumber}" };
                            if (!string.IsNullOrWhiteSpace(extras.BankIfsc)) parts.Add($"IFSC Code: {extras.BankIfsc}");
                            if (!string.IsNullOrWhiteSpace(extras.BankBranch)) parts.Add($"Branch: {extras.BankBranch}");
                            bank.Item().Text(string.Join("    |    ", parts)).FontSize(8.5f);
                        });
                    }

                    column.Item().PaddingTop(18).ShowEntire().Row(row =>
                    {
                        Signature(row, "Authorized Signature", invoice.CompanyName);
                        row.ConstantItem(36);
                        Signature(row, "Receiver Signature", null);
                        row.RelativeItem(0.6f);
                    });

                    // Last thing on the invoice.
                    if (!string.IsNullOrWhiteSpace(terms))
                    {
                        column.Item().PaddingTop(10).Column(t =>
                        {
                            t.Item().Text("Terms & Conditions").FontSize(9.5f);
                            t.Item().PaddingTop(3).Text(terms).FontSize(8).FontColor(Muted);
                        });
                    }
                });

                // Faint diagonal watermark behind every page.
                // A zero-size anchor at the page centre, rotated, with the text shifted back
                // by half its size so it is centred on the anchor.
                page.Background().AlignCenter().AlignMiddle().Unconstrained().Rotate(-35)
                    .TranslateX(-280).TranslateY(-38)
                    .Width(560).AlignCenter().Text(Watermark).FontSize(64).Bold().FontColor("#EEF2F7");

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.75f).LineColor(Rule);
                    footer.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text("This is a computer-generated invoice.").FontSize(7).FontColor(Muted);
                        row.AutoItem().Text(text =>
                        {
                            text.DefaultTextStyle(s => s.FontSize(7).FontColor(Muted));
                            text.CurrentPageNumber();
                        });
                    });
                });
            });
        }).GeneratePdf();

        static IContainer Head(IContainer c) =>
            c.Background(HeadFill).Border(0.75f).BorderColor(Rule).PaddingVertical(7).PaddingHorizontal(6)
                .DefaultTextStyle(s => s.FontSize(8.5f));

        static IContainer Body(IContainer c) =>
            c.Border(0.75f).BorderColor(Rule).PaddingVertical(8).PaddingHorizontal(6);

        static void TaxCell(TableDescriptor table, decimal amount, decimal percent) =>
            table.Cell().Element(Body).AlignRight().Column(t =>
            {
                t.Item().AlignRight().Text($"{amount:N2}");
                t.Item().AlignRight().Text($"{percent:0.##}%").FontSize(7.5f).FontColor(Muted);
            });

        static void Fact(TableDescriptor facts, string label, string value)
        {
            facts.Cell().Background(HeadFill).Border(0.75f).BorderColor(Rule).Padding(6).Text(label);
            facts.Cell().Border(0.75f).BorderColor(Rule).Padding(6).Text(value);
        }

        static void Total(ColumnDescriptor totals, string label, string value, string? color = null) =>
            totals.Item().PaddingVertical(4).Row(r =>
            {
                r.RelativeItem().AlignRight().PaddingRight(12).Text(label);
                var v = r.ConstantItem(100).AlignRight().Text(value);
                if (color is not null) v.FontColor(color);
            });

        static void Signature(RowDescriptor row, string label, string? under) =>
            row.RelativeItem().Column(s =>
            {
                s.Item().Text(label).FontSize(10);
                s.Item().PaddingTop(26).LineHorizontal(1).LineColor(Ink);
                if (under is not null)
                {
                    s.Item().PaddingTop(3).Text(under).FontSize(7.5f);
                }
            });
    }

    private static string Money(decimal amount) => $"{amount:N2}";

    private static string Signed(decimal amount) =>
        amount < 0 ? $"(-) {-amount:N2}" : $"{amount:N2}";

    /// <summary>"Due on Receipt" when due the day it was raised, else "Net N".</summary>
    private static string TermsLabel(Invoice invoice)
    {
        var days = invoice.DueDate.DayNumber - invoice.InvoiceDate.DayNumber;
        return days <= 0 ? "Due on Receipt" : $"Net {days}";
    }

    /// <summary>"Workspace booking REG-1 — Desk 09, 01 Oct" prints the part before the
    /// dash as the item and the rest underneath it in grey.</summary>
    private static (string Title, string? Detail) SplitDescription(string description)
    {
        var at = description.IndexOf(" — ", StringComparison.Ordinal);
        if (at < 0) at = description.IndexOf(" - ", StringComparison.Ordinal);
        return at <= 0
            ? (description, null)
            : (description[..at].Trim(), description[(at + 3)..].Trim());
    }

    private static void Stamp(IContainer container, InvoiceStatus status)
    {
        var (label, color) = status switch
        {
            InvoiceStatus.Paid => ("PAID", "#43A047"),
            InvoiceStatus.PartiallyPaid => ("PARTIALLY PAID", "#EF8C00"),
            InvoiceStatus.Cancelled => ("CANCELLED", "#D32F2F"),
            InvoiceStatus.Refunded => ("REFUNDED", "#757575"),
            _ => ("DUE", "#EF8C00")
        };
        container.Border(1.5f).BorderColor(color).PaddingVertical(4).PaddingHorizontal(12)
            .Text(label).FontSize(14).FontColor(color);
    }
}
