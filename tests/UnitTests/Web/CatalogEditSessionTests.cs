using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Web.Api;
using PenguinPlank.Web.Catalog;

namespace PenguinPlank.UnitTests.Web;

public sealed class CatalogEditSessionTests
{
    private static readonly UpdateProductContract s_edit = new() { Name = "Plank" };

    private sealed class SequentialKeys : IIdempotencyKeyGenerator
    {
        private int _next;

        public string NewKey() => $"key-{++_next}";
    }

    private sealed class RecordingSender
    {
        private readonly Queue<ApiResult> _results;

        public RecordingSender(params ApiResult[] results)
        {
            _results = new Queue<ApiResult>(results);
        }

        public List<(string ETag, string Key)> Calls { get; } = [];

        public Task<ApiResult> SendAsync(UpdateProductContract request, string eTag, string key, CancellationToken cancellationToken)
        {
            Calls.Add((eTag, key));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private static ApiResult Fail(ApiErrorKind kind) =>
        ApiResult.Failure(new ApiError(kind, null, null, "title", null, null));

    private static CatalogEditSession<UpdateProductContract> NewSession()
    {
        var session = new CatalogEditSession<UpdateProductContract>(new SequentialKeys());
        session.Load("v1");
        return session;
    }

    [Fact]
    public async Task SubmitAsync_Success_ReportsSavedAndDropsKeyAsync()
    {
        CatalogEditSession<UpdateProductContract> session = NewSession();
        var sender = new RecordingSender(ApiResult.Success());

        bool saved = await session.SubmitAsync(s_edit, sender.SendAsync);

        Assert.True(saved);
        Assert.Equal(CatalogEditStatus.Saved, session.Status);
        Assert.Null(session.PendingIdempotencyKey);
        Assert.Equal(("v1", "key-1"), Assert.Single(sender.Calls));
    }

    [Fact]
    public async Task SubmitAsync_StaleVersion_BlocksFurtherSubmitUntilReloadAsync()
    {
        CatalogEditSession<UpdateProductContract> session = NewSession();
        var sender = new RecordingSender(Fail(ApiErrorKind.StaleVersion));

        bool saved = await session.SubmitAsync(s_edit, sender.SendAsync);
        bool again = await session.SubmitAsync(s_edit, sender.SendAsync);

        Assert.False(saved);
        Assert.False(again);
        Assert.Equal(CatalogEditStatus.Conflict, session.Status);
        Assert.True(session.RequiresReload);
        Assert.Single(sender.Calls);

        session.Load("v2");

        Assert.True(session.CanSubmit);
    }

    [Fact]
    public async Task SubmitAsync_ConnectivityLossThenSameEdit_ReusesIdempotencyKeyAsync()
    {
        CatalogEditSession<UpdateProductContract> session = NewSession();
        var sender = new RecordingSender(Fail(ApiErrorKind.Connectivity), ApiResult.Success());

        await session.SubmitAsync(s_edit, sender.SendAsync);
        Assert.Equal(CatalogEditStatus.Unsaved, session.Status);
        Assert.Equal("key-1", session.PendingIdempotencyKey);
        await session.SubmitAsync(s_edit with { Name = "Plank" }, sender.SendAsync);

        Assert.Equal(["key-1", "key-1"], sender.Calls.Select(c => c.Key));
    }

    [Fact]
    public async Task SubmitAsync_ConnectivityLossThenChangedEdit_UsesNewKeyAsync()
    {
        CatalogEditSession<UpdateProductContract> session = NewSession();
        var sender = new RecordingSender(Fail(ApiErrorKind.Connectivity), ApiResult.Success());

        await session.SubmitAsync(s_edit, sender.SendAsync);
        await session.SubmitAsync(s_edit with { Name = "Board" }, sender.SendAsync);

        Assert.Equal(["key-1", "key-2"], sender.Calls.Select(c => c.Key));
    }

    [Fact]
    public async Task SubmitAsync_ValidationFailureThenResubmit_UsesNewKeyAsync()
    {
        CatalogEditSession<UpdateProductContract> session = NewSession();
        var sender = new RecordingSender(Fail(ApiErrorKind.Validation), ApiResult.Success());

        await session.SubmitAsync(s_edit, sender.SendAsync);
        await session.SubmitAsync(s_edit, sender.SendAsync);

        Assert.Equal(["key-1", "key-2"], sender.Calls.Select(c => c.Key));
    }

    [Fact]
    public async Task SubmitAsync_NoLoadedVersion_DoesNotSendAsync()
    {
        var session = new CatalogEditSession<UpdateProductContract>(new SequentialKeys());
        var sender = new RecordingSender();

        bool saved = await session.SubmitAsync(s_edit, sender.SendAsync);

        Assert.False(saved);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public void Describe_Connectivity_SaysNothingIsQueued()
    {
        string message = CatalogMessages.Describe(new ApiError(ApiErrorKind.Connectivity, null, null, "t", null, "corr-1"));

        Assert.Contains("not saved", message, StringComparison.Ordinal);
        Assert.Contains("nothing is queued", message, StringComparison.Ordinal);
        Assert.Contains("corr-1", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BlankNameAndDimensionWithoutUnit_ReportsProblems()
    {
        Assert.Single(new ProductDraft { Name = "  " }.Validate());

        var variant = new VariantDraft { UnitOfMeasure = "each", Length = 4m };

        Assert.Contains("Choose a unit for the dimensions.", variant.Validate());
    }

    [Fact]
    public void ToContract_BlankOptionalText_BecomesAbsent()
    {
        UpdateProductContract contract = new ProductDraft { Name = " Plank ", Category = "  " }.ToContract();

        Assert.Equal("Plank", contract.Name);
        Assert.Null(contract.Category);
    }

    [Fact]
    public async Task LoadAsync_ConnectivityFailure_KeepsPreviousItemsAndReportsErrorAsync()
    {
        var list = new CatalogListModel<string>();
        await list.LoadAsync(
            (_, _) => Task.FromResult(ApiResult.Success(new PagedResponse<string> { Items = ["a"], TotalCount = 41, PageNumber = 1, PageSize = 20 })),
            1);
        await list.LoadAsync(
            (_, _) => Task.FromResult(ApiResult.Failure<PagedResponse<string>>(new ApiError(ApiErrorKind.Connectivity, null, null, "t", null, null))),
            2);

        Assert.Equal(["a"], list.Items);
        Assert.NotNull(list.ErrorMessage);
        Assert.True(list.HasNext);
        Assert.False(list.IsEmpty);
    }
}
