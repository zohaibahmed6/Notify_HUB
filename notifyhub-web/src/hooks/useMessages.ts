import { useQuery } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { SmsHistoryPagedResult } from "@/types/messages";

export interface SmsHistoryFilters {
  patientName?: string;
  username?: string;
  phone?: string;
  text?: string;
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

function buildQuery(filters: SmsHistoryFilters): string {
  const params = new URLSearchParams();
  if (filters.patientName) params.set("patientName", filters.patientName);
  if (filters.username) params.set("username", filters.username);
  if (filters.phone) params.set("phone", filters.phone);
  if (filters.text) params.set("text", filters.text);
  if (filters.status) params.set("status", filters.status);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 25));
  return params.toString();
}

/** P9-06: GET /api/messages, Admin-only (matches useAuditLog's GET /api/audit pattern). */
export function useSmsHistory(filters: SmsHistoryFilters) {
  return useQuery({
    queryKey: ["messages", filters],
    queryFn: () => apiClient.get<SmsHistoryPagedResult>(`/api/messages?${buildQuery(filters)}`),
  });
}
