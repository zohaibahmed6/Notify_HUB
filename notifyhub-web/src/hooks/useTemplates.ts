import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { CommunicationMode, CreateTemplateRequest, TemplateDto, UpdateTemplateRequest } from "@/types/templates";

export function useTemplates(isActive?: boolean, communicationMode?: CommunicationMode) {
  const params = new URLSearchParams();
  if (isActive !== undefined) params.set("isActive", String(isActive));
  if (communicationMode !== undefined) params.set("communicationMode", communicationMode);
  const query = params.toString();

  return useQuery({
    queryKey: ["templates", isActive, communicationMode],
    queryFn: () => apiClient.get<TemplateDto[]>(`/api/templates${query ? `?${query}` : ""}`),
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
