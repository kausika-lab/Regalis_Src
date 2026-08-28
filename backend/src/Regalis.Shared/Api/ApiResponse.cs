namespace Regalis.Shared.Api;

/// <summary>
/// The standard response envelope returned by every endpoint.
/// Produced by <c>ApiControllerBase</c>, the validation filter and the exception
/// middleware — never hand-rolled inside a controller.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<ApiError> Errors { get; init; } = [];
    public string? CorrelationId { get; init; }

    public static ApiResponse Ok(string message = "Operation completed successfully.") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IReadOnlyList<ApiError>? errors = null, string? correlationId = null) =>
        new() { Success = false, Message = message, Errors = errors ?? [], CorrelationId = correlationId };

    public static ApiResponse Fail(string message, string errorCode, string errorMessage, string? field = null) =>
        Fail(message, [new ApiError(errorCode, errorMessage, field)]);
}

/// <summary>The standard response envelope carrying a payload.</summary>
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Operation completed successfully.") =>
        new() { Success = true, Message = message, Data = data };

    public new static ApiResponse<T> Fail(string message, IReadOnlyList<ApiError>? errors = null, string? correlationId = null) =>
        new() { Success = false, Message = message, Data = default, Errors = errors ?? [], CorrelationId = correlationId };
}

/// <summary>A single machine-readable error inside the envelope.</summary>
public record ApiError(string Code, string Message, string? Field = null);
