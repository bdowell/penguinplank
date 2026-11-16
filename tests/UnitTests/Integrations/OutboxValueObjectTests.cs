using PenguinPlank.Application.Integrations;

namespace UnitTests.Integrations;

/// <summary>Unit tests for the outbox boundary value objects.</summary>
public class OutboxValueObjectTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OperationKey_EmptyOrWhitespace_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new OperationKey(value));
    }

    [Fact]
    public void OperationKey_TooLong_Throws()
    {
        string value = new('k', OperationKey.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => new OperationKey(value));
    }

    [Fact]
    public void OperationKey_AtMaxLength_KeepsValue()
    {
        string value = new('k', OperationKey.MaxLength);

        Assert.Equal(value, new OperationKey(value).Value);
    }

    [Fact]
    public void OperationKey_SameValue_AreEqual()
    {
        Assert.Equal(new OperationKey("op-1"), new OperationKey("op-1"));
    }
}
