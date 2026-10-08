using PenguinPlank.Application.Media;
using PenguinPlank.Contracts.Media;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps between the Media Contracts DTOs and the Application media request/read-model shapes at the
/// API boundary (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// A thin, pure boundary translation (coding-standards §3): it projects a
/// <see cref="MediaAssetView"/> onto a <see cref="MediaAssetResponse"/> and parses an edit
/// contract into an <see cref="UpdateMediaMetadataRequest"/>, raising a
/// <see cref="CatalogContractFormatException"/> when a visibility string names no known value so
/// the endpoint returns a consistent <c>400</c> rather than an unexpected fault. It carries no
/// business logic and performs no I/O, so it is exercised directly in a unit test. The response
/// deliberately omits the randomized storage key (requirement 6.9).
/// </remarks>
public static class MediaContractMapper
{
    /// <summary>Projects an Application media view onto its Contracts response.</summary>
    /// <param name="view">The Application read model.</param>
    /// <returns>The versioned transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static MediaAssetResponse ToResponse(MediaAssetView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new MediaAssetResponse
        {
            MediaAssetId = view.MediaAssetId,
            MimeType = view.MimeType,
            SizeBytes = view.SizeBytes,
            Checksum = view.Checksum,
            Caption = view.Caption,
            Role = view.Role,
            SortOrder = view.SortOrder,
            Visibility = view.Visibility.ToString(),
            CreatedAtUtc = view.CreatedAtUtc,
            UpdatedAtUtc = view.UpdatedAtUtc,
        };
    }

    /// <summary>Parses an edit contract into the Application update request.</summary>
    /// <param name="body">The request body carrying the editable metadata.</param>
    /// <param name="mediaAssetId">The asset id from the route.</param>
    /// <returns>The Application update request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="body"/> is <see langword="null"/>.</exception>
    /// <exception cref="CatalogContractFormatException">Thrown when the visibility string names no known value.</exception>
    public static UpdateMediaMetadataRequest ToRequest(UpdateMediaMetadataContract body, Guid mediaAssetId)
    {
        System.ArgumentNullException.ThrowIfNull(body);

        return new UpdateMediaMetadataRequest
        {
            MediaAssetId = mediaAssetId,
            Caption = body.Caption,
            Role = body.Role,
            SortOrder = body.SortOrder,
            Visibility = ParseVisibility(body.Visibility),
        };
    }

    private static MediaVisibility ParseVisibility(string value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out MediaVisibility visibility)
            && Enum.IsDefined(visibility))
        {
            return visibility;
        }

        throw new CatalogContractFormatException(
            $"'{value}' is not a recognized media visibility; expected 'Private' or 'PublicApproved'.");
    }
}
