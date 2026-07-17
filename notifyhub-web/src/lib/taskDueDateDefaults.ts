import type { TaskPriority } from "@/types/tasks";

/** FR-008: system-suggested due date by priority, staff-overridable before saving — mirrors
 * `NotifyHub.Domain/Tasks/TaskDueDateDefaults.DefaultDueAt` exactly. Keep both in sync. */
const OFFSET_HOURS: Record<TaskPriority, number> = {
  Urgent: 4,
  High: 24,
  Medium: 24 * 3,
  Low: 24 * 7,
};

export function defaultDueAt(priority: TaskPriority, from: Date = new Date()): Date {
  return new Date(from.getTime() + OFFSET_HOURS[priority] * 60 * 60 * 1000);
}

function pad2(n: number): string {
  return String(n).padStart(2, "0");
}

/** "yyyy-MM-ddTHH:mm", local time, no timezone — same shape `DateTimePicker` uses. */
export function formatDateTimeLocal(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}T${pad2(date.getHours())}:${pad2(date.getMinutes())}`;
}
