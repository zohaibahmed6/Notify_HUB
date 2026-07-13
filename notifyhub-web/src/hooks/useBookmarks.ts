import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { BookmarkDto, CreateBookmarkRequest, UpdateBookmarkRequest } from "@/types/bookmarks";

export function useBookmarks() {
  return useQuery({
    queryKey: ["bookmarks"],
    queryFn: () => apiClient.get<BookmarkDto[]>("/api/bookmarks"),
  });
}

export function useCreateBookmarkMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateBookmarkRequest) => apiClient.post<BookmarkDto>("/api/bookmarks", body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["bookmarks"] }),
  });
}

export function useUpdateBookmarkMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: UpdateBookmarkRequest & { id: number }) =>
      apiClient.patch<BookmarkDto>(`/api/bookmarks/${id}`, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["bookmarks"] }),
  });
}

export function useDeleteBookmarkMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => apiClient.delete<void>(`/api/bookmarks/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["bookmarks"] }),
  });
}
