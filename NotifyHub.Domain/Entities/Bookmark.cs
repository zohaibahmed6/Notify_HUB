namespace NotifyHub.Domain.Entities;

/// §5: an admin-managed library of reusable snippets/merge-field shortcuts, selectable
/// from a dropdown while creating/editing a template (e.g. Label="Patient Name",
/// InsertText="{{patient_name}}").
public class Bookmark
{
    public long Id { get; set; }
    public string Label { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string InsertText { get; set; } = default!;

    public ICollection<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();
}
