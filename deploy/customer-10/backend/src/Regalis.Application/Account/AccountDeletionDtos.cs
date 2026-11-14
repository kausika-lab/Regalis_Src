using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Account;

/// <summary>Something that must be settled before the account can be deleted.</summary>
public sealed record DeletionBlocker(string Code, string Message, string? ActionLabel, string? ActionRoute);

/// <summary>What the Delete Account dialog needs before asking for confirmation.
/// Method is "password" when the account has one, otherwise "otp" (Google-only sign-in).</summary>
public sealed record AccountDeletionCheckDto(bool CanDelete, string Method, string MaskedEmail, IReadOnlyList<DeletionBlocker> Blockers);

/// <summary>Password (accounts with one) or the emailed code (Google-only accounts).</summary>
public sealed record DeleteAccountRequest(string? Password, string? Code);

public interface IAccountDeletionService
{
    Task<AccountDeletionCheckDto> CheckAsync(CancellationToken ct = default);

    /// <summary>Emails a one-time code to the signed-in customer (Google-only accounts).</summary>
    Task SendCodeAsync(CancellationToken ct = default);

    /// <summary>Verifies identity, re-checks the blockers, then closes and anonymises the account.</summary>
    Task DeleteAsync(DeleteAccountRequest request, CancellationToken ct = default);
}
