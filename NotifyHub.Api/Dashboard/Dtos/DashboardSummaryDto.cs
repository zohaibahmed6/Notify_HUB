using NotifyHub.Api.Audit.Dtos;

namespace NotifyHub.Api.Dashboard.Dtos;

public class DashboardSummaryDto
{
    public TaskCountsDto MyTasks { get; set; } = default!;

    /// Null for non-Admins (§2: dashboards restrict org-wide task visibility the same way
    /// GET /api/audit does — Admin sees everything, Staff sees their own slice).
    public TaskCountsDto? OrgTasks { get; set; }

    public int UnreadThreadCount { get; set; }
    public IReadOnlyList<AuditLogDto> RecentActivity { get; set; } = [];
}
