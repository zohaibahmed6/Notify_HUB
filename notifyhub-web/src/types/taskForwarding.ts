export interface TaskForwardingRuleDto {
  id: number;
  targetUserId: number;
  targetUsername: string;
  from: string | null;
  to: string | null;
  reason: string | null;
  createdAt: string;
}

export interface TaskForwardingRuleRequest {
  targetUserId: number;
  from?: string | null;
  to?: string | null;
  reason?: string | null;
}
