using System.Text.Json;
using CsCheck;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>
/// Property 21 (requirements 3.2, 3.12, 8.5): <em>Credential secrets are never exposed.</em> For
/// any supplied external credential, no serialized form reachable by a caller contains the secret
/// value; only a credential reference appears.
/// </summary>
/// <remarks>
/// <para>
/// The system never accepts or stores a secret value, so secrecy is proven at the surfaces a
/// caller can reach without a database: (1) the redaction applied before an error enters the
/// sync-history exception queue or a log line, which must remove any secret embedded in provider
/// text; (2) the connection read model, whose serialized form must expose only the allowed fields
/// and never a credential reference or secret member; and (3) the persisted connection record,
/// whose only credential-bearing member is the reference. The EF stores that map the record to the
/// read model need SQL Server and are covered by the integration tests, not here.
/// </para>
/// <para>CsCheck (pinned in <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/> samples.</para>
/// </remarks>
public class CredentialSecrecyPropertyTests
{
    private const int Iterations = 500;

    private const string MessagePrefix = "Order sync failed: ";

    private const string MessageSuffix = " while contacting the platform";

    private static readonly string[] s_credentialKeys =
    [
        "token",
        "secret",
        "password",
        "passwd",
        "api_key",
        "api-key",
        "authorization",
        "credential",
        "signature",
    ];

    private static readonly string[] s_summaryJsonNames =
    [
        "id",
        "platform",
        "account",
        "scopes",
        "apiVersion",
        "enabledCapabilities",
        "syncDirection",
        "health",
        "enabled",
        "hasCredential",
        "credentialState",
        "credentialStateChangedAtUtc",
    ];

    private static readonly string[] s_forbiddenMemberFragments = ["secret", "password", "token", "reference"];

    /// <summary>Generates an arbitrary secret value that a provider message could echo back.</summary>
    private static readonly Gen<string> s_genSecret =
        Gen.String[Gen.Char.AlphaNumeric, 12, 48]
            .Where(secret => !IsContainedInFixedText(secret));

    /// <summary>Generates a long opaque token, the shape of an API key or access token.</summary>
    private static readonly Gen<string> s_genOpaqueToken =
        Gen.String[Gen.Char.AlphaNumeric, 32, 64]
            .Where(secret => !IsContainedInFixedText(secret));

    private static readonly Gen<IntegrationConnectionSummary> s_genSummary =
        Gen.Select(
                Gen.Guid,
                Gen.String[Gen.Char.AlphaNumeric, 0, 20],
                Gen.String[Gen.Char.AlphaNumeric, 0, 20],
                Gen.Bool,
                Gen.OneOfConst(Enum.GetValues<CredentialState>()),
                Gen.OneOfConst(Enum.GetValues<ConnectionHealth>()),
                Gen.OneOfConst(Enum.GetValues<SyncDirection>()))
            .Select((id, platform, account, hasCredential, credentialState, health, direction) =>
                new IntegrationConnectionSummary(
                    id,
                    platform,
                    account,
                    "read_orders",
                    "2025-01",
                    "orders",
                    direction,
                    health,
                    Enabled: false,
                    hasCredential,
                    credentialState,
                    CredentialStateChangedAtUtc: null));

    [Fact]
    public void Redact_SecretInCredentialPair_NeverAppearsInRedactedError()
    {
        Gen.Select(s_genSecret, Gen.Int[0, s_credentialKeys.Length - 1], Gen.Bool)
            .Sample(
                (secret, keyIndex, useColon) =>
                {
                    string separator = useColon ? ": " : "=";
                    string raw = MessagePrefix + s_credentialKeys[keyIndex] + separator + secret + MessageSuffix;

                    string redacted = SyncErrorRedactor.Redact(raw);

                    Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
                },
                iter: Iterations);
    }

    [Fact]
    public void Redact_SecretAsBearerToken_NeverAppearsInRedactedError()
    {
        s_genSecret.Sample(
            secret =>
            {
                string redacted = SyncErrorRedactor.Redact(MessagePrefix + "Bearer " + secret + MessageSuffix);

                Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
            },
            iter: Iterations);
    }

    [Fact]
    public void Redact_BareOpaqueToken_NeverAppearsInRedactedError()
    {
        s_genOpaqueToken.Sample(
            secret =>
            {
                string redacted = SyncErrorRedactor.Redact(MessagePrefix + secret + MessageSuffix);

                Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
            },
            iter: Iterations);
    }

    [Fact]
    public void Serialize_AnyConnectionSummary_ExposesOnlyAllowedFieldsAndNoReference()
    {
        s_genSummary.Sample(
            summary =>
            {
                using JsonDocument document = JsonDocument.Parse(
                    JsonSerializer.Serialize(summary, JsonSerializerOptions.Web));

                string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

                Assert.Equal(s_summaryJsonNames.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
                Assert.DoesNotContain(
                    names,
                    name => s_forbiddenMemberFragments.Any(
                        fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
            },
            iter: Iterations);
    }

    [Fact]
    public void ReadModels_PublicShape_CarryNoSecretOrReferenceMember()
    {
        Type[] readModels = [typeof(IntegrationConnectionSummary)];

        foreach (Type readModel in readModels)
        {
            Assert.DoesNotContain(
                readModel.GetProperties(),
                property => property.PropertyType == typeof(CredentialReference));
        }
    }

    [Fact]
    public void IntegrationConnection_PublicShape_OnlyCredentialReferenceCanCarryCredentialData()
    {
        string[] credentialMembers = typeof(IntegrationConnection)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("token", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(credentialMembers);
    }

    private static bool IsContainedInFixedText(string secret)
    {
        return MessagePrefix.Contains(secret, StringComparison.OrdinalIgnoreCase)
            || MessageSuffix.Contains(secret, StringComparison.OrdinalIgnoreCase);
    }
}
