namespace NotifyHub.Api.Templates.Dtos;

public class TemplateDto
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string Body { get; set; } = default!;
    public int OffsetHours { get; set; }
    public bool IsActive { get; set; }
    public string CommunicationMode { get; set; } = default!;
    public IReadOnlyList<long> BookmarkIds { get; set; } = Array.Empty<long>();
}
