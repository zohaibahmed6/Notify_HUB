import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { CreateTemplateRequest, TemplateDto, UpdateTemplateRequest } from "@/types/templates";

export function useTemplates(isActive?: boolean) {
  const params = isActive === undefined ? "" : `?isActive=${isActive}`;

  return useQuery({
    queryKey: ["templates", isActive],
    queryFn: () => apiClient.get<TemplateDto[]>(`/api/templates${params}`),
  });
}

export function useCreateTemplateMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTemplateRequest) => apiClient.post<TemplateDto>("/api/templates", body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["templates"] }),
  });
}

export function useUpdateTemplateMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...body }: UpdateTemplateRequest & { id: number }) =>
      apiClient.patch<TemplateDto>(`/api/templates/${id}`, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["templates"] }),
  });
}
