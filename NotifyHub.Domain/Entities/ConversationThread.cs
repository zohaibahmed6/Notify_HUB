namespace NotifyHub.Domain.Entities;

/// Named ConversationThread (not Thread) to avoid clashing with System.Threading.Thread,
/// same reasoning as TaskItem vs System.Threading.Tasks.Task. Maps to the `threads` table
/// (§7). One thread per patient (patient_id unique), created via find-or-create on first
/// inbound message to prevent race-condition duplicates.
public class ConversationThread
{
    public long Id { get; set; }

    public long PatientId { get; set; }
    public Patient Patient { get; set; } = default!;

    public long? AssignedStaffId { get; set; }
    public User? AssignedStaff { get; set; }

    public int UnreadCount { get; set; }

    public ICollection<InboundMessage> InboundMessages { get; set; } = new List<InboundMessage>();
    public ICollection<OutboundMessage> OutboundMessages { get; set; } = new List<OutboundMessage>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
