import { useState } from "react";

import { useAuth } from "@/context/AuthContext";
import { useAuditLog } from "@/hooks/useAudit";
import { toDateInputValue, defaultFromDaysAgo, toInstantRange } from "@/lib/dateRangeFilter";
import { formatUtcDateTime } from "@/lib/dateUtc";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { DateTimePicker } from "@/components/v2/date-time-picker";

const ACTIONS = [
  "All",
  "send",
  "receipt",
  "opt-out",
  "assignment",
  "escalation",
  "blocked",
  "superseded",
  "expired",
  "reminder-created",
  "reminder-updated",
  "reminder-cancelled",
];
const PAGE_SIZE = 25;

// §6b: Admin sees the full log (any actor); Staff sees only their own actions via
// /api/audit/mine — the server enforces this (§8), the client just doesn't render an
// actor filter for Staff since it wouldn't do anything.
export default function AuditLogPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  const [actor, setActor] = useState("");
  const [action, setAction] = useState("All");
  // Default to the last 7 days (inclusive of today) instead of unfiltered history.
  const [from, setFrom] = useState(() => defaultFromDaysAgo(7));
  const [to, setTo] = useState(() => toDateInputValue(new Date()));
  const [page, setPage] = useState(1);

  const { data, isLoading } = useAuditLog(isAdmin, {
    actor: isAdmin && actor ? actor : undefined,
    action: action === "All" ? undefined : action,
    ...toInstantRange(from, to),
    page,
    pageSize: PAGE_SIZE,
  });

  const logs = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold">Audit log</h1>
        {!isAdmin && <span className="text-xs text-muted-foreground">Showing your own actions only</span>}
      </div>

      <div className="mb-4 flex flex-wrap items-end gap-3">
        {isAdmin && (
          <div className="space-y-1.5">
            <Label htmlFor="audit-actor">Actor</Label>
            <Input
              id="audit-actor"
              placeholder="Any actor"
              value={actor}
              onChange={(event) => {
                setActor(event.target.value);
                setPage(1);
              }}
              className="w-40"
            />
          </div>
        )}
        <div className="space-y-1.5">
          <Label htmlFor="audit-action">Action</Label>
          <select
            id="audit-action"
            value={action}
            onChange={(event) => {
              setAction(event.target.value);
              setPage(1);
            }}
            className="flex h-9 w-40 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            {ACTIONS.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="audit-from">From</Label>
          <DateTimePicker
            id="audit-from"
            mode="date"
            value={from}
            onChange={(v) => {
              setFrom(v);
              setPage(1);
            }}
            className="w-40"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="audit-to">To</Label>
          <DateTimePicker
            id="audit-to"
            mode="date"
            value={to}
            onChange={(v) => {
              setTo(v);
              setPage(1);
            }}
            className="w-40"
          />
        </div>
      </div>

      {isLoading ? (
        <p className="text-sm text-muted-foreground">Loading...</p>
      ) : logs.length === 0 ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-1 text-center">
          <p className="font-medium">No audit entries found</p>
          <p className="text-sm text-muted-foreground">Try widening your filters.</p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full text-sm">
              <thead className="bg-muted/40 text-left text-xs text-muted-foreground">
                <tr>
                  <th className="p-2">Actor</th>
                  <th className="p-2">Action</th>
                  <th className="p-2">Occurred at</th>
                  <th className="p-2">Detail</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log) => (
                  <tr key={log.id} className="border-t">
                    <td className="p-2">{log.actor}</td>
                    <td className="p-2">{log.action}</td>
                    <td className="p-2">{formatUtcDateTime(log.occurredAt)}</td>
                    <td className="p-2 text-xs text-muted-foreground">{log.detail ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
            <span>
              Page {data?.page} of {totalPages} ({data?.totalCount} total)
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Previous
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
