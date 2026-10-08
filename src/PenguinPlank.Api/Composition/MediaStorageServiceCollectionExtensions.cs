using PenguinPlank.Application.Media;
using PenguinPlank.Infrastructure.Media;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registrations for the media <see cref="IFileStore"/> and its validated typed
/// options (requirements 6.8â€“6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so media-storage wiring stays
/// localized and does not contend with the persistence/identity/authorization registrations being
/// edited in parallel. It is still part of the single composition root: no business code resolves
/// these services at runtime (coding-standards Â§2).
/// </para>
/// <para>
/// The <see cref="FileStoreOptions"/> are bound from the <see cref="FileStoreOptions.SectionName"/>
/// configuration section and <b>validated at startup</b> â€” a blank root path (which would risk
/// storing media inside the web root) fails fast rather than at the first upload (requirement 6.8).
/// Both the key generator and the local store are stateless and capture no scoped dependency, so
/// they are registered as <b>singletons</b> (requirement A2 Â§5.14).
/// </para>
/// </remarks>
public static class MediaStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the media file-store options, the randomized storage-key generator, and the local
    /// <see cref="IFileStore"/> implementation.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">
    /// Application configuration, read for the <see cref="FileStoreOptions.SectionName"/> section
    /// (the media root path â€” a directory outside the web root â€” and the image/video size bounds).
    /// </param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankMediaStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<FileStoreOptions>()
            .Bind(configuration.GetSection(FileStoreOptions.SectionName))
            .Validate(
                options => options.Validate(out _),
                "FileStore options are invalid: a RootPath outside the web root and positive size limits are required.")
            .ValidateOnStart();

        services.AddSingleton<IStorageKeyGenerator, GuidStorageKeyGenerator>();
        services.AddSingleton<IFileStore, LocalFileStore>();

        return services;
    }
}
