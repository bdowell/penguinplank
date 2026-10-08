using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Infrastructure.Media;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the EF Core media metadata store and the media use cases (task 9.2) into the
/// dependency-injection container (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This completes the media composition the <see cref="MediaStorageServiceCollectionExtensions"/>
/// started: that extension registers the stateless <see cref="IFileStore"/> and its validated
/// options as singletons; this one binds the metadata persistence boundary
/// (<see cref="IMediaAssetStore"/>) to its EF implementation and registers the upload, download,
/// read, and edit use cases. It is still part of the single composition root; no business code
/// resolves a service at runtime (coding-standards §2).
/// </para>
/// <para>
/// Every registration here is <b>scoped</b>: the store captures the scoped
/// <c>PenguinPlankDbContext</c>, and each use case depends (transitively) on it, so a singleton
/// registration would capture a scoped context and is forbidden (coding-standards §2; requirement
/// A2 §5.14). The stateless <c>IFileStore</c>, identifier-generator, and <c>TimeProvider</c> seams
/// it composes are registered as singletons elsewhere, which this extension assumes have already
/// run.
/// </para>
/// </remarks>
public static class MediaServiceCollectionExtensions
{
    /// <summary>
    /// Binds the media metadata store and registers the media use cases.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankMedia(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Narrow metadata persistence boundary; captures the scoped DbContext.
        services.AddScoped<IMediaAssetStore, EfMediaAssetStore>();

        // Media use cases: plain orchestration classes resolved by constructor injection; scoped
        // because they depend on the scoped metadata store (upload/download/edit) or the file
        // store seam.
        services.AddScoped<UploadMediaUseCase>();
        services.AddScoped<DownloadMediaUseCase>();
        services.AddScoped<GetMediaMetadataUseCase>();
        services.AddScoped<UpdateMediaMetadataUseCase>();

        return services;
    }
}
