using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.QuotationAccount;
using Regalis.Application.Finance.QuotationInvoice;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.QuotationAccount;

/// <summary>
/// quotation-account kit (27 Nov 2026). "Convert to invoice" on the admin quotation page for a quotation
/// without a customer account: optionally corrects the quotation's email / mobile (approved, no account,
/// no invoice yet; needs QUOTATION.EDIT; written to the timeline), then raises the one invoice through
/// <see cref="IQuotationInvoicing"/>, which links or creates the customer. All in one transaction: when
/// the customer cannot be linked or created, nothing changes and the reason comes back as a 422.
/// </summary>
public sealed class QuotationAccountService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IQuotationInvoicing invoicing,
    IQuotationCustomerAccounts customerAccounts) : IQuotationAccountService
{
    private static readonly Regex MobilePattern = new("^\\+?[0-9]{10,15}$", RegexOptions.CultureInvariant);

    public async Task<QuotationInvoiceRaisedDto> RaiseInvoiceAsync(long quotationId, RaiseQuotationInvoiceRequest request, CancellationToken ct = default)
    {
        if (currentUser.UserType == UserType.Customer)
        {
            throw new NotFoundException("Quotation", quotationId);
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT QuotationId FROM TRN_Quotation WITH (UPDLOCK, HOLDLOCK) WHERE QuotationId = {quotationId}", ct);
            Quotation quotation = await db.Quotations.FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
                ?? throw new NotFoundException("Quotation", quotationId);
            if (quotation.Status != QuotationStatus.Approved)
            {
                throw new BusinessRuleException("Only an approved quotation can be invoiced. " + quotation.QuotationNumber + " is " + quotation.Status.ToString().ToLowerInvariant() + ".");
            }

            string? newEmail = Clean(request.CustomerEmail);
            string? newMobile = Clean(request.CustomerMobile);
            bool emailChanged = newEmail != null && !string.Equals(newEmail, Clean(quotation.CustomerEmail), StringComparison.OrdinalIgnoreCase);
            bool mobileChanged = newMobile != null && Digits(newMobile) != Digits(quotation.CustomerMobile);
            if (emailChanged || mobileChanged)
            {
                if (quotation.CustomerId.HasValue || quotation.InvoiceId.HasValue)
                {
                    throw new BusinessRuleException("The email and mobile can be changed here only for a quotation that has no customer account and no invoice yet.");
                }
                if (!currentUser.HasPermission("QUOTATION.EDIT"))
                {
                    throw new ForbiddenException("Changing the quotation's email or mobile needs the QUOTATION.EDIT permission.");
                }
                if (emailChanged && (newEmail!.Length > 256 || !new EmailAddressAttribute().IsValid(newEmail) || !newEmail.Contains('.')))
                {
                    throw new AppValidationException(new[] { new Regalis.Shared.Api.ApiError("VALIDATION_ERROR", $"'{newEmail}' is not a valid email address.", "CustomerEmail") });
                }
                string? storedMobile = mobileChanged ? QuotationCustomerAccounts.NormaliseMobile(newMobile!) : null;
                if (mobileChanged && !MobilePattern.IsMatch(storedMobile!))
                {
                    throw new AppValidationException(new[] { new Regalis.Shared.Api.ApiError("VALIDATION_ERROR", $"'{newMobile}' is not a valid mobile number.", "CustomerMobile") });
                }
                string summary = "Contact corrected before raising the invoice:"
                    + (emailChanged ? $" email {Clean(quotation.CustomerEmail) ?? "(none)"} -> {newEmail}." : "")
                    + (mobileChanged ? $" mobile {Clean(quotation.CustomerMobile) ?? "(none)"} -> {storedMobile}." : "");
                if (emailChanged)
                {
                    quotation.CustomerEmail = newEmail!;
                }
                if (mobileChanged)
                {
                    quotation.CustomerMobile = storedMobile;
                }
                db.Set<QuotationEvent>().Add(new QuotationEvent
                {
                    QuotationId = quotation.QuotationId,
                    EventType = QuotationEventType.NoteAdded,
                    ActorType = QuotationActorType.Admin,
                    ActorUserId = currentUser.UserId,
                    Summary = summary,
                    CreatedUtc = clock.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }

            QuotationInvoiceOutcome outcome = await invoicing.ConvertAsync(quotationId, ct);
            await tx.CommitAsync(ct);
            QuotationCustomerLink? link = customerAccounts.LastLink;
            long customerId = quotation.CustomerId ?? 0;
            string? customerName = link?.CustomerName ?? await db.Users.AsNoTracking().Where(u => u.UserId == customerId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
            string message = !outcome.Created
                ? $"Already invoiced as {outcome.InvoiceNumber}."
                : link == null
                    ? $"Invoice {outcome.InvoiceNumber} raised."
                    : link.Kind == "Created"
                        ? $"Customer account {link.CustomerName} (#{link.CustomerId}) created and invoice {outcome.InvoiceNumber} raised."
                        : $"Linked to the existing customer {link.CustomerName} (#{link.CustomerId}) and invoice {outcome.InvoiceNumber} raised.";
            return new QuotationInvoiceRaisedDto(quotation.QuotationId, quotation.QuotationNumber, outcome.InvoiceId, outcome.InvoiceNumber, outcome.Created,
                customerId, customerName, link?.Kind, message);
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Digits(string? value)
    {
        string d = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
        return d.Length == 12 && d.StartsWith("91", StringComparison.Ordinal) ? d[2..] : d;
    }
}
