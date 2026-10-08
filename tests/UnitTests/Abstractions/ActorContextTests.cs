using PenguinPlank.Application.Abstractions;

namespace UnitTests.Abstractions;

/// <summary>
/// Behavior of the immutable <see cref="ActorContext"/> value object: user and system
/// actors, validation, and value equality. Pure, no I/O (coding-standards §1, §7).
/// </summary>
public class ActorContextTests
{
    [Fact]
    public void Constructor_UserActor_CarriesIdRoleAndNotSystemWorker()
    {
        var userId = Guid.NewGuid();

        var actor = new ActorContext(userId, Role.Staff);

        Assert.Equal(userId, actor.UserId);
        Assert.Equal(Role.Staff, actor.Role);
        Assert.False(actor.IsSystemWorker);
    }

    [Fact]
    public void Constructor_EmptyUserId_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new ActorContext(Guid.Empty, Role.Owner));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public void SystemWorker_FlagsSystemActorWithStableIdAndOwnerAuthority()
    {
        ActorContext actor = ActorContext.SystemWorker();

        Assert.True(actor.IsSystemWorker);
        Assert.Equal(ActorContext.SystemWorkerId, actor.UserId);
        Assert.Equal(Role.Owner, actor.Role);
    }

    [Fact]
    public void SystemWorker_UsesNonEmptyStableId()
    {
        Assert.NotEqual(Guid.Empty, ActorContext.SystemWorkerId);
        Assert.Equal(ActorContext.SystemWorkerId, ActorContext.SystemWorker().UserId);
    }

    [Fact]
    public void Equality_SameUserAndRole_AreEqual()
    {
        var userId = Guid.NewGuid();

        var first = new ActorContext(userId, Role.Owner);
        var second = new ActorContext(userId, Role.Owner);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equality_UserActorAndSystemWorker_AreNotEqual()
    {
        var userActor = new ActorContext(ActorContext.SystemWorkerId, Role.Owner);

        Assert.NotEqual(ActorContext.SystemWorker(), userActor);
    }
}
