using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Reporting;

/// <summary>
/// The payment receipt (document §11, §12). Deliberately not a new numbered document:
/// TRN_Payment.PaymentNumber already is the receipt reference, so a receipt is a
/// rendering of a payment rather than a second record that could disagree with it.
/// </summary>
public static class ReceiptPdfGenerator
{
    private const string Gold = "#C9A227";
    private const string Black = "#0B0B0D";

    static ReceiptPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        Payment payment,
        string customerName,
        Invoice? invoice,
        string companyName,
        string? companyAddress,
        string? companyGstin)
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
                        column.Item().Text(companyName).Bold().FontSize(18).FontColor(Black);
                        if (!string.IsNullOrWhiteSpace(companyAddress))
                        {
                            column.Item().Text(companyAddress).FontColor(Colors.Grey.Darken1);
                        }
                        if (!string.IsNullOrWhiteSpace(companyGstin))
                        {
                            column.Item().Text($"GSTIN: {companyGstin}").FontColor(Colors.Grey.Darken1);
                        }
                    });
                    row.ConstantItem(180).Column(column =>
                    {
                        column.Item().AlignRight().Text("PAYMENT RECEIPT").Bold().FontSize(14)
                            .FontColor(Gold);
                        column.Item().AlignRight().Text(payment.PaymentNumber).Bold();
                        if (payment.PaidUtc is not null)
                        {
                            // Stored UTC, rendered IST — the same rule the portal follows.
                            var paidIst = Shared.IstClock.ToIst(payment.PaidUtc.Value);
                            column.Item().AlignRight().Text($"{paidIst:dd MMM yyyy}");
                            column.Item().AlignRight().Text($"{paidIst:HH:mm} IST")
                                .FontColor(Colors.Grey.Darken1);
                        }
                        column.Item().AlignRight().Text("RECEIVED").Bold()
                            .FontColor(Colors.Green.Darken2);
                    });
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(from =>
                    {
                        from.Item().Text("Received from").Bold().FontColor(Colors.Grey.Darken2);
                        from.Item().Text(customerName).Bold();
                        from.Item().Text($"Customer ID: {payment.CustomerId}")
                            .FontColor(Colors.Grey.Darken1);
                    });

                    column.Item().Column(rows =>
                    {
                        rows.Spacing(3);
                        DetailRow(rows, "Payment reference", payment.PaymentNumber);
                        if (invoice is not null)
                        {
                            DetailRow(rows, "Against invoice", invoice.InvoiceNumber);
                            DetailRow(rows, "Invoice total", $"₹ {invoice.TotalAmount:N2}");
                            DetailRow(rows, "Outstanding after this payment",
                                $"₹ {invoice.OutstandingAmount:N2}");
                        }
                        DetailRow(rows, "Payment method", MethodLabel(payment.PaymentMethod));
                        if (!string.IsNullOrWhiteSpace(payment.Gateway))
                        {
                            DetailRow(rows, "Gateway", payment.Gateway);
                        }
                        if (!string.IsNullOrWhiteSpace(payment.GatewayPaymentId))
                        {
                            DetailRow(rows, "Transaction ID", payment.GatewayPaymentId);
                        }
                    });

                    column.Item().AlignRight().Width(240)
                        .BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text("Amount received").Bold();
                            row.ConstantItem(110).AlignRight().Text($"₹ {payment.Amount:N2}")
                                .Bold().FontSize(12);
                        });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Computer-generated receipt · ").FontColor(Colors.Grey.Darken1);
                    text.Span(companyName).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        static void DetailRow(ColumnDescriptor rows, string label, string value) =>
            rows.Item().Row(row =>
            {
                row.ConstantItem(200).Text(label).FontColor(Colors.Grey.Darken2);
                row.RelativeItem().Text(value);
            });
    }

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Wallet => "Wallet",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.CreditCard => "Credit card",
        PaymentMethod.DebitCard => "Debit card",
        PaymentMethod.NetBanking => "Net banking",
        PaymentMethod.Cash => "Cash",
        _ => "Other"
    };
}
