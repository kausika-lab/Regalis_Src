using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>Own-record profile (matrix §6: always scoped to the caller, never an id
/// from the client). Email is the login and is not edited here. Invoices are
/// unaffected by profile edits — they snapshot at issue time.</summary>
public class ProfileService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IOptions<StorageOptions> storageOptions,
    IDateTimeProvider clock) : IProfileService
{
    private const long MaxPhotoBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedPhotoTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    public async Task<ProfileDto> GetAsync(CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstAsync(u => u.UserId == currentUser.UserId, ct);
        return ToDto(user);
    }

    public async Task<ProfileDto> UpdateAsync(UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(u => u.UserId == currentUser.UserId, ct);
        var mobile = request.MobileNumber.Trim();
        if (mobile != user.MobileNumber &&
            await db.Users.AnyAsync(u => u.MobileNumber == mobile && u.UserId != user.UserId, ct))
        {
            throw new ConflictException("That mobile number is already registered.");
        }
        user.FullName = request.FullName.Trim();
        user.MobileNumber = mobile;
        user.CompanyName = request.CompanyName?.Trim();
        user.Address = request.Address?.Trim();
        await AdminAlerts.AccountAsync(db, user, "Profile updated", "was updated", clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<ProfileDto> SetPhotoAsync(AttachmentUpload upload,
        CancellationToken ct = default)
    {
        if (upload.Length is <= 0 or > MaxPhotoBytes)
        {
            throw new BusinessRuleException("Profile photos are limited to 2 MB.");
        }
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedPhotoTypes.ContainsKey(extension))
        {
            throw new BusinessRuleException("Allowed photo types: .png .jpg .jpeg .webp");
        }
        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > MaxPhotoBytes || !FileTypeSniffer.Matches(extension, bytes))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }

        var user = await db.Users.FirstAsync(u => u.UserId == currentUser.UserId, ct);
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "profiles");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory,
            $"{user.UserId}{extension.ToLowerInvariant()}");
        await File.WriteAllBytesAsync(filePath, bytes, ct);
        user.PhotoPath = filePath;
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<ProfilePhotoContent> GetPhotoAsync(CancellationToken ct = default)
    {
        var photoPath = await db.Users.AsNoTracking()
            .Where(u => u.UserId == currentUser.UserId)
            .Select(u => u.PhotoPath)
            .FirstAsync(ct);
        if (photoPath is null || !File.Exists(photoPath))
        {
            throw new NotFoundException("ProfilePhoto", currentUser.UserId);
        }
        var contentType = AllowedPhotoTypes.GetValueOrDefault(
            Path.GetExtension(photoPath), "application/octet-stream");
        return new ProfilePhotoContent(await File.ReadAllBytesAsync(photoPath, ct), contentType);
    }

    // ── Onboarding ──────────────────────────────────────────────────────────

    public async Task<OnboardingStatusDto> GetOnboardingStatusAsync(CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstAsync(u => u.UserId == currentUser.UserId, ct);
        return new OnboardingStatusDto(user.IsProfileComplete, MissingFields(user), ToDto(user));
    }

    public async Task<ProfileDto> CompleteOnboardingAsync(CompleteOnboardingRequest request,
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(u => u.UserId == currentUser.UserId, ct);

        // Staff and admins are created by an administrator, not through a signup form.
        // Accepting this from them would be a second, unreviewed way to edit a staff row.
        if (user.UserType != Domain.Enums.UserType.Customer)
        {
            throw new ForbiddenException("Onboarding applies to member accounts only.");
        }

        var mobile = request.MobileNumber.Trim();
        if (mobile != user.MobileNumber &&
            await db.Users.AnyAsync(u => u.MobileNumber == mobile && u.UserId != user.UserId, ct))
        {
            throw new ConflictException("That mobile number is already registered.");
        }

        user.FullName = request.FullName.Trim();
        user.MobileNumber = mobile;
        user.CompanyName = request.CompanyName.Trim();
        user.AddressLine1 = request.AddressLine1.Trim();
        user.AddressLine2 = request.AddressLine2.Trim();
        user.City = request.City.Trim();
        user.State = request.State.Trim();
        user.Country = request.Country.Trim();
        user.PostalCode = request.PostalCode.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = (Domain.Enums.Gender)request.Gender;
        user.Gstin = request.Gstin.Trim().ToUpperInvariant();
        // The legacy single line is what TRN_Invoice snapshots, so it has to keep up.
        user.Address = ComposeAddress(user);

        await AdminAlerts.AccountAsync(db, user, "Profile completed",
            "finished onboarding", clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    /// <summary>
    /// What is still missing, in the order the form shows it. This mirrors
    /// User.IsProfileComplete: change a rule there and it changes here too.
    /// </summary>
    private static List<string> MissingFields(Domain.Entities.Identity.User user)
    {
        var missing = new List<string>();
        if (user.UserType != Domain.Enums.UserType.Customer)
        {
            return missing;
        }

        void Require(string name, bool present)
        {
            if (!present) missing.Add(name);
        }

        Require("mobileNumber", !string.IsNullOrWhiteSpace(user.MobileNumber));
        Require("companyName", !string.IsNullOrWhiteSpace(user.CompanyName));
        Require("addressLine1", !string.IsNullOrWhiteSpace(user.AddressLine1));
        Require("addressLine2", !string.IsNullOrWhiteSpace(user.AddressLine2));
        Require("city", !string.IsNullOrWhiteSpace(user.City));
        Require("state", !string.IsNullOrWhiteSpace(user.State));
        Require("country", !string.IsNullOrWhiteSpace(user.Country));
        Require("postalCode", !string.IsNullOrWhiteSpace(user.PostalCode));
        Require("dateOfBirth", user.DateOfBirth is not null);
        Require("gender", user.Gender is not null);
        Require("gstin", !string.IsNullOrWhiteSpace(user.Gstin));
        return missing;
    }

    /// <summary>Flattens the structured address into the legacy single line.</summary>
    internal static string ComposeAddress(Domain.Entities.Identity.User user) =>
        string.Join(", ", new[]
        {
            user.AddressLine1, user.AddressLine2, user.City, user.State,
            user.PostalCode, user.Country
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static ProfileDto ToDto(Domain.Entities.Identity.User user) => new(
        user.UserId, user.FullName, user.Email, user.MobileNumber, user.CompanyName,
        user.Address, user.PhotoPath is not null, (byte)user.UserType, user.CreatedDate,
        user.AddressLine1, user.AddressLine2, user.City, user.State, user.Country,
        user.PostalCode, user.DateOfBirth, (byte?)user.Gender, user.Gstin,
        user.IsProfileComplete, user.PasswordHash is not null, user.GoogleId is not null);
}
