namespace Regalis.Application.Abstractions;

/// <summary>Clock abstraction so services are testable. All timestamps are stored UTC
/// and rendered in IST by the frontend (A13).</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
