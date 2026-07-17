import type { UserRole } from "@/types/users";

export interface TaskForwardingRuleDto {
  id: number;
  userId: number;
  username: string;
  fullName: string | null;
  role: UserRole;
  targetUserId: number;
  targetUsername: string;
  targetFullName: string | null;
  targetRole: UserRole;
  from: string | null;
  to: string | null;
  reason: string | null;
  createdAt: string;
}

export interface TaskForwardingRuleRequest {
  userId: number;
  targetUserId: number;
  from?: string | null;
  to?: string | null;
  reason?: string | null;
}
