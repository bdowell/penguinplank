using PenguinPlank.Application.Abstractions;

namespace UnitTests.Abstractions;

/// <summary>
/// Confirms the time convention reads the current instant from an injected
/// <see cref="TimeProvider"/> rather than a real clock (requirement A8 §10.1;
/// coding-standards §2). Uses a controllable fixed provider — no real clock, no sleep.
/// </summary>
public class TimeConventionTests
{
    [Fact]
    public void UtcNow_ReturnsInstantFromInjectedProvider()
    {
        var fixedInstant = new DateTimeOffset(2025, 3, 14, 9, 26, 53, TimeSpan.Zero);
        var provider = new FixedTimeProvider(fixedInstant);

        DateTimeOffset result = TimeConvention.UtcNow(provider);

        Assert.Equal(fixedInstant, result);
    }

    [Fact]
    public void UtcNow_NullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TimeConvention.UtcNow(null!));
    }

    /// <summary>A deterministic <see cref="TimeProvider"/> that always reports a fixed instant.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _instant;

        public FixedTimeProvider(DateTimeOffset instant)
        {
            _instant = instant;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _instant;
        }
    }
}
