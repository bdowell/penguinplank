using System.Security.Cryptography;
using System.Text;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Application.Integrations.Webhooks;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>Unit tests for webhook intake: signature, size, dedupe, and record-before-ack.</summary>
public class ReceiveWebhookUseCaseTests
{
    private static readonly byte[] s_secret = Encoding.UTF8.GetBytes("test-secret");

    [Fact]
    public async Task ReceiveAsync_ValidSignature_RecordsPayloadAndInboxAsync()
    {
        Harness harness = new();
        WebhookCallback callback = Signed("shopify", "{\"a\":1}", "d-1");

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(callback, CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.Recorded, outcome);
        InboundDelivery delivery = Assert.Single(harness.Inbox.Recorded);
        Assert.Equal("d-1", delivery.DeliveryKey.Value);
        Assert.Equal(harness.ConnectionId, delivery.ConnectionId);
        Assert.Equal(WebhookVerification.VerifiedSignatureState, delivery.SignatureState);
        Assert.Single(harness.Payloads.Saved);
    }

    [Fact]
    public async Task ReceiveAsync_RepeatedDeliveryKey_ReportsDuplicateAsync()
    {
        Harness harness = new();
        WebhookCallback callback = Signed("shopify", "{}", "d-1");

        await harness.UseCase.ReceiveAsync(callback, CancellationToken.None);
        WebhookIntakeOutcome second = await harness.UseCase.ReceiveAsync(callback, CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.Duplicate, second);
        Assert.Single(harness.Inbox.Recorded);
    }

    [Fact]
    public async Task ReceiveAsync_ForgedSignature_RejectsAndRecordsNothingAsync()
    {
        Harness harness = new();
        WebhookCallback signed = Signed("shopify", "{}", "d-1");
        var forged = signed with { Body = Encoding.UTF8.GetBytes("{\"tampered\":true}") };

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(forged, CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.Rejected, outcome);
        Assert.Empty(harness.Inbox.Recorded);
        Assert.Empty(harness.Payloads.Saved);
    }

    [Fact]
    public async Task ReceiveAsync_MissingSignature_RejectsAsync()
    {
        Harness harness = new();
        var callback = new WebhookCallback(
            "shopify",
            Encoding.UTF8.GetBytes("{}"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Delivery"] = "d-1" });

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(callback, CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.Rejected, outcome);
        Assert.Empty(harness.Inbox.Recorded);
    }

    [Fact]
    public async Task ReceiveAsync_DisabledVerifier_RejectsEverythingAsync()
    {
        Harness harness = new(new DisabledWebhookSignatureVerifier());

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(
            Signed("shopify", "{}", "d-1"),
            CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.Rejected, outcome);
        Assert.Empty(harness.Inbox.Recorded);
    }

    [Fact]
    public async Task ReceiveAsync_BodyOverLimit_RejectsBeforeVerificationAsync()
    {
        Harness harness = new(maxPayloadBytes: 4);

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(
            Signed("shopify", "12345", "d-1"),
            CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.PayloadTooLarge, outcome);
        Assert.Empty(harness.Inbox.Recorded);
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task ReceiveAsync_MalformedProvider_ReportsUnknownProviderAsync(string provider)
    {
        Harness harness = new();

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(
            Signed(provider, "{}", "d-1"),
            CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.UnknownProvider, outcome);
    }

    [Fact]
    public async Task ReceiveAsync_NoConnectionForProvider_ReportsUnknownConnectionAsync()
    {
        Harness harness = new(new HmacWebhookSignatureVerifier("etsy", s_secret, "X-Signature", "X-Delivery"));

        WebhookIntakeOutcome outcome = await harness.UseCase.ReceiveAsync(
            Signed("etsy", "{}", "d-1"),
            CancellationToken.None);

        Assert.Equal(WebhookIntakeOutcome.UnknownConnection, outcome);
        Assert.Empty(harness.Inbox.Recorded);
    }

    [Fact]
    public async Task ReceiveAsync_InboxWriteFails_PropagatesSoNothingIsAcknowledgedAsync()
    {
        Harness harness = new();
        harness.Inbox.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.UseCase.ReceiveAsync(Signed("shopify", "{}", "d-1"), CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HmacSha256Signature_HexOrBase64WithOptionalPrefix_Validates(bool hex)
    {
        byte[] body = Encoding.UTF8.GetBytes("payload");
        byte[] mac = HMACSHA256.HashData(s_secret, body);
        string signature = hex ? Convert.ToHexStringLower(mac) : "sha256=" + Convert.ToBase64String(mac);

        Assert.True(HmacSha256Signature.IsValid(s_secret, body, signature));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-signature")]
    [InlineData("sha256=00")]
    public void HmacSha256Signature_MissingOrMalformed_IsInvalid(string? signature)
    {
        Assert.False(HmacSha256Signature.IsValid(s_secret, Encoding.UTF8.GetBytes("payload"), signature));
    }

    [Fact]
    public void HmacSha256Signature_WrongSecret_IsInvalid()
    {
        byte[] body = Encoding.UTF8.GetBytes("payload");
        string signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("other"), body));

        Assert.False(HmacSha256Signature.IsValid(s_secret, body, signature));
    }

    private static WebhookCallback Signed(string provider, string body, string deliveryKey)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        string signature = Convert.ToHexStringLower(HMACSHA256.HashData(s_secret, bytes));
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Signature"] = signature,
            ["X-Delivery"] = deliveryKey,
        };
        return new WebhookCallback(provider, bytes, headers);
    }

    private sealed class Harness
    {
        public Harness(IWebhookSignatureVerifier? verifier = null, int maxPayloadBytes = 1024)
        {
            Inbox = new FakeInbox();
            Payloads = new FakePayloads();
            UseCase = new ReceiveWebhookUseCase(
                verifier ?? new HmacWebhookSignatureVerifier("shopify", s_secret, "X-Signature", "X-Delivery"),
                new FakeConnections(ConnectionId),
                Payloads,
                Inbox,
                new WebhookIntakeOptions(maxPayloadBytes));
        }

        public Guid ConnectionId { get; } = Guid.NewGuid();

        public FakeInbox Inbox { get; }

        public FakePayloads Payloads { get; }

        public ReceiveWebhookUseCase UseCase { get; }
    }

    private sealed class FakeInbox : IInboxStore
    {
        public List<InboundDelivery> Recorded { get; } = [];

        public bool Fail { get; set; }

        public Task<bool> TryRecordAsync(InboundDelivery delivery, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("write failed");
            }

            if (Recorded.Any(item => item.DeliveryKey == delivery.DeliveryKey))
            {
                return Task.FromResult(false);
            }

            Recorded.Add(delivery);
            return Task.FromResult(true);
        }
    }

    private sealed class FakePayloads : IWebhookPayloadStore
    {
        public List<PayloadRef> Saved { get; } = [];

        public Task SaveIfAbsentAsync(PayloadRef reference, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            if (!Saved.Contains(reference))
            {
                Saved.Add(reference);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeConnections : IIntegrationConnectionStore
    {
        private readonly Guid _shopifyId;

        public FakeConnections(Guid shopifyId)
        {
            _shopifyId = shopifyId;
        }

        public Task<IReadOnlyList<IntegrationConnectionSummary>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<IntegrationConnectionSummary> list =
            [
                new IntegrationConnectionSummary(
                    _shopifyId, "Shopify", "shop", "", "v1", "", default, default, false, false, default, null),
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
