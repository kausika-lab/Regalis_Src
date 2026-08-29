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
    IOptions<StorageOptions> storageOptions) : IProfileService
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

    private static ProfileDto ToDto(Domain.Entities.Identity.User user) => new(
        user.UserId, user.FullName, user.Email, user.MobileNumber, user.CompanyName,
        user.Address, user.PhotoPath is not null, (byte)user.UserType, user.CreatedDate);
}
