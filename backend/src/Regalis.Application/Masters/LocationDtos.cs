using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Masters;

public record LocationDto(
    int LocationId,
    string LocationCode,
    string LocationName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactNumber,
    string? Email,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string WorkingDays,
    decimal? Latitude,
    decimal? Longitude,
    string? Description,
    bool IsDefault,
    bool IsActive,
    int WorkspaceCount);

public record SaveLocationRequest(
    string LocationCode,
    string LocationName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactNumber,
    string? Email,
    string WorkingHoursStart,
    string WorkingHoursEnd,
    string[] WorkingDays,
    decimal? Latitude,
    decimal? Longitude,
    string? Description,
    bool IsDefault);

public class SaveLocationRequestValidator : AbstractValidator<SaveLocationRequest>
{
    private static readonly string[] ValidDays = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];

    public SaveLocationRequestValidator()
    {
        RuleFor(x => x.LocationCode).NotEmpty().MaximumLength(30).Matches("^[A-Z0-9-]+$")
            .WithMessage("Location code must be uppercase letters, digits and hyphens.");
        RuleFor(x => x.LocationName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.ContactNumber).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.WorkingHoursStart).NotEmpty()
            .Must(v => TimeOnly.TryParse(v, out _)).WithMessage("Use HH:mm, e.g. 08:00.");
        RuleFor(x => x.WorkingHoursEnd).NotEmpty()
            .Must(v => TimeOnly.TryParse(v, out _)).WithMessage("Use HH:mm, e.g. 21:00.");
        RuleFor(x => x)
            .Must(x => TimeOnly.TryParse(x.WorkingHoursStart, out var s) &&
                       TimeOnly.TryParse(x.WorkingHoursEnd, out var e) && e > s)
            .WithMessage("Working hours end must be after the start.")
            .OverridePropertyName(nameof(SaveLocationRequest.WorkingHoursEnd));
        RuleFor(x => x.WorkingDays).NotEmpty().WithMessage("Select at least one working day.")
            .Must(days => days.All(d => ValidDays.Contains(d)))
            .WithMessage("Working days must be MON…SUN codes.");
    }
}

public interface ILocationService
{
    Task<PagedResult<LocationDto>> GetLocationsAsync(PagedRequest request, CancellationToken ct = default);
    Task<LocationDto> GetLocationAsync(int locationId, CancellationToken ct = default);
    Task<LocationDto> CreateLocationAsync(SaveLocationRequest request, CancellationToken ct = default);
    Task<LocationDto> UpdateLocationAsync(int locationId, SaveLocationRequest request, CancellationToken ct = default);
    Task DeleteLocationAsync(int locationId, CancellationToken ct = default);
}
