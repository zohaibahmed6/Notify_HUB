import { useEffect } from "react";

import { useAuth } from "@/context/AuthContext";
import { useAssignableUsers } from "@/hooks/useUsers";
import { useSettings } from "@/hooks/useSettings";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/// Shared by CreateTaskForm (inbox thread) and NewTaskForm (task board) so the default-
/// assignee resolution logic (thread's current owner -> configured default task provider
/// -> the creator) lives in one place, matching the equivalent server-side fallback in
/// ThreadsController.CreateTask exactly (`thread.AssignedStaffId ?? defaultProviderId ??
/// callerId` — no "lowest-id Active Admin" rung). A prior version of this default chain
/// inserted a lowest-id-Active-Admin step before falling back to the creator, which could
/// silently pre-select a different Admin than whoever opened the dialog once a second
/// Active Admin existed — removed as a bug fix, not a scope change.
export function TaskAssignmentFields({
  threadAssignedStaffId,
  value,
  onChange,
}: {
  threadAssignedStaffId: number | null | undefined;
  value: number | "";
  onChange: (id: number) => void;
}) {
  const { user } = useAuth();
  const { data: assignableUsers } = useAssignableUsers();
  const { data: settings } = useSettings();

  useEffect(() => {
    if (value !== "" || !assignableUsers) return;

    const byId = (id: number | null | undefined) => assignableUsers.find((u) => u.id === id);

    const defaultId =
      byId(threadAssignedStaffId)?.id ??
      byId(settings?.defaultTaskProviderId ?? null)?.id ??
      user?.id;

    if (defaultId !== undefined) onChange(defaultId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [assignableUsers, settings, threadAssignedStaffId]);

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <div className="space-y-1.5">
        <Label>Assigned from</Label>
        <div className="flex h-10 items-center rounded-md border border-input bg-muted px-3 text-sm text-muted-foreground">
          {user?.username} ({user?.role})
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="task-assigned-to">Assigned to</Label>
        <Select value={value === "" ? undefined : String(value)} onValueChange={(v) => onChange(Number(v))}>
          <SelectTrigger id="task-assigned-to">
            <SelectValue placeholder="Select a user..." />
          </SelectTrigger>
          <SelectContent>
            {(assignableUsers ?? []).map((u) => (
              <SelectItem key={u.id} value={String(u.id)}>
                {u.fullName ?? u.username}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
