import type { AuditLogDto } from "@/types/audit";

export interface TaskCountsDto {
  open: number;
  inProgress: number;
  escalated: number;
  overdue: number;
}

export interface DashboardSummaryDto {
  myTasks: TaskCountsDto;
  orgTasks: TaskCountsDto | null;
  unreadThreadCount: number;
  recentActivity: AuditLogDto[];
}
