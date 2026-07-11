export type TaskPriority = "Low" | "Medium" | "High" | "Urgent";
export type TaskStatus = "Open" | "InProgress" | "Completed" | "Escalated" | "Cancelled";

export interface TaskDto {
  id: number;
  threadId: number;
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
}

export interface CreateTaskRequest {
  priority?: TaskPriority;
  dueAt?: string;
  isRecurring?: boolean;
  recurrenceIntervalDays?: number;
  recurrenceEndDate?: string;
  recurrenceMaxOccurrences?: number;
}

export interface UpdateTaskRequest {
  status?: TaskStatus;
  priority?: TaskPriority;
  dueAt?: string;
  assignedStaffId?: number;
}
