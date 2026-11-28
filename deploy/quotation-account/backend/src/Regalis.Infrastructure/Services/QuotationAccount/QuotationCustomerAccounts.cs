using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.QuotationAccount;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.QuotationAccount;

/// <summary>
/// quotation-account kit (27 Nov 2026). Finds or creates the customer account of a quotation that has
/// none, and links it, inside the caller's transaction (the invoice is raised in the same one).
///
/// Order: (1) the email belongs to a staff / admin account -> refused; (2) a customer with the same email
/// (case-insensitive) -> linked; (3) the mobile belongs to a staff / admin account -> refused; (4) a
/// customer with the same mobile (+91 and 10-digit forms are the same number) -> linked; (5) otherwise a
/// customer account is created by the Customers module (<see cref="IUserAdminService.CreateUserAsync"/>,
/// the path admin "New user" and CRM "convert" use: same duplicate check, roles, wallet, admin alert),
/// with the CreateUserRequest validator run first. No password is given to anyone: as CRM does, a random
/// one nobody knows is set (MustChangePassword), and the customer signs in with "Forgot password",
/// Google or OTP. That path sends no welcome mail, so none is sent here.
///
/// Soft-deleted accounts are never matched. A refusal changes nothing and throws
/// <see cref="QuotationAccountRefusedException"/> with the reason.
/// </summary>
public sealed class QuotationCustomerAccounts(
    RegalisDbContext db,
    IUserAdminService userAdmin,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<QuotationCustomerAccounts> logger) : IQuotationCustomerAccounts
{
    public QuotationCustomerLink? LastLink { get; private set; }

    public async Task<QuotationCustomerLink> EnsureCustomerAsync(Quotation quotation, CancellationToken ct = default)
    {
        if (quotation.CustomerId.HasValue)
        {
            string existingName = await db.Users.AsNoTracking().Where(u => u.UserId == quotation.CustomerId.Value)
                .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? quotation.CustomerName;
            return new QuotationCustomerLink(quotation.CustomerId.Value, existingName, "Existing", "Already linked.");
        }

        string? email = Clean(quotation.CustomerEmail);
        string? rawMobile = Clean(quotation.CustomerMobile);
        string? mobile = rawMobile == null ? null : NormaliseMobile(rawMobile);
        string name = Clean(quotation.CustomerName) ?? "";

        // 1-2: email.
        if (email != null)
        {
            string lower = email.ToLowerInvariant();
            var byEmail = await db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && u.Email.ToLower() == lower)
                .Select(u => new { u.UserId, u.UserType, u.FullName })
                .ToListAsync(ct);
            var staff = byEmail.FirstOrDefault(u => u.UserType != UserType.Customer);
            if (staff != null)
            {
                throw new QuotationAccountRefusedException(
                    $"The email {email} belongs to a Regalis staff account ({staff.FullName}), and a quotation is never linked to a staff account. Change the quotation's email to the customer's own address, then use Convert to invoice.");
            }
            var customer = byEmail.FirstOrDefault();
            if (customer != null)
            {
                return await LinkAsync(quotation, customer.UserId, customer.FullName, "Email",
                    $"Linked to the existing customer {customer.FullName} (#{customer.UserId}), who has the same email {email}. No second account was created.", ct);
            }
        }

        // 3-4: mobile.
        if (mobile != null)
        {
            string[] aliases = MobileNumbers.Aliases(mobile).Append(rawMobile!).Distinct().ToArray();
            var byMobile = await db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && aliases.Contains(u.MobileNumber))
                .Select(u => new { u.UserId, u.UserType, u.FullName })
                .ToListAsync(ct);
            var staff = byMobile.FirstOrDefault(u => u.UserType != UserType.Customer);
            if (staff != null)
            {
                throw new QuotationAccountRefusedException(
                    $"The mobile number {rawMobile} belongs to a Regalis staff account ({staff.FullName}), and a quotation is never linked to a staff account. Change the quotation's mobile number to the customer's own, then use Convert to invoice.");
            }
            var customer = byMobile.FirstOrDefault();
            if (customer != null)
            {
                return await LinkAsync(quotation, customer.UserId, customer.FullName, "Mobile",
                    $"Linked to the existing customer {customer.FullName} (#{customer.UserId}), who has the same mobile number {rawMobile}. No second account was created.", ct);
            }
        }

        // 5: create through the Customers module.
        if (email == null)
        {
            throw new QuotationAccountRefusedException("The quotation has no email address, and a customer account needs one for signing in. Add the customer's email in Convert to invoice.");
        }
        if (mobile == null)
        {
            throw new QuotationAccountRefusedException($"No customer account has the email {email}, and a new account needs a mobile number, which the quotation does not have. Add the customer's mobile number in Convert to invoice.");
        }
        int customerRole = await db.Roles.AsNoTracking().Where(r => r.RoleCode == "CUSTOMER").Select(r => r.RoleId).FirstOrDefaultAsync(ct);
        if (customerRole == 0)
        {
            throw new QuotationAccountRefusedException("The customer account could not be created: the CUSTOMER role is missing.");
        }
        var request = new CreateUserRequest(name, email, mobile, TemporaryPassword(), (byte)UserType.Customer, new[] { customerRole },
            Clean(quotation.CustomerCompanyName), quotation.LocationId);
        var validation = new CreateUserRequestValidator().Validate(request);
        if (!validation.IsValid)
        {
            string problems = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage.TrimEnd('.') + ".").Distinct());
            throw new QuotationAccountRefusedException($"The customer account could not be created: {problems} Correct the quotation's details in Convert to invoice.");
        }

        IDbContextTransaction tx = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("EnsureCustomerAsync must run inside the invoicing transaction.");
        HashSet<object> before = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        const string savepoint = "QuotationAccount";
        await tx.CreateSavepointAsync(savepoint, ct);
        UserDetailDto created;
        try
        {
            created = await userAdmin.CreateUserAsync(request, ct);
            User user = await db.Users.FirstAsync(u => u.UserId == created.UserId, ct);
            string? gstin = Clean(quotation.CustomerGstin);
            string? address = Clean(quotation.BillingAddress);
            if (gstin != null && string.IsNullOrWhiteSpace(user.Gstin))
            {
                user.Gstin = gstin.ToUpperInvariant();
            }
            if (address != null && string.IsNullOrWhiteSpace(user.Address))
            {
                user.Address = address.Length > 500 ? address[..500] : address;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is AppException or DbUpdateException)
        {
            await tx.RollbackToSavepointAsync(savepoint, ct);
            foreach (EntityEntry entry in db.ChangeTracker.Entries().Where(e => !before.Contains(e.Entity)).ToList())
            {
                entry.State = EntityState.Detached;
            }
            string why = ex is AppException app ? app.Message : (ex.InnerException?.Message ?? ex.Message);
            logger.LogWarning(ex, "Quotation {QuotationNumber}: customer account not created", quotation.QuotationNumber);
            throw new QuotationAccountRefusedException($"The customer account could not be created: {why.TrimEnd('.')}. Correct the quotation's email or mobile in Convert to invoice.");
        }
        await tx.ReleaseSavepointAsync(savepoint, ct);
        return await LinkAsync(quotation, created.UserId, created.FullName, "Created",
            $"Customer account {created.FullName} (#{created.UserId}) created from the quotation's details by the Customers module. They sign in with \"Forgot password\" or Google using {email}.", ct);
    }

    private async Task<QuotationCustomerLink> LinkAsync(Quotation quotation, long customerId, string customerName, string kind, string summary, CancellationToken ct)
    {
        DateTime nowUtc = clock.UtcNow;
        quotation.CustomerId = customerId;
        db.Set<QuotationEvent>().Add(new QuotationEvent
        {
            QuotationId = quotation.QuotationId,
            EventType = QuotationEventType.NoteAdded,
            ActorType = currentUser.IsAuthenticated && currentUser.UserType != UserType.Customer ? QuotationActorType.Admin : QuotationActorType.System,
            ActorUserId = currentUser.IsAuthenticated ? (long?)currentUser.UserId : null,
            Summary = summary,
            CreatedUtc = nowUtc
        });
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.IsAuthenticated ? (long?)currentUser.UserId : null,
            UserName = currentUser.FullName ?? currentUser.Email,
            Action = kind == "Created" ? "QUOTATION_CUSTOMER_CREATED" : "QUOTATION_CUSTOMER_LINKED",
            ModuleName = "Finance",
            EntityName = "Quotation",
            EntityId = quotation.QuotationId.ToString(CultureInfo.InvariantCulture),
            NewValue = JsonSerializer.Serialize(new { quotation.QuotationNumber, CustomerId = customerId, CustomerName = customerName, MatchedOn = kind }),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = nowUtc
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Quotation {QuotationNumber} linked to customer {CustomerId} ({Kind})", quotation.QuotationNumber, customerId, kind);
        LastLink = new QuotationCustomerLink(customerId, customerName, kind, summary);
        return LastLink;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"98765 43210", "+91-98765-43210", "919876543210" -> "+919876543210", the stored form.
    /// Anything else keeps its digits (and a leading +) and is left to the validator.</summary>
    internal static string NormaliseMobile(string mobile)
    {
        string digits = new string(mobile.Where(char.IsAsciiDigit).ToArray());
        if (mobile.TrimStart().StartsWith('+'))
        {
            return "+" + digits;
        }
        if (digits.Length == 10)
        {
            return "+91" + digits;
        }
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            return "+" + digits;
        }
        return digits;
    }

    /// <summary>A password nobody is told (as CRM conversion does); it meets the password rule.</summary>
    private static string TemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        char[] pw = new char[16];
        for (int i = 0; i < pw.Length; i++)
        {
            pw[i] = chars[bytes[i] % chars.Length];
        }
        return new string(pw) + "Qa7#";
    }
}
