using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Application.Common.Exceptions;

/// <summary>Base for exceptions the middleware maps to a specific HTTP status
/// and error code (ARCHITECTURE §9). Stack traces reach the log, never the client.</summary>
public abstract class AppException(string message, string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public virtual IReadOnlyList<ApiError> ToErrors() => [new ApiError(ErrorCode, Message)];
}

/// <summary>→ 400 with field-level errors.</summary>
public class AppValidationException(IReadOnlyList<ApiError> errors)
    : AppException("One or more validation errors occurred.", ErrorCodes.ValidationError)
{
    public IReadOnlyList<ApiError> Errors { get; } = errors;
    public override IReadOnlyList<ApiError> ToErrors() => Errors;
}

/// <summary>→ 401.</summary>
public class UnauthorizedException(string message = "Authentication failed.", string errorCode = ErrorCodes.AuthenticationFailed)
    : AppException(message, errorCode);

/// <summary>→ 403.</summary>
public class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message, ErrorCodes.AuthorizationFailed);

/// <summary>→ 404.</summary>
public class NotFoundException(string entityName, object key)
    : AppException($"{entityName} with id '{key}' was not found.", ErrorCodes.NotFound);

/// <summary>→ 409 — used by the booking engine when a slot is lost in a race.</summary>
public class ConflictException(string message, string errorCode = ErrorCodes.SlotUnavailable)
    : AppException(message, errorCode);

/// <summary>→ 422.</summary>
public class BusinessRuleException(string message, string errorCode = ErrorCodes.BusinessRuleViolation)
    : AppException(message, errorCode);
