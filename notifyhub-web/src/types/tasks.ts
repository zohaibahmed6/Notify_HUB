export type TaskPriority = "Low" | "Medium" | "High" | "Urgent";
export type TaskStatus = "Open" | "InProgress" | "Completed" | "Escalated" | "Cancelled";
export type TaskType =
  | "RepeatRx"
  | "Recall"
  | "AppointmentBooking"
  | "FollowUp"
  | "Finance"
  | "General"
  | "ClinicalReview"
  | "Administrative"
  | "Other";

export const TASK_TYPES: TaskType[] = [
  "RepeatRx",
  "Recall",
  "AppointmentBooking",
  "FollowUp",
  "Finance",
  "General",
  "ClinicalReview",
  "Administrative",
  "Other",
];

export interface TaskDto {
  id: number;
  threadId: number;
  patientName: string;
  priority: TaskPriority;
  dueAt: string;
  status: TaskStatus;
  assignedStaffId: number | null;
  assignedStaffUsername: string | null;
  originalOwnerId: number;
  isRecurring: boolean;
  recurrenceIntervalDays: number | null;
  recurrenceEndDate: string | null;
  recurrenceMaxOccurrences: number | null;
  occurrenceCount: number;
  description: string | null;
  taskType: TaskType;
  isActive: boolean;
}

export interface CreateTaskRequest {
  priority?: TaskPriority;
  dueAt?: string;
  isRecurring?: boolean;
  recurrenceIntervalDays?: number;
  recurrenceEndDate?: string;
  recurrenceMaxOccurrences?: number;
  description?: string;
  taskType?: TaskType;
  assignedStaffId?: number;
}

export interface UpdateTaskRequest {
  status?: TaskStatus;
  priority?: TaskPriority;
  dueAt?: string;
  assignedStaffId?: number;
  description?: string;
  taskType?: TaskType;
  isActive?: boolean;
}

export interface ForwardTaskRequest {
  targetUserId: number;
  note?: string;
}

export type TaskSortBy = "dueAt" | "priority" | "status" | "patientName" | "assignedStaffUsername";
export type TaskSortDir = "asc" | "desc";

export interface TaskListFilters {
  status?: TaskStatus | "All";
  /** Comma-joined server-side, e.g. for TaskNavWidget's "Open/InProgress/Escalated" badge
   * set — wins over `status` when both are present. Lets a caller filter to a status set
   * without fetching everything and filtering client-side (which silently misses rows once
   * the true match count exceeds `pageSize`, see TaskNavWidget's bug history). */
  statuses?: TaskStatus[];
  assignedStaffId?: number;
  description?: string;
  patientName?: string;
  dueFrom?: string;
  dueTo?: string;
  isActive?: boolean;
  priority?: TaskPriority;
  isRecurring?: boolean;
  unassigned?: boolean;
  sortBy?: TaskSortBy;
  sortDir?: TaskSortDir;
  page?: number;
  pageSize?: number;
}
