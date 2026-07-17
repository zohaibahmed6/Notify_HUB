import type { UserRole } from "@/types/users";

export function formatUserLabel(user: {
  fullName?: string | null;
  username: string;
  role?: UserRole | string | null;
}): string {
  const name = user.fullName?.trim() || user.username;
  return user.role ? `${name} (${user.role})` : name;
}

export function formatUserName(user: { fullName?: string | null; username: string }): string {
  return user.fullName?.trim() || user.username;
}
