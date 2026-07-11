import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { PagedResult, ThreadDetailDto, ThreadDto } from "@/types/inbox";
import type { CreateTaskRequest, TaskDto } from "@/types/tasks";

export function useThreads() {
  return useQuery({
    queryKey: ["threads"],
    queryFn: () => apiClient.get<PagedResult<ThreadDto>>("/api/threads?pageSize=100"),
  });
}

export function useThread(threadId: number | null) {
  const queryClient = useQueryClient();

  return useQuery({
    queryKey: ["thread", threadId],
    queryFn: async () => {
      const data = await apiClient.get<ThreadDetailDto>(`/api/threads/${threadId}`);
      // Opening a thread resets its unread count server-side (§6c) — refresh the list
      // so the sidebar badge reflects it without a manual reload.
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      return data;
    },
    enabled: threadId !== null,
  });
}

export function useReplyMutation(threadId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: string) => apiClient.post(`/api/threads/${threadId}/messages`, { body }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["thread", threadId] });
    },
  });
}

export function useAssignMutation(threadId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => apiClient.post(`/api/threads/${threadId}/assign`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      queryClient.invalidateQueries({ queryKey: ["thread", threadId] });
    },
  });
}

export function useCreateTaskMutation(threadId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTaskRequest) => apiClient.post<TaskDto>(`/api/threads/${threadId}/tasks`, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
}
