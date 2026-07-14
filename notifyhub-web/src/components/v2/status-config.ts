import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  ArrowUpCircle,
  Ban,
  Calendar,
  Check,
  CheckCheck,
  Clock,
  ClipboardList,
  CornerUpRight,
  Hourglass,
  Minus,
  Pill,
  Send,
  ShieldAlert,
  UserPlus,
  XCircle,
} from "lucide-react";

import type { StatusBadgeConfig, StatusTone } from "@/components/v2/status-badge";
import type { TaskPriority, TaskStatus } from "@/types/tasks";
import type { TemplateTriggerType } from "@/types/templates";

// Solid-fill counterpart to StatusBadge's pastel tones, for small-area marks (segmented
// distribution bars, sparkline bars) where a soft badge background would be invisible.
export const STATUS_TONE_BAR_CLASS: Record<StatusTone, string> = {
  neutral: "bg-slate-300 dark:bg-slate-600",
  progress: "bg-amber-400 dark:bg-amber-500",
  success: "bg-emerald-400 dark:bg-emerald-500",
  danger: "bg-red-400 dark:bg-red-500",
  info: "bg-blue-400 dark:bg-blue-500",
  muted: "bg-purple-300 dark:bg-purple-600",
};

// Keyed on ThreadMessageDto.status (NotifyHub.Domain MessageStatus, serialized as the
// C# enum member name — see NotifyHub.Api DTO mapping).
export const DELIVERY_STATUS_CONFIG: Record<string, StatusBadgeConfig> = {
  Queued: { icon: Clock, tone: "neutral", label: "Queued" },
  Sending: { icon: Clock, tone: "progress", label: "Sending", spin: true },
  Sent: { icon: Check, tone: "info", label: "Sent" },
  Delivered: { icon: CheckCheck, tone: "success", label: "Delivered" },
  Failed: { icon: XCircle, tone: "danger", label: "Failed" },
  Superseded: { icon: CornerUpRight, tone: "muted", label: "Superseded" },
  // P9-07: terminal, never picked up again by the dispatcher — same "muted" tone as
  // Superseded (a never-sent-but-not-a-delivery-failure outcome), not "danger" like Failed.
  Expired: { icon: Hourglass, tone: "muted", label: "Expired" },
};

export const TASK_STATUS_CONFIG: Record<TaskStatus, StatusBadgeConfig> = {
  Open: { icon: Clock, tone: "neutral", label: "Open" },
  InProgress: { icon: ArrowUp, tone: "info", label: "In progress" },
  Completed: { icon: CheckCheck, tone: "success", label: "Completed" },
  Escalated: { icon: AlertTriangle, tone: "danger", label: "Escalated" },
  Cancelled: { icon: XCircle, tone: "muted", label: "Cancelled" },
};

export const TASK_PRIORITY_CONFIG: Record<TaskPriority, StatusBadgeConfig> = {
  Low: { icon: ArrowDown, tone: "neutral", label: "Low" },
  Medium: { icon: Minus, tone: "info", label: "Medium" },
  High: { icon: ArrowUp, tone: "progress", label: "High" },
  Urgent: { icon: AlertTriangle, tone: "danger", label: "Urgent" },
};

// Keyed on AuditLogDto.action — exact strings emitted by AuditLogger.Add call sites
// (NotifyHub.Api/Infrastructure), lowercase-hyphenated, not the C# enum casing.
export const AUDIT_ACTION_CONFIG: Record<string, StatusBadgeConfig> = {
  send: { icon: Send, tone: "info", label: "Send" },
  receipt: { icon: CheckCheck, tone: "success", label: "Receipt" },
  "opt-out": { icon: Ban, tone: "muted", label: "Opt-out" },
  assignment: { icon: UserPlus, tone: "info", label: "Assignment" },
  escalation: { icon: ArrowUpCircle, tone: "danger", label: "Escalation" },
  blocked: { icon: ShieldAlert, tone: "danger", label: "Blocked" },
  superseded: { icon: CornerUpRight, tone: "muted", label: "Superseded" },
};

export const TRIGGER_TYPE_CONFIG: Record<TemplateTriggerType, StatusBadgeConfig> = {
  AppointmentReminder: { icon: Calendar, tone: "info", label: "Appointment reminder" },
  MedicationAlert: { icon: Pill, tone: "progress", label: "Medication alert" },
  PrescriptionAlert: { icon: ClipboardList, tone: "neutral", label: "Prescription alert" },
};

// Fallback for any status/action string not covered above (defensive against future
// enum values the frontend hasn't been updated for) — renders plainly rather than
// throwing on an unmapped key.
export const UNKNOWN_STATUS_CONFIG: StatusBadgeConfig = {
  icon: Clock,
  tone: "neutral",
  label: "Unknown",
};
