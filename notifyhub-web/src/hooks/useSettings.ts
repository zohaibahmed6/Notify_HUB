import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import type { SettingsDto, SystemInfoDto, UpdateSettingsRequest } from "@/types/settings";

export function useSettings() {
  return useQuery({
    queryKey: ["settings"],
    queryFn: () => apiClient.get<SettingsDto>("/api/settings"),
  });
}

export function useUpdateSettingsMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: UpdateSettingsRequest) => apiClient.patch<SettingsDto>("/api/settings", body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings"] }),
  });
}

export function useSystemInfo() {
  return useQuery({
    queryKey: ["settings", "system-info"],
    queryFn: () => apiClient.get<SystemInfoDto>("/api/settings/system-info"),
  });
}
