using FluentValidation;
using Regalis.Application.Auth;
using Regalis.Shared.Api;

namespace Regalis.Application.Administration;

public class UserListRequest : PagedRequest
{
    /// <summary>1 Customer, 2 Admin, 3 Staff.</summary>
    public byte? UserType { get; set; }
    /// <summary>1 Active, 2 Inactive, 3 Locked, 4 PendingVerification.</summary>
    public byte? Status { get; set; }
    public int? RoleId { get; set; }
}

public record UserListItemDto(
    long UserId,
    string FullName,
    string Email,
    string MobileNumber,
    byte UserType,
    byte Status,
    string? CompanyName,
    IReadOnlyList<string> Roles,
    DateTime? LastLoginUtc,
    DateTime CreatedDate,
    bool IsActive);

public record UserDetailDto(
    long UserId,
    string FullName,
    string Email,
    string MobileNumber,
    byte UserType,
    byte Status,
    string? CompanyName,
    string? Address,
    string? PhotoPath,
    int? DefaultLocationId,
    bool IsEmailVerified,
    bool IsMobileVerified,
    bool MustChangePassword,
    DateTime? LastLoginUtc,
    DateTime? LockoutEndUtc,
    IReadOnlyList<int> RoleIds,
    IReadOnlyList<string> Roles,
    DateTime CreatedDate,
    bool IsActive);

public record CreateUserRequest(
    string FullName,
    string Email,
    string MobileNumber,
    string Password,
    byte UserType,
    int[] RoleIds,
    string? CompanyName,
    int? DefaultLocationId);

public record UpdateUserRequest(
    string FullName,
    string MobileNumber,
    int[] RoleIds,
    string? CompanyName,
    string? Address,
    int? DefaultLocationId);

public record AdminResetPasswordRequest(string NewPassword);

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.MobileNumber).NotEmpty().Matches(@"^\+?[0-9]{10,15}$");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MustBeWellComposed();
        RuleFor(x => x.UserType).Must(t => t is 1 or 2 or 3)
            .WithMessage("UserType must be 1 (Customer), 2 (Admin) or 3 (Staff).");
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Assign at least one role.");
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MobileNumber).NotEmpty().Matches(@"^\+?[0-9]{10,15}$");
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Assign at least one role.");
    }
}

public class AdminResetPasswordRequestValidator : AbstractValidator<AdminResetPasswordRequest>
{
    public AdminResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MustBeWellComposed();
    }
}

public interface IUserAdminService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(UserListRequest request, CancellationToken ct = default);
    Task<UserDetailDto> GetUserAsync(long userId, CancellationToken ct = default);
    Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto> UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken ct = default);
    Task ActivateAsync(long userId, CancellationToken ct = default);
    Task DeactivateAsync(long userId, CancellationToken ct = default);
    Task LockAsync(long userId, CancellationToken ct = default);
    Task UnlockAsync(long userId, CancellationToken ct = default);
    Task ResetPasswordAsync(long userId, AdminResetPasswordRequest request, CancellationToken ct = default);
    Task DeleteAsync(long userId, CancellationToken ct = default);
}
