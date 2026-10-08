using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 4 (task 7.8, requirements R01 Â§1.12, R10 Â§2.6, Â§2.7): <em>Draft default and no
/// automatic publication.</em> <see cref="PublicationPolicy"/> is a pure static policy
/// (coding-standards Â§1, Â§7): every decision is a function of explicit <see cref="PublicationState"/>
/// and <see cref="PublicationAction"/> inputs and performs no I/O, so these properties exercise the
/// business rule with ordinary values and need no database, <c>HttpContext</c>, or application
/// startup. They assert three guarantees:
/// <list type="number">
///   <item><b>Default is Draft.</b> <see cref="PublicationPolicy.InitialState"/> is
///   <see cref="PublicationState.Draft"/>; <see cref="PublicationPolicy.AssertDefaultForNewItem"/>
///   succeeds only for <see cref="PublicationState.Draft"/> and otherwise fails with
///   <see cref="ErrorCode.Validation"/>.</item>
///   <item><b>No automatic publication.</b> A <see cref="PublicationPolicy.Transition"/> yields
///   <see cref="PublicationState.PublicApproved"/> <em>iff</em> the current state is
///   <see cref="PublicationState.Draft"/> and the action is <see cref="PublicationAction.Publish"/>.
///   Over a random sequence of actions starting from Draft the state becomes
///   <see cref="PublicationState.PublicApproved"/> only immediately after a successful explicit
///   Publish â€” never spontaneously.</item>
///   <item><b>Transition truth table.</b> Draft+Publish â‡’ PublicApproved; PublicApproved+Withdraw â‡’
///   Draft; Draft+Withdraw and PublicApproved+Publish â‡’ <see cref="ErrorCode.Validation"/> failure.</item>
/// </list>
/// </summary>
/// <remarks>
/// The single-step input space is the four-combination cross product of
/// (<see cref="PublicationState"/> Ã— <see cref="PublicationAction"/>), so it is both enumerated
/// exhaustively as a theory and sampled by CsCheck (pinned in <c>UnitTests.csproj</c>). The
/// no-auto-publish guarantee is additionally proven by replaying a random sequence of actions and
/// asserting PublicApproved is reached only right after a successful Publish. All CsCheck
/// properties run at <see cref="Iterations"/> samples (â‰¥100 per the design floor).
///
/// <b>Validates: Requirements 1.12, 2.6, 2.7</b>
/// </remarks>
public class PublicationPolicyPropertyTests
{
    /// <summary>
    /// Iterations per CsCheck property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor so the small input space and the action
    /// sequences are sampled many times over.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>Generates one of the two publication states.</summary>
    private static readonly Gen<PublicationState> s_genState =
        Gen.Int[0, 1].Select(index => (PublicationState)index);

    /// <summary>Generates one of the two publication actions.</summary>
    private static readonly Gen<PublicationAction> s_genAction =
        Gen.Int[0, 1].Select(index => (PublicationAction)index);

    /// <summary>Generates the full single-step input tuple over the (state Ã— action) cross product.</summary>
    private static readonly Gen<(PublicationState Current, PublicationAction Action)> s_genInput =
        Gen.Select(s_genState, s_genAction);

    /// <summary>Generates a random sequence of 0..20 actions used to prove no spontaneous publication.</summary>
    private static readonly Gen<PublicationAction[]> s_genActionSequence =
        s_genAction.Array[0, 20];

    [Fact]
    public void InitialState_ForAnyNewCatalogRecord_IsDraft()
    {
        Assert.Equal(PublicationState.Draft, PublicationPolicy.InitialState);
    }

