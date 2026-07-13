namespace NotifyHub.Api.Dashboard.Dtos;

public class TaskCountsDto
{
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Escalated { get; set; }
    public int Overdue { get; set; }
}
