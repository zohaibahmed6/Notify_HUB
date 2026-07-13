import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { PagedResult } from "@/types/inbox";
import type { CreateUserRequest, UpdateUserStatusRequest, UserDto, UserRole, UserStatus } from "@/types/users";

/// The source every assignee-picker in the app should use (task forward, task/thread
/// assign) — replaces the old workaround of deduping usernames off already-fetched
/// task/thread lists, which only ever showed users who already had something assigned.
export function useAssignableUsers() {
  return useQuery({
    queryKey: ["users", "assignable"],
    queryFn: () => apiClient.get<UserDto[]>("/api/users/assignable"),
  });
}

export function useUsers(filters: { role?: UserRole; status?: UserStatus } = {}) {
  const params = new URLSearchParams({ pageSize: "100" });
  if (filters.role) params.set("role", filters.role);
  if (filters.status) params.set("status", filters.status);

  return useQuery({
    queryKey: ["users", "list", filters],
    queryFn: () => apiClient.get<PagedResult<UserDto>>(`/api/users?${params.toString()}`),
  });
}

export function useCreateUserMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateUserRequest) => apiClient.post<UserDto>("/api/users", body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
    },
  });
}

export function useUpdateUserStatusMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: UpdateUserStatusRequest & { id: number }) =>
      apiClient.patch<UserDto>(`/api/users/${id}/status`, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      // A status change can silently reassign tasks (auto-forward) — refresh those too.
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
}
