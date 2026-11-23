using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Offers;

/// <summary>Admin portal: Offers Management (OFFER.VIEW / OFFER.MANAGE).</summary>
public interface IOfferAdminService
{
	Task<OfferListDto> ListAsync(OfferListRequest request, CancellationToken ct = default);

	Task<OfferDetailDto> GetAsync(int offerId, CancellationToken ct = default);

	Task<OfferOptionsDto> OptionsAsync(CancellationToken ct = default);

	Task<OfferDetailDto> CreateAsync(SaveOfferRequest request, CancellationToken ct = default);

	Task<OfferDetailDto> UpdateAsync(int offerId, SaveOfferRequest request, CancellationToken ct = default);

	Task<OfferDetailDto> SetActiveAsync(int offerId, bool active, CancellationToken ct = default);

	Task DeleteAsync(int offerId, CancellationToken ct = default);

	Task<OfferDetailDto> SetImageAsync(int offerId, string fileName, string? contentType, byte[] data, CancellationToken ct = default);

	Task<OfferDetailDto> RemoveImageAsync(int offerId, CancellationToken ct = default);

	Task<List<OfferRedemptionRowDto>> RedemptionsAsync(int offerId, CancellationToken ct = default);
}

/// <summary>Customer portal: the offers a module shows, the price preview, and applying offers to
/// the customer's own held bookings. Passes and memberships take the offer ids on the existing
/// /passes/order and /passes/purchase calls instead.</summary>
public interface IOfferPortalService
{
	Task<List<PortalOfferDto>> ListAsync(string? module, CancellationToken ct = default);

	Task<OfferPreviewDto> PreviewAsync(OfferPreviewRequest request, CancellationToken ct = default);

	Task<ApplyBookingOffersResultDto> ApplyToBookingsAsync(ApplyBookingOffersRequest request, CancellationToken ct = default);

	/// <summary>The banner picture (any signed-in or anonymous caller; offers are public promotions).</summary>
	Task<(byte[] Data, string ContentType)?> ImageAsync(int offerId, CancellationToken ct = default);
}
