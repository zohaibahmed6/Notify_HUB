import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { PagedResult } from "@/types/inbox";
import type { TaskDto, TaskStatus, UpdateTaskRequest } from "@/types/tasks";

export function useTasks(status?: TaskStatus | "All") {
  const filter = status && status !== "All" ? status : undefined;

  return useQuery({
    queryKey: ["tasks", filter],
    queryFn: () =>
      apiClient.get<PagedResult<TaskDto>>(`/api/tasks?pageSize=100${filter ? `&status=${filter}` : ""}`),
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
