using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Contracts.Administration;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps between the Administration Contracts DTOs and the Application admin request/read-model
/// shapes at the API boundary (requirement A2 §5.3, §5.9, §5.10, §5.12).
/// </summary>
/// <remarks>
/// A thin, pure boundary translation (coding-standards §3): it projects the settings, staff-user,
/// and audit read models onto their Contracts responses and parses the edit/create contracts into
/// Application requests. It carries no business logic and performs no I/O, so it is exercised
/// directly in a unit test. A role string that names no known role raises a
/// <see cref="CatalogContractFormatException"/> so the endpoint returns a consistent <c>400</c>.
/// </remarks>
public static class AdministrationContractMapper
{
    /// <summary>Projects the business-settings view onto its Contracts response.</summary>
    /// <param name="view">The Application read model.</param>
    /// <returns>The versioned transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static BusinessSettingsResponse ToResponse(BusinessSettingsView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new BusinessSettingsResponse
        {
            Timezone = view.Timezone,
            Currency = view.Currency,
            DefaultLaborRate = view.DefaultLaborRate,
            DefaultOverheadRate = view.DefaultOverheadRate,
            DefaultDimensionUnit = view.DefaultDimensionUnit,
            ImageSizeLimitBytes = view.ImageSizeLimitBytes,
            VideoSizeLimitBytes = view.VideoSizeLimitBytes,
            ETag = view.ETagToken,
        };
    }

    /// <summary>Parses a settings edit contract into the Application update request.</summary>
    /// <param name="body">The request body carrying the editable settings.</param>
    /// <param name="expectedVersion">The <c>If-Match</c> concurrency token from the request header.</param>
    /// <returns>The Application update request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="body"/> is <see langword="null"/>.</exception>
    public static UpdateBusinessSettingsRequest ToRequest(UpdateBusinessSettingsContract body, string expectedVersion)
    {
        System.ArgumentNullException.ThrowIfNull(body);

        return new UpdateBusinessSettingsRequest
        {
            Timezone = body.Timezone,
            Currency = body.Currency,
            DefaultLaborRate = body.DefaultLaborRate,
            DefaultOverheadRate = body.DefaultOverheadRate,
            DefaultDimensionUnit = body.DefaultDimensionUnit,
            ImageSizeLimitBytes = body.ImageSizeLimitBytes,
            VideoSizeLimitBytes = body.VideoSizeLimitBytes,
            ExpectedVersion = expectedVersion,
        };
    }

    /// <summary>Projects a staff-user view onto its Contracts response.</summary>
    /// <param name="view">The Application read model.</param>
    /// <returns>The versioned transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static StaffUserResponse ToResponse(StaffUserView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new StaffUserResponse
        {
            UserId = view.UserId,
            Email = view.Email,
            Role = RoleNames.FromRole(view.Role),
        };
    }

    /// <summary>Parses a staff-user creation contract into the Application request.</summary>
    /// <param name="body">The request body carrying the new account's email, password, and role.</param>
    /// <returns>The Application create request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="body"/> is <see langword="null"/>.</exception>
    /// <exception cref="CatalogContractFormatException">Thrown when the role string names no known value.</exception>
    public static CreateStaffUserRequest ToRequest(CreateStaffUserContract body)
    {
        System.ArgumentNullException.ThrowIfNull(body);

        return new CreateStaffUserRequest(body.Email, body.Password, ParseRole(body.Role));
    }

    /// <summary>Projects an audit-entry view onto its Contracts response.</summary>
    /// <param name="view">The Application read model.</param>
    /// <returns>The versioned transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static AuditEntryResponse ToResponse(AuditEntryView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new AuditEntryResponse
        {
            AuditEntryId = view.AuditEntryId,
            ActorId = view.ActorId,
            Action = view.Action,
            EntityType = view.EntityType,
            EntityId = view.EntityId,
            Timestamp = view.Timestamp,
            PermittedChangeSummary = view.PermittedChangeSummary,
        };
    }

    private static Role ParseRole(string value)
    {
        if (string.Equals(value, RoleNames.Owner, System.StringComparison.OrdinalIgnoreCase))
        {
            return Role.Owner;
        }

        if (string.Equals(value, RoleNames.Staff, System.StringComparison.OrdinalIgnoreCase))
        {
            return Role.Staff;
        }

        throw new CatalogContractFormatException(
            $"'{value}' is not a recognized role; expected '{RoleNames.Owner}' or '{RoleNames.Staff}'.");
    }
}