    [Fact]
    public void AssertDefaultForNewItem_Draft_Succeeds()
    {
        Result result = PublicationPolicy.AssertDefaultForNewItem(PublicationState.Draft);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AssertDefaultForNewItem_PublicApproved_FailsValidation()
    {
        Result result = PublicationPolicy.AssertDefaultForNewItem(PublicationState.PublicApproved);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    [Fact]
    public void AssertDefaultForNewItem_AnyCandidate_SucceedsExactlyWhenDraft()
    {
        s_genState.Sample(
            candidate =>
            {
                Result result = PublicationPolicy.AssertDefaultForNewItem(candidate);

                if (candidate == PublicationState.Draft)
                {
                    Assert.True(result.IsSuccess);
                }
                else
                {
                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.Validation, result.Error.Code);
                    Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
                }
            },
            iter: Iterations);
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Transition_EveryCombination_MatchesTruthTable(
        PublicationState current,
        PublicationAction action)
    {
        Result<PublicationState> result = PublicationPolicy.Transition(current, action);

        AssertMatchesTruthTable(current, action, result);
    }

    [Fact]
    public void Transition_AnyInput_MatchesTruthTable()
    {
        s_genInput.Sample(
            input =>
            {
                Result<PublicationState> result = PublicationPolicy.Transition(input.Current, input.Action);

                AssertMatchesTruthTable(input.Current, input.Action, result);
            },
            iter: Iterations);
    }

    [Fact]
    public void Transition_AnyInput_ReachesPublicApprovedOnlyFromDraftPublish()
    {
        s_genInput.Sample(
            input =>
            {
                Result<PublicationState> result = PublicationPolicy.Transition(input.Current, input.Action);

                bool reachesPublicApproved = result.IsSuccess && result.Value == PublicationState.PublicApproved;
                bool isDeliberateDraftPublish =
                    input.Current == PublicationState.Draft
                    && input.Action == PublicationAction.Publish;

                Assert.Equal(isDeliberateDraftPublish, reachesPublicApproved);
            },
            iter: Iterations);
    }

    [Fact]
    public void Transition_RandomActionSequenceFromDraft_BecomesPublicApprovedOnlyAfterExplicitPublish()
    {
        s_genActionSequence.Sample(
            actions =>
            {
                PublicationState state = PublicationPolicy.InitialState;
                Assert.Equal(PublicationState.Draft, state);

                foreach (PublicationAction action in actions)
                {
                    PublicationState before = state;
                    Result<PublicationState> result = PublicationPolicy.Transition(state, action);

                    if (result.IsSuccess)
                    {
                        state = result.Value;
                    }

                    // The state can only have advanced to PublicApproved on this step when the
                    // caller explicitly requested Publish from Draft. There is no path that
                    // promotes the record spontaneously.
                    if (state == PublicationState.PublicApproved && before != PublicationState.PublicApproved)
                    {
                        Assert.Equal(PublicationState.Draft, before);
                        Assert.Equal(PublicationAction.Publish, action);
                        Assert.True(result.IsSuccess);
                    }

                    // A failed transition never changes the state (the policy is pure).
                    if (result.IsFailure)
                    {
                        Assert.Equal(before, state);
                    }
                }
            },
            iter: Iterations);
    }

    /// <summary>The full four-combination (state Ã— action) cross product used by the exhaustive theory.</summary>
    public static TheoryData<PublicationState, PublicationAction> AllCombinations()
    {
        TheoryData<PublicationState, PublicationAction> data = new();

        PublicationState[] states = { PublicationState.Draft, PublicationState.PublicApproved };
        PublicationAction[] actions = { PublicationAction.Publish, PublicationAction.Withdraw };

        foreach (PublicationState current in states)
        {
            foreach (PublicationAction action in actions)
            {
                data.Add(current, action);
            }
        }

        return data;
    }

    /// <summary>
    /// Asserts the policy outcome against the specification truth table: Draft+Publish â‡’
    /// PublicApproved; PublicApproved+Withdraw â‡’ Draft; every other move is a
    /// <see cref="ErrorCode.Validation"/> failure that leaves no result value.
    /// </summary>
    private static void AssertMatchesTruthTable(
        PublicationState current,
        PublicationAction action,
        Result<PublicationState> result)
    {
        if (action == PublicationAction.Publish && current == PublicationState.Draft)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(PublicationState.PublicApproved, result.Value);
        }
        else if (action == PublicationAction.Withdraw && current == PublicationState.PublicApproved)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(PublicationState.Draft, result.Value);
        }
        else
        {
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCode.Validation, result.Error.Code);
            Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
        }
    }
}
