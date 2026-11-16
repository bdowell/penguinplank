using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Integrations;

/// <summary>Pure input validation for <see cref="EntityMappingRequest"/>.</summary>
public static class EntityMappingValidator
{
    /// <summary>Maximum Platform and OwnerType length, matching the column size.</summary>
    public const int MaxPlatformLength = 64;

    /// <summary>Maximum Account and ExternalId length, matching the column size.</summary>
    public const int MaxIdentifierLength = 200;

    /// <summary>Validates a mapping request without changing it.</summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>Success, or a <see cref="ErrorCode.Validation"/> failure naming the field.</returns>
    public static Result Validate(EntityMappingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConnectionId == Guid.Empty)
        {
            return Fail("ConnectionId");
        }

        if (request.InternalId == Guid.Empty)
        {
            return Fail("InternalId");
        }

        if (!IsValid(request.Platform, MaxPlatformLength))
        {
            return Fail("Platform");
        }

        if (!IsValid(request.Account, MaxIdentifierLength))
        {
            return Fail("Account");
        }

        if (!IsValid(request.ExternalId, MaxIdentifierLength))
        {
            return Fail("ExternalId");
        }

        return IsValid(request.OwnerType, MaxPlatformLength) ? Result.Success() : Fail("OwnerType");
    }

    private static bool IsValid(string value, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
    }

    private static Result Fail(string field)
    {
        return Result.Failure(ErrorCode.Validation, $"The mapping {field} is missing or invalid.");
    }
}
