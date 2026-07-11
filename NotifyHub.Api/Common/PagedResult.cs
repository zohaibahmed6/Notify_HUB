namespace NotifyHub.Api.Common;

/// §11a: default page size 25, max 100. Shared by /api/threads and /api/tasks (FR-010).
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }

    public static (int Page, int PageSize) Clamp(int page, int pageSize)
    {
        var clampedPage = page < 1 ? 1 : page;
        var clampedPageSize = pageSize < 1 ? 25 : Math.Min(pageSize, 100);
        return (clampedPage, clampedPageSize);
    }
}
