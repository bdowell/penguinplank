using PenguinPlank.Application.Integrations;

namespace UnitTests.Integrations;

/// <summary>Unit tests for the inbox boundary value objects.</summary>
public class InboxValueObjectTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeliveryKey_EmptyOrWhitespace_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new DeliveryKey(value));
    }

    [Fact]
    public void DeliveryKey_TooLong_Throws()
    {
        string value = new('k', DeliveryKey.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => new DeliveryKey(value));
    }

    [Fact]
    public void DeliveryKey_AtMaxLength_KeepsValue()
    {
        string value = new('k', DeliveryKey.MaxLength);

        Assert.Equal(value, new DeliveryKey(value).Value);
    }

    [Fact]
    public void DeliveryKey_SameValue_AreEqual()
    {
        Assert.Equal(new DeliveryKey("evt-1"), new DeliveryKey("evt-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void PayloadRef_EmptyOrWhitespace_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new PayloadRef(value));
    }

    [Fact]
    public void PayloadRef_TooLong_Throws()
    {
        string value = new('p', PayloadRef.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => new PayloadRef(value));
    }
}
