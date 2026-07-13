namespace NotifyHub.Api.Bookmarks.Dtos;

public class CreateBookmarkRequest
{
    public string Label { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string InsertText { get; set; } = default!;
}
