using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Regalis.Domain.Entities.Masters;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.PurchaseOrderDefaults;

/// <summary>
/// po-last-used-defaults kit (28 Nov 2026): what was saved on the last purchase order becomes
/// the default for the next new one.
///
///   Purchase.TermsAndConditions   the PO's terms                       (existing setting)
///   Purchase.LastDeliveryLocationId  the PO's LocationId               (created on first save)
///   Purchase.LastDeliveryAddress     the PO's DeliveryAddress text     (created on first save)
///
/// PurchaseOrderService calls <see cref="StageAsync"/> inside its own create / update
/// transaction, before its final SaveChanges, so the PO and the MST_AppSetting rows commit or
/// roll back together. The rows are written here, not through PUT /api/settings, so users with
/// PURCHASE.CREATE/EDIT but no SETTING.EDIT (Finance, Operations Manager) still move them.
///
/// Rules: blank terms / a blank delivery address never overwrite anything (location and address
/// are written as a pair, only when the address is non-blank); a value equal to the stored one
/// changes nothing (no row update, ModifiedDate stays); text is stored exactly as the PO stored
/// it. ModifiedBy / ModifiedDate / CreatedDate and the generic row-change TRN_AuditLog entry come
/// from the save-changes interceptor, as for the Settings screen.
/// </summary>
public static class PurchaseOrderLastUsed
{
	public const string TermsKey = "Purchase.TermsAndConditions";

	public const string LocationKey = "Purchase.LastDeliveryLocationId";

	public const string AddressKey = "Purchase.LastDeliveryAddress";

	/// <summary>po-last-used-defaults: name printed in place of the location in the PO "Shipping to" block.</summary>
	public const string ShipToNameKey = "Purchase.ShipToName";

	/// <summary>Which defaults the save moved.</summary>
	public readonly record struct Changes(bool Terms, bool Delivery);

	public static async Task<Changes> StageAsync(RegalisDbContext db, string? terms, int locationId, string? deliveryAddress, CancellationToken ct)
	{
		bool termsChanged = !string.IsNullOrWhiteSpace(terms)
			&& await StageOneAsync(db, TermsKey, terms, "string", "Default purchase order terms, copied onto each new PO (editable per PO)", ct);
		bool deliveryChanged = false;
		if (!string.IsNullOrWhiteSpace(deliveryAddress) && locationId > 0)
		{
			// Non-short-circuit: both rows must be staged.
			bool loc = await StageOneAsync(db, LocationKey, locationId.ToString(CultureInfo.InvariantCulture), "int",
				"Location of the last purchase order saved; the next new PO starts from it", ct);
			bool addr = await StageOneAsync(db, AddressKey, deliveryAddress, "string",
				"Delivery address of the last purchase order saved; the next new PO starts from it", ct);
			deliveryChanged = loc || addr;
		}
		return new Changes(termsChanged, deliveryChanged);
	}

	private static async Task<bool> StageOneAsync(RegalisDbContext db, string key, string value, string dataType, string description, CancellationToken ct)
	{
		// The unique index UX_AppSetting_Key covers soft-deleted rows too, so look past the filter.
		AppSetting? row = await db.AppSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SettingKey == key, ct);
		if (row == null)
		{
			// Same shape as db/migrations/20260925103627_PurchaseOrderSettingsAndBackfill.sql.
			db.AppSettings.Add(new AppSetting
			{
				SettingKey = key,
				SettingValue = value,
				SettingGroup = "Billing",
				DataType = dataType,
				Description = description,
				IsEditableByAdmin = true,
				IsSecret = false,
				IsActive = true
			});
			return true;
		}
		if (row.IsDeleted || string.Equals(row.SettingValue, value, StringComparison.Ordinal))
		{
			// A soft-deleted row means someone removed the setting on purpose; leave it alone.
			return false;
		}
		row.SettingValue = value;
		return true;
	}

	/// <summary>Drops the SettingService read cache ("appsetting:" + key, as SettingAdminService
	/// does) so the next form-options call sees the new values. Call after the commit.</summary>
	public static void Evict(IMemoryCache cache)
	{
		cache.Remove("appsetting:" + TermsKey);
		cache.Remove("appsetting:" + LocationKey);
		cache.Remove("appsetting:" + AddressKey);
	}
}
