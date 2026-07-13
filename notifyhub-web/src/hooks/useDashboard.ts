import { useQuery } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { DashboardSummaryDto } from "@/types/dashboard";

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: () => apiClient.get<DashboardSummaryDto>("/api/dashboard/summary"),
  });
}
