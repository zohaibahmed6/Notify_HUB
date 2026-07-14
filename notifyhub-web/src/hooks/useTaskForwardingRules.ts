import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { TaskForwardingRuleDto, TaskForwardingRuleRequest } from "@/types/taskForwarding";

/// P9-10: self-service — always scoped server-side to the caller's own UserId.
export function useTaskForwardingRules() {
  return useQuery({
    queryKey: ["task-forwarding-rules"],
    queryFn: () => apiClient.get<TaskForwardingRuleDto[]>("/api/task-forwarding-rules"),
  });
}

export function useCreateTaskForwardingRuleMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: TaskForwardingRuleRequest) => apiClient.post<TaskForwardingRuleDto>("/api/task-forwarding-rules", body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["task-forwarding-rules"] }),
  });
}

export function useDeleteTaskForwardingRuleMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => apiClient.delete(`/api/task-forwarding-rules/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["task-forwarding-rules"] }),
  });
}
