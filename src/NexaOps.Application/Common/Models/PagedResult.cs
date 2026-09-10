namespace NexaOps.Application.Common.Models;

public class PageMetadata
{
    public int Total { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
    public int TotalPages { get; init; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public PageMetadata Meta { get; init; } = new();

    public int TotalCount => Meta.Total;
    public int Page => Meta.Page;
    public int PageSize => Meta.Limit;
    public int TotalPages => Meta.TotalPages;

    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        Meta = new PageMetadata
        {
            Total = totalCount,
            Page = page,
            Limit = pageSize,
            TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0
        };
    }
}
