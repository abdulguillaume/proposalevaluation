using Microsoft.AspNetCore.Mvc;

namespace ProposalEval.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult Success(int code, object? data) =>
        StatusCode(code, new { code, data });

    protected ObjectResult Fail(int code, string error, object? data = null) =>
        StatusCode(code, new { code, data, error });
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        var size = pageSize < 1 ? 20 : pageSize;
        return new PagedResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = size,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)size)
        };
    }
}

public static class Paging
{
    public static (int PageNumber, int PageSize) Normalize(int pageNumber, int pageSize)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var size = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
        return (page, size);
    }
}
