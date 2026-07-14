import { useState } from "react";
import { toast } from "sonner";
import { Trash2 } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useAssignableUsers } from "@/hooks/useUsers";
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
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to).toISOString() : undefined,
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
    <Card>
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

/// Task due-date defaults stay read-only (FR-008 hardcoded Domain constants, not
/// SystemSetting-backed) — forwarding rules below are the real, editable control this tab
/// gains in P9-10.
export function TaskTab() {
  return (
    <div className="space-y-4">
      <Card>
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

      <TaskForwardingRulesCard />
    </div>
  );
}
