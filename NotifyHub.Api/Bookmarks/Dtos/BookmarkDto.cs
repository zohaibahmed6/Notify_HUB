namespace NotifyHub.Api.Bookmarks.Dtos;

public class BookmarkDto
{
    public long Id { get; set; }
    public string Label { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string InsertText { get; set; } = default!;
}
