using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Masters;

public record WorkspaceTypeDto(
    int WorkspaceTypeId,
    string TypeCode,
    string TypeName,
    string? IconName,
    int DisplayOrder,
    bool RequiresCapacity,
    int WorkspaceCount);

public record AmenityDto(int AmenityId, string AmenityName, string? IconName, int DisplayOrder);

public record SaveAmenityRequest(string AmenityName, string? IconName, int DisplayOrder);

public class SaveAmenityRequestValidator : AbstractValidator<SaveAmenityRequest>
{
    public SaveAmenityRequestValidator()
    {
        RuleFor(x => x.AmenityName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IconName).MaximumLength(60);
    }
}

public class WorkspaceListRequest : PagedRequest
{
    public int? LocationId { get; set; }
    public int? WorkspaceTypeId { get; set; }
    /// <summary>1 Available, 2 Occupied, 3 Maintenance, 4 Disabled.</summary>
    public byte? Status { get; set; }
}

public record WorkspaceDto(
    int WorkspaceId,
    int LocationId,
    string LocationName,
    int WorkspaceTypeId,
    string WorkspaceTypeName,
    string WorkspaceCode,
    string WorkspaceName,
    string? FloorNumber,
    int Capacity,
    decimal PricePerHour,
    decimal? PricePerDay,
    string? Equipment,
    string? Description,
    byte Status,
    bool IsBookable,
    bool IsActive,
    IReadOnlyList<AmenityDto> Amenities);

public record SaveWorkspaceRequest(
    int LocationId,
    int WorkspaceTypeId,
    string WorkspaceCode,
    string WorkspaceName,
    string? FloorNumber,
    int Capacity,
    decimal PricePerHour,
    decimal? PricePerDay,
    string? Equipment,
    string? Description,
    bool IsBookable,
    int[] AmenityIds);

public record UpdateWorkspaceStatusRequest(byte Status);

public class SaveWorkspaceRequestValidator : AbstractValidator<SaveWorkspaceRequest>
{
    public SaveWorkspaceRequestValidator()
    {
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0);
        RuleFor(x => x.WorkspaceCode).NotEmpty().MaximumLength(40).Matches("^[A-Z0-9-]+$")
            .WithMessage("Workspace code must be uppercase letters, digits and hyphens, e.g. D-001.");
        RuleFor(x => x.WorkspaceName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.FloorNumber).MaximumLength(20);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PricePerHour).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PricePerDay).GreaterThanOrEqualTo(0).When(x => x.PricePerDay is not null);
        RuleFor(x => x.Equipment).MaximumLength(500);
    }
}

public class UpdateWorkspaceStatusRequestValidator : AbstractValidator<UpdateWorkspaceStatusRequest>
{
    public UpdateWorkspaceStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is 1 or 2 or 3 or 4)
            .WithMessage("Status must be 1 Available, 2 Occupied, 3 Maintenance or 4 Disabled.");
    }
}

public interface IWorkspaceService
{
    Task<List<WorkspaceTypeDto>> GetWorkspaceTypesAsync(CancellationToken ct = default);
    Task<List<AmenityDto>> GetAmenitiesAsync(CancellationToken ct = default);
    Task<AmenityDto> CreateAmenityAsync(SaveAmenityRequest request, CancellationToken ct = default);
    Task<PagedResult<WorkspaceDto>> GetWorkspacesAsync(WorkspaceListRequest request, CancellationToken ct = default);
    Task<WorkspaceDto> GetWorkspaceAsync(int workspaceId, CancellationToken ct = default);
    Task<WorkspaceDto> CreateWorkspaceAsync(SaveWorkspaceRequest request, CancellationToken ct = default);
    Task<WorkspaceDto> UpdateWorkspaceAsync(int workspaceId, SaveWorkspaceRequest request, CancellationToken ct = default);
    Task<WorkspaceDto> UpdateWorkspaceStatusAsync(int workspaceId, UpdateWorkspaceStatusRequest request, CancellationToken ct = default);
    Task DeleteWorkspaceAsync(int workspaceId, CancellationToken ct = default);
}
