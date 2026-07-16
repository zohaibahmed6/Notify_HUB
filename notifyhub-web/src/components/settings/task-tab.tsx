import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Trash2 } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useAssignableUsers } from "@/hooks/useUsers";
import { useSettings, useUpdateSettingsMutation } from "@/hooks/useSettings";
import {
  useCreateTaskForwardingRuleMutation,
  useDeleteTaskForwardingRuleMutation,
  useTaskForwardingRules,
} from "@/hooks/useTaskForwardingRules";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { Skeleton } from "@/components/ui/skeleton";

/** `value` is a "yyyy-MM-dd" string from `DateTimePicker mode="date"` (local calendar
 * day, no timezone). `new Date(value).toISOString()` would parse it as UTC midnight,
 * shifting it to the wrong day once `toLocaleDateString()` converts it back to local time
 * on display — so build the instant from local date parts instead. */
function toLocalMidnightIso(value: string): string {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d).toISOString();
}

const DEFAULT_DUE_DATES = [
  { priority: "Urgent", offset: "+4 hours" },
  { priority: "High", offset: "+1 day" },
  { priority: "Medium", offset: "+3 days" },
  { priority: "Low", offset: "+7 days" },
];

/// P9-10: "forward my tasks to X" — self-service, scoped to the caller's own UserId
/// server-side. Checked (only for new task creation while the natural assignee is
/// Inactive/OnLeave) before the existing always-fallback-to-Admin logic, not a
/// replacement for it.
function TaskForwardingRulesCard() {
  const { user } = useAuth();
  const { data: rules, isLoading } = useTaskForwardingRules();
  const { data: assignableUsers } = useAssignableUsers();
  const createRule = useCreateTaskForwardingRuleMutation();
  const deleteRule = useDeleteTaskForwardingRuleMutation();

  const [targetUserId, setTargetUserId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [reason, setReason] = useState("");

  // Rule 7: a user cannot set themselves as their own forwarding target.
  const targetOptions = (assignableUsers ?? []).filter((u) => u.id !== user?.id);

  const handleCreate = async () => {
    if (!targetUserId) {
      toast.error("Pick who to forward to");
      return;
    }
    try {
      await createRule.mutateAsync({
        targetUserId: Number(targetUserId),
        from: from ? toLocalMidnightIso(from) : undefined,
        to: to ? toLocalMidnightIso(to) : undefined,
        reason: reason || undefined,
      });
      toast.success("Forwarding rule created");
      setTargetUserId("");
      setFrom("");
      setTo("");
      setReason("");
    } catch (error) {
      toast.error(errorMessage(error, "Could not create forwarding rule"));
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteRule.mutateAsync(id);
      toast.success("Forwarding rule removed");
    } catch (error) {
      toast.error(errorMessage(error, "Delete failed"));
    }
  };

  return (
    <Card id="task-forwarding">
      <CardHeader>
        <CardTitle className="text-base">Task forwarding</CardTitle>
        <CardDescription>
          While you're Inactive or On Leave, new tasks that would go to you are forwarded to
          whoever you set here instead of falling straight to an Admin. Only applies to new tasks
          — tasks already assigned to you at the moment you go Inactive/On Leave still forward to
          an Admin as before.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : !rules || rules.length === 0 ? (
          <p className="text-sm text-muted-foreground">No forwarding rules set.</p>
        ) : (
          <ul className="space-y-2">
            {rules.map((rule) => (
              <li key={rule.id} className="flex items-center justify-between gap-3 rounded-md border p-2 text-sm">
                <div className="min-w-0">
                  <div className="font-medium">Forward to {rule.targetUsername}</div>
                  <div className="text-xs text-muted-foreground">
                    {rule.from || rule.to
                      ? `${rule.from ? new Date(rule.from).toLocaleDateString() : "always"} – ${
                          rule.to ? new Date(rule.to).toLocaleDateString() : "always"
                        }`
                      : "Always active"}
                    {rule.reason && ` · ${rule.reason}`}
                  </div>
                </div>
                <Button variant="ghost" size="icon" onClick={() => handleDelete(rule.id)} disabled={deleteRule.isPending}>
                  <Trash2 className="size-4" />
                  <span className="sr-only">Delete</span>
                </Button>
              </li>
            ))}
          </ul>
        )}

        <div className="grid grid-cols-1 gap-3 border-t pt-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="forward-target">Forward to</Label>
            <Select value={targetUserId} onValueChange={setTargetUserId}>
              <SelectTrigger id="forward-target">
                <SelectValue placeholder="Select a user..." />
              </SelectTrigger>
              <SelectContent>
                {targetOptions.map((u) => (
                  <SelectItem key={u.id} value={String(u.id)}>
                    {u.fullName ?? u.username}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="forward-reason">Reason (optional)</Label>
            <Input id="forward-reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. annual leave" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="forward-from">From (optional)</Label>
            <DateTimePicker id="forward-from" mode="date" value={from} onChange={setFrom} placeholder="Always" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="forward-to">To (optional)</Label>
            <DateTimePicker id="forward-to" mode="date" value={to} onChange={setTo} placeholder="Always" />
          </div>
        </div>
        <div className="flex justify-end">
          <Button size="sm" onClick={handleCreate} disabled={createRule.isPending}>
            Add rule
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/// System-wide fallback assignee for a new task created from an unassigned thread (only
/// used when the thread itself has no current owner) — see ThreadsController.CreateTask.
/// Admin-editable; Staff see the configured value but can't change it (server already
/// enforces this on PATCH, disabling client-side just avoids a pointless 403).
function DefaultTaskProviderCard() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { data: settings } = useSettings();
  const { data: assignableUsers } = useAssignableUsers();
  const updateSettings = useUpdateSettingsMutation();

  const [defaultTaskProviderId, setDefaultTaskProviderId] = useState("none");

  useEffect(() => {
    if (!settings) return;
    setDefaultTaskProviderId(settings.defaultTaskProviderId ? String(settings.defaultTaskProviderId) : "none");
  }, [settings]);

  const handleSave = async () => {
    try {
      await updateSettings.mutateAsync({
        defaultTaskProviderId: defaultTaskProviderId === "none" ? 0 : Number(defaultTaskProviderId),
      });
      toast.success("Default task provider saved");
    } catch (error) {
      toast.error(errorMessage(error, "Save failed"));
    }
  };

  return (
    <Card id="task-default-provider">
      <CardHeader>
        <CardTitle className="text-base">Default task provider</CardTitle>
        <CardDescription>
          When a task is created from a thread with no current owner, it's assigned here by default
          (still changeable per task). Falls back to the lowest-id active Admin if nothing is set.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="space-y-1.5">
          <Label htmlFor="default-task-provider">Default assignee</Label>
          <Select value={defaultTaskProviderId} onValueChange={setDefaultTaskProviderId} disabled={!isAdmin}>
            <SelectTrigger id="default-task-provider">
              <SelectValue placeholder="No default" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">No default</SelectItem>
              {(assignableUsers ?? []).map((u) => (
                <SelectItem key={u.id} value={String(u.id)}>
                  {u.fullName ?? u.username}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {isAdmin && (
          <div className="flex justify-end">
            <Button size="sm" onClick={handleSave} disabled={updateSettings.isPending}>
              Save
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/// Task due-date defaults stay read-only (FR-008 hardcoded Domain constants, not
/// SystemSetting-backed) — forwarding rules below are the real, editable control this tab
/// gains in P9-10.
export function TaskTab() {
  return (
    <div className="space-y-4">
      <Card id="task-defaults">
        <CardHeader>
          <CardTitle className="text-base">Task defaults</CardTitle>
          <CardDescription>Due dates auto-suggested by priority when creating a task (not editable here).</CardDescription>
        </CardHeader>
        <CardContent>
          <ul className="space-y-1.5 text-sm">
            {DEFAULT_DUE_DATES.map((d) => (
              <li key={d.priority} className="flex justify-between">
                <span className="text-muted-foreground">{d.priority}</span>
                <span>{d.offset}</span>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <DefaultTaskProviderCard />
      <TaskForwardingRulesCard />
    </div>
  );
}
