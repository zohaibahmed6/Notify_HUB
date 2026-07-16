import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { PagedResult } from "@/types/inbox";
import type { ForwardTaskRequest, TaskDto, TaskListFilters, TaskStatus, UpdateTaskRequest } from "@/types/tasks";

/// Accepts either a bare status shorthand (legacy TaskBoardPage's usage) or a full filter
/// object (TaskBoardPageV2's filter bar). `isActive` is omitted here by default so the
/// server applies its own default (Active-only) — pass `isActive: false` explicitly to
/// see inactive tasks, matching the "just a checkbox, Active selected by default" model.
/// `pageSize` defaults to 100 (the Kanban Board's "give me everything active" fetch) —
/// the Grid view passes its own smaller `pageSize`/`page`/`sortBy`/`sortDir` for real
/// server-side pagination. `options.enabled` lets a caller with two tabs (Board/Grid) gate
/// each fetch to its own active tab so switching tabs doesn't fire a wasted request.
export function useTasks(statusOrFilters?: TaskStatus | "All" | TaskListFilters, options?: { enabled?: boolean }) {
  const filters: TaskListFilters =
    typeof statusOrFilters === "string" || statusOrFilters === undefined
      ? { status: statusOrFilters }
      : statusOrFilters;

  const params = new URLSearchParams({ pageSize: String(filters.pageSize ?? 100) });
  if (filters.statuses?.length) params.set("status", filters.statuses.join(","));
  else if (filters.status && filters.status !== "All") params.set("status", filters.status);
  if (filters.unassigned) params.set("unassigned", "true");
  else if (filters.assignedStaffId != null) params.set("assignedStaffId", String(filters.assignedStaffId));
  if (filters.description) params.set("description", filters.description);
  if (filters.patientName) params.set("patientName", filters.patientName);
  if (filters.dueFrom) params.set("dueFrom", filters.dueFrom);
  if (filters.dueTo) params.set("dueTo", filters.dueTo);
  if (filters.isActive !== undefined) params.set("isActive", String(filters.isActive));
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.isRecurring !== undefined) params.set("isRecurring", String(filters.isRecurring));
  if (filters.sortBy) params.set("sortBy", filters.sortBy);
  if (filters.sortDir) params.set("sortDir", filters.sortDir);
  if (filters.page != null) params.set("page", String(filters.page));

  return useQuery({
    queryKey: ["tasks", filters],
    queryFn: () => apiClient.get<PagedResult<TaskDto>>(`/api/tasks?${params.toString()}`),
    enabled: options?.enabled,
  });
}

/// BR-014: fetching a task's detail is itself an "action taken" by the assignee — the
/// backend (TasksController.Detail) reverts Escalated -> InProgress as a side effect of
/// this GET when the caller is the assignee. Firing this query (i.e. "opening" the task
/// in the UI) is what makes that revert happen; the list query alone never triggers it.
export function useTask(id: number | null) {
  return useQuery({
    queryKey: ["task", id],
    queryFn: () => apiClient.get<TaskDto>(`/api/tasks/${id}`),
    enabled: id != null,
  });
}

export function useUpdateTaskMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: { id: number } & UpdateTaskRequest) =>
      apiClient.patch<TaskDto>(`/api/tasks/${id}`, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
}

export function useForwardTaskMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: { id: number } & ForwardTaskRequest) =>
      apiClient.post<TaskDto>(`/api/tasks/${id}/forward`, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
}
