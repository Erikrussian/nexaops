namespace NexaOps.Application.Common.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public PageMetadata Meta { get; init; } = new();
}

public class PageMetadata
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int Limit { get; init; }
    public int TotalPages { get; init; }
}
