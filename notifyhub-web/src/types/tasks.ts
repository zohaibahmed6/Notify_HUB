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

export interface TaskListFilters {
  status?: TaskStatus | "All";
  assignedStaffId?: number;
  description?: string;
  patientName?: string;
  dueFrom?: string;
  dueTo?: string;
  isActive?: boolean;
}
