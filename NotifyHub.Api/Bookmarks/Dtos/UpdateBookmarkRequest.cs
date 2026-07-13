namespace NotifyHub.Api.Bookmarks.Dtos;

/// PATCH semantics: only non-null fields are applied.
public class UpdateBookmarkRequest
{
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? InsertText { get; set; }
}
