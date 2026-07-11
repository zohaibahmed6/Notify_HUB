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
