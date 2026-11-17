using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations.Webhooks;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the provider webhook intake (requirements 3.6, 3.7, 9.1 to 9.4). The signature
/// verifier is the disabled Phase A default, which rejects every callback until a provider adapter
/// is registered.
/// </summary>
public static class IntegrationWebhookServiceCollectionExtensions
{
    /// <summary>The configuration section that overrides <see cref="WebhookIntakeOptions"/>.</summary>
    public const string SectionName = "IntegrationWebhooks";

    /// <summary>Registers the webhook intake services.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configured payload limit is not positive.</exception>
    public static IServiceCollection AddPenguinPlankIntegrationWebhooks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        WebhookIntakeOptions options = configuration.GetSection(SectionName).Get<WebhookIntakeOptions>()
            ?? WebhookIntakeOptions.Default;
        if (options.MaxPayloadBytes <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:MaxPayloadBytes must be positive.");
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<IWebhookSignatureVerifier, DisabledWebhookSignatureVerifier>();
        services.TryAddSingleton<IWebhookPayloadStore, LocalWebhookPayloadStore>();
        services.TryAddScoped<ReceiveWebhookUseCase>();

        return services;
    }
}
