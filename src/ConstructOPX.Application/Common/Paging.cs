namespace ConstructOPX.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    /// <summary>The last page number accepted; far beyond any real list, and it keeps <see cref="Skip"/> inside an <c>int</c>.</summary>
    public const int MaximumPage = 1_000_000;

    public int SafePage => Math.Clamp(Page, 1, MaximumPage);
    public int SafePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaximumPageSize);
    public int Skip => (SafePage - 1) * SafePageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public static PagedResult<T> Of(IReadOnlyList<T> items, int total, PageRequest request) =>
        new(items, total, request.SafePage, request.SafePageSize);
}
