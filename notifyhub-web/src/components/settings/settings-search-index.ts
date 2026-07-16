export type SettingsTabValue = "general" | "sms" | "task" | "template" | "notification" | "users" | "system";

export interface SettingsSearchItem {
  id: string;
  tab: SettingsTabValue;
  label: string;
  group: string;
  adminOnly?: boolean;
}

/// Manifest of searchable settings Cards, one entry per Card across all Settings tabs.
/// `id` must match the DOM id on that Card so SettingsPage can scroll to it.
export const SETTINGS_SEARCH_INDEX: SettingsSearchItem[] = [
  { id: "general-info", tab: "general", label: "General", group: "General" },
  { id: "sms-quiet-hours", tab: "sms", label: "Quiet Hours", group: "SMS" },
  { id: "sms-rate-limit", tab: "sms", label: "Per-patient rate limiting", group: "SMS" },
  { id: "sms-reminder-defaults", tab: "sms", label: "Reminder SMS defaults", group: "SMS" },
  { id: "task-defaults", tab: "task", label: "Task defaults", group: "Task" },
  { id: "task-default-provider", tab: "task", label: "Default task provider", group: "Task" },
  { id: "task-forwarding", tab: "task", label: "Task forwarding", group: "Task" },
  { id: "template-bookmarks", tab: "template", label: "Bookmarks", group: "Template" },
  { id: "template-add-bookmark", tab: "template", label: "Add bookmark", group: "Template" },
  { id: "notification-browser", tab: "notification", label: "Browser notifications", group: "Notification" },
  { id: "users-list", tab: "users", label: "Users", group: "User Management", adminOnly: true },
  { id: "users-create", tab: "users", label: "Create user", group: "User Management", adminOnly: true },
  { id: "system-info", tab: "system", label: "System", group: "System" },
];
