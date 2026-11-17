using System.Security.Cryptography;
using System.Text;
using CsCheck;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Application.Integrations.Webhooks;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>
/// Property 20 (requirements 3.7, 9.2, 9.3): <em>Forged provider callbacks are rejected.</em>
/// For any payload and any signature that is not the valid HMAC of that payload (random text,
/// missing, valid for a different body, signed with a different secret, truncated, or with one
/// character altered), <see cref="ReceiveWebhookUseCase"/> rejects the callback and writes nothing
/// to the payload store or the inbox. A correctly signed callback is recorded.
/// </summary>
public class ForgedWebhookPropertyTests
{
    private const int Iterations = 1000;
    private const string Provider = "shopify";
    private const string SignatureHeader = "X-Signature";
    private const string DeliveryHeader = "X-Delivery";
    private const int ForgeryKindCount = 6;

    private static readonly byte[] s_secret = Encoding.UTF8.GetBytes("property-test-secret");

    private static readonly Gen<byte[]> s_genPayload = Gen.Byte.Array[0, 256];

    private static readonly Gen<(byte[] Payload, int Kind, string Text, int Position)> s_genForgery =
        Gen.Select(
            s_genPayload,
            Gen.Int[0, ForgeryKindCount - 1],
            Gen.String[Gen.Char.AlphaNumeric, 0, 100],
            Gen.Int[0, 1000]);

    /// <summary>Feature: penguin-plank-phase-a, Property 20: Forged provider callbacks are rejected.</summary>
    /// <remarks><b>Validates: Requirements 3.7, 9.2, 9.3</b></remarks>
    [Fact]
    public async Task ReceiveAsync_AnyForgedSignature_RejectsAndRecordsNothingAsync()
    {
        await s_genForgery.SampleAsync(
            async forgery =>
            {
                byte[] validSignatureInput = forgery.Payload;
                string? signature = Forge(forgery.Payload, forgery.Kind, forgery.Text, forgery.Position);
                if (signature is not null && IsAcceptable(s_secret, validSignatureInput, signature))
                {
                    return; // The forgery happened to equal a valid signature; not a forgery.
                }

                Harness harness = new();
                WebhookCallback callback = Callback(forgery.Payload, signature, "d-1");

                WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(callback, CancellationToken.None);

                Assert.Equal(WebhookIntakeOutcome.Rejected, outcome);
                Assert.Empty(harness.Inbox.Recorded);
                Assert.Empty(harness.Payloads.Saved);
            },
            iter: Iterations);
    }

    /// <summary>Feature: penguin-plank-phase-a, Property 20: valid signatures are recorded.</summary>
    /// <remarks><b>Validates: Requirements 3.7, 9.2, 9.3</b></remarks>
    [Fact]
    public async Task ReceiveAsync_AnyPayloadWithValidSignature_IsRecordedAsync()
    {
        await s_genPayload.SampleAsync(
            async payload =>
            {
                Harness harness = new();
                string signature = Convert.ToHexStringLower(HMACSHA256.HashData(s_secret, payload));

                WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(
                    Callback(payload, signature, "d-1"),
                    CancellationToken.None);

                Assert.Equal(WebhookIntakeOutcome.Recorded, outcome);
                Assert.Single(harness.Inbox.Recorded);
                Assert.Single(harness.Payloads.Saved);
            },
            iter: Iterations);
    }

    private static bool IsAcceptable(byte[] secret, byte[] payload, string signature)
        => HmacSha256Signature.IsValid(secret, payload, signature);

    /// <summary>Builds a signature that is intended to be invalid for the payload, or null for a missing one.</summary>
    private static string? Forge(byte[] payload, int kind, string text, int position)
    {
        string valid = Convert.ToHexStringLower(HMACSHA256.HashData(s_secret, payload));
        switch (kind)
        {
            case 0:
                return text;
            case 1:
                return null;
            case 2:
                byte[] other = [.. payload, (byte)position];
                return Convert.ToHexStringLower(HMACSHA256.HashData(s_secret, other));
            case 3:
                byte[] wrongSecret = Encoding.UTF8.GetBytes("wrong-secret" + text);
                return "sha256=" + Convert.ToBase64String(HMACSHA256.HashData(wrongSecret, payload));
            case 4:
                return valid[..(position % valid.Length)];
            default:
                int index = position % valid.Length;
                char replacement = valid[index] == '0' ? '1' : '0';
                return string.Concat(valid.AsSpan(0, index), replacement.ToString(), valid.AsSpan(index + 1));
        }
    }

    private static WebhookCallback Callback(byte[] payload, string? signature, string deliveryKey)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DeliveryHeader] = deliveryKey,
        };
        if (signature is not null)
        {
            headers[SignatureHeader] = signature;
        }

        return new WebhookCallback(Provider, payload, headers);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Inbox = new RecordingInbox();
            Payloads = new RecordingPayloads();
            UseCase = new ReceiveWebhookUseCase(
                new HmacWebhookSignatureVerifier(Provider, s_secret, SignatureHeader, DeliveryHeader),
                new SingleConnectionStore(Guid.NewGuid()),
                Payloads,
                Inbox,
                new WebhookIntakeOptions(1024));
        }

        public RecordingInbox Inbox { get; }

        public RecordingPayloads Payloads { get; }

        public ReceiveWebhookUseCase UseCase { get; }
    }

    private sealed class RecordingInbox : IInboxStore
    {
        public List<InboundDelivery> Recorded { get; } = [];

        public Task<bool> TryRecordAsync(InboundDelivery delivery, CancellationToken cancellationToken)
        {
            Recorded.Add(delivery);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingPayloads : IWebhookPayloadStore
    {
        public List<PayloadRef> Saved { get; } = [];

        public Task SaveIfAbsentAsync(PayloadRef reference, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            Saved.Add(reference);
            return Task.CompletedTask;
        }
    }

    private sealed class SingleConnectionStore : IIntegrationConnectionStore
    {
        private readonly Guid _connectionId;

        public SingleConnectionStore(Guid connectionId)
        {
            _connectionId = connectionId;
        }

        public Task<IReadOnlyList<IntegrationConnectionSummary>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<IntegrationConnectionSummary> list =
            [
                new IntegrationConnectionSummary(
                    _connectionId, "Shopify", "shop", "", "v1", "", default, default, false, false, default, null),
            ];
            return Task.FromResult(list);
        }

        public Task<IntegrationConnectionSummary> RecordAsync(Guid connectionId, ConnectionRegistration registration, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IntegrationConnectionSummary?> GetAsync(Guid connectionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Result> UpdateHealthAsync(Guid connectionId, ConnectionHealth health, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
