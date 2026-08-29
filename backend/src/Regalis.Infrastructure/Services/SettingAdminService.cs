using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Masters;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The Settings screen behind MST_AppSetting (M23, Design Invariant 6). Secret values
/// are masked on read and locked on write; every accepted change is validated against
/// the row's declared DataType before it is stored, and the read cache is evicted so
/// the engine sees the new value immediately.
/// </summary>
public class SettingAdminService(
    RegalisDbContext db,
    IMemoryCache cache,
    ISettingService settings) : ISettingAdminService
{
    private const string SecretMask = "••••";

    public async Task<List<AppSettingDto>> GetSettingsAsync(CancellationToken ct = default) =>
        (await db.AppSettings.AsNoTracking()
            .OrderBy(s => s.SettingGroup).ThenBy(s => s.SettingKey)
            .ToListAsync(ct))
        .Select(s => new AppSettingDto(
            s.AppSettingId, s.SettingKey, s.IsSecret ? SecretMask : s.SettingValue,
            s.SettingGroup, s.DataType, s.Description, s.IsEditableByAdmin, s.IsSecret))
        .ToList();

    public async Task<List<AppSettingDto>> UpdateSettingsAsync(UpdateAppSettingsRequest request,
        CancellationToken ct = default)
    {
        var keys = request.Settings.Select(s => s.SettingKey).ToList();
        var rows = await db.AppSettings
            .Where(s => keys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, ct);

        foreach (var (key, value) in request.Settings.Select(s => (s.SettingKey, s.SettingValue)))
        {
            if (!rows.TryGetValue(key, out var row))
            {
                throw new NotFoundException("AppSetting", key);
            }
            if (!row.IsEditableByAdmin || row.IsSecret)
            {
                throw new BusinessRuleException($"{key} is not editable from the Settings screen.");
            }
            ValidateDataType(row.SettingKey, row.DataType, value);
            row.SettingValue = value.Trim();
        }
        await db.SaveChangesAsync(ct);

        foreach (var key in keys)
        {
            cache.Remove($"appsetting:{key}");
        }
        return await GetSettingsAsync(ct);
    }

    public async Task<PublicSettingsDto> GetPublicSettingsAsync(CancellationToken ct = default) => new(
        CompanyName: await settings.GetStringAsync(AppSettingKeys.Company.Name, "Regalis WorkSpace", ct),
        CompanyEmail: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Company.Email, "", ct)),
        CompanyPhone: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Company.Phone, "", ct)),
        CompanyAddress: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct)),
        SupportContactNumber: NullIfEmpty(
            await settings.GetStringAsync(AppSettingKeys.Support.ContactNumber, "", ct)),
        SupportEmail: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Support.Email, "", ct)),
        SupportHours: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Support.Hours, "", ct)),
        SupportMessage: NullIfEmpty(await settings.GetStringAsync(AppSettingKeys.Support.Message, "", ct)));

    private static void ValidateDataType(string key, string dataType, string value)
    {
        var ok = dataType switch
        {
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "bool" => bool.TryParse(value, out _),
            "time" => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            _ => true
        };
        if (!ok)
        {
            throw new BusinessRuleException($"{key} must be a valid {dataType} value.");
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
