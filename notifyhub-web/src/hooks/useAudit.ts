import { useQuery } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { PagedResult } from "@/types/inbox";
import type { AuditLogDto } from "@/types/audit";

export interface AuditFilters {
  actor?: string;
  action?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

function buildQuery(filters: AuditFilters): string {
  const params = new URLSearchParams();
  if (filters.actor) params.set("actor", filters.actor);
  if (filters.action) params.set("action", filters.action);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 25));
  return params.toString();
}

/** isAdmin picks /api/audit (all actors, actor filter honored) vs /api/audit/mine (server
 * ignores any actor filter and scopes to the caller's own username, §8). */
export function useAuditLog(isAdmin: boolean, filters: AuditFilters) {
  const path = isAdmin ? "/api/audit" : "/api/audit/mine";

  return useQuery({
    queryKey: ["audit", isAdmin, filters],
    queryFn: () => apiClient.get<PagedResult<AuditLogDto>>(`${path}?${buildQuery(filters)}`),
  });
}
