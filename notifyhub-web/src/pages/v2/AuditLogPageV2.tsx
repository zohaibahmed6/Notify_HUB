import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, ArrowUpDown, ScrollText, ShieldAlert } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useAuditLog } from "@/hooks/useAudit";
import { toDateInputValue, defaultFromDaysAgo, toInstantRange } from "@/lib/dateRangeFilter";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { StatusBadge } from "@/components/v2/status-badge";
import { AUDIT_ACTION_CONFIG, UNKNOWN_STATUS_CONFIG } from "@/components/v2/status-config";
import { EmptyState } from "@/components/v2/empty-state";
import { TableRowSkeleton } from "@/components/v2/skeletons";
import { Sparkline } from "@/components/v2/sparkline";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import type { AuditLogDto } from "@/types/audit";

const ACTIONS = ["All", "send", "receipt", "opt-out", "assignment", "escalation", "blocked", "superseded"];
const PAGE_SIZE = 25;

type SortKey = "actor" | "action" | "entity" | "occurredAt";
type SortDir = "asc" | "desc";

function sortLogs(logs: AuditLogDto[], key: SortKey, dir: SortDir): AuditLogDto[] {
  const sorted = [...logs].sort((a, b) => {
    switch (key) {
      case "actor":
        return a.actor.localeCompare(b.actor);
      case "action":
        return a.action.localeCompare(b.action);
      case "entity":
        return `${a.entityType}#${a.entityId}`.localeCompare(`${b.entityType}#${b.entityId}`);
      case "occurredAt":
        return new Date(a.occurredAt).getTime() - new Date(b.occurredAt).getTime();
    }
  });
  return dir === "asc" ? sorted : sorted.reverse();
}

// Restricted to Admin in the redesign (legacy still lets Staff see their own actions via
// /api/audit/mine) — per explicit product decision, not a technical constraint. Guarded
// here in addition to the nav link being hidden (AppShell.tsx), so a direct /audit visit
// by a Staff account doesn't fall through to the Staff view either.
export default function AuditLogPageV2() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  const [actor, setActor] = useState("");
  const [action, setAction] = useState("All");
  const [from, setFrom] = useState(() => defaultFromDaysAgo(7));
  const [to, setTo] = useState(() => toDateInputValue(new Date()));
  const [page, setPage] = useState(1);
  const [sortKey, setSortKey] = useState<SortKey>("occurredAt");
  const [sortDir, setSortDir] = useState<SortDir>("desc");

  const { data, isLoading } = useAuditLog(isAdmin, {
    actor: actor || undefined,
    action: action === "All" ? undefined : action,
    ...toInstantRange(from, to),
    page,
    pageSize: PAGE_SIZE,
  });

  const logs = data?.items ?? [];
  const sortedLogs = useMemo(() => sortLogs(logs, sortKey, sortDir), [logs, sortKey, sortDir]);
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  // Day-by-day count across the current page only (no new endpoint) — a glanceable shape
  // of activity within the filtered range, not a true full-history aggregate.
  const dailyCounts = useMemo(() => {
    const buckets = new Map<string, number>();
    for (const log of logs) {
      const day = log.occurredAt.slice(0, 10);
      buckets.set(day, (buckets.get(day) ?? 0) + 1);
    }
    return Array.from(buckets.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([, count]) => count);
  }, [logs]);

  const resetPage = () => setPage(1);

  const toggleSort = (key: SortKey) => {
    if (sortKey === key) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDir("desc");
    }
  };

  const SortIcon = ({ column }: { column: SortKey }) => {
    if (sortKey !== column) return <ArrowUpDown className="size-3 text-muted-foreground/50" />;
    return sortDir === "asc" ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />;
  };

  if (!isAdmin) {
    return (
      <div className="flex h-full flex-col p-4">
        <EmptyState
          icon={ShieldAlert}
          title="Admins only"
          description="The audit log is only available to Admin accounts."
        />
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <h1 className="mb-4 text-lg font-semibold">Audit log</h1>

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="audit-actor">Actor</Label>
          <Input
            id="audit-actor"
            placeholder="Any actor"
            value={actor}
            onChange={(e) => {
              setActor(e.target.value);
              resetPage();
            }}
            className="w-40"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="audit-action">Action</Label>
          <Select
            value={action}
            onValueChange={(v) => {
              setAction(v);
              resetPage();
            }}
          >
            <SelectTrigger id="audit-action" className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ACTIONS.map((a) => (
                <SelectItem key={a} value={a}>
                  {a}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="audit-from">From</Label>
          <DateTimePicker
            id="audit-from"
            mode="date"
            value={from}
            onChange={(v) => {
              setFrom(v);
              resetPage();
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
              resetPage();
            }}
            className="w-40"
          />
        </div>
      </div>

      {!isLoading && dailyCounts.length > 1 && (
        <div className="mb-4 max-w-xs">
          <Sparkline values={dailyCounts} />
          <p className="mt-1 text-2xs text-muted-foreground">Events per day, this page</p>
        </div>
      )}

      {isLoading ? (
        <div className="divide-y rounded-lg border">
          {Array.from({ length: 8 }).map((_, i) => (
            <TableRowSkeleton key={i} columns={5} />
          ))}
        </div>
      ) : logs.length === 0 ? (
        <EmptyState icon={ScrollText} title="No audit entries found" description="Try widening your filters." />
      ) : (
        <>
          {/* Mobile: stacked card rows instead of a horizontally-scrolling table — every
              column stays visible, just arranged vertically per entry (P9-00). */}
          <div className="min-h-0 flex-1 space-y-2 overflow-y-auto md:hidden">
            {sortedLogs.map((log) => {
              const config = AUDIT_ACTION_CONFIG[log.action] ?? UNKNOWN_STATUS_CONFIG;
              return (
                <div key={log.id} className="rounded-lg border p-3 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono text-xs">{log.actor}</span>
                    <StatusBadge {...config} />
                  </div>
                  <div className="mt-2 grid grid-cols-[auto_1fr] gap-x-2 gap-y-1 text-xs">
                    <span className="text-muted-foreground">Entity</span>
                    <span>
                      {log.entityType} #{log.entityId}
                    </span>
                    <span className="text-muted-foreground">Occurred at</span>
                    <span className="font-mono">{new Date(log.occurredAt).toLocaleString()}</span>
                    <span className="text-muted-foreground">Detail</span>
                    <span>{log.detail ?? "—"}</span>
                  </div>
                </div>
              );
            })}
          </div>

          <div className="hidden min-h-0 flex-1 overflow-auto rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  {(
                    [
                      ["actor", "Actor"],
                      ["action", "Action"],
                      ["entity", "Entity"],
                      ["occurredAt", "Occurred at"],
                    ] as [SortKey, string][]
                  ).map(([key, label]) => (
                    <TableHead key={key} className="sticky top-0 z-10 bg-background">
                      <button
                        type="button"
                        onClick={() => toggleSort(key)}
                        className="flex items-center gap-1 hover:text-foreground"
                      >
                        {label}
                        <SortIcon column={key} />
                      </button>
                    </TableHead>
                  ))}
                  <TableHead className="sticky top-0 z-10 bg-background">Detail</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sortedLogs.map((log) => {
                  const config = AUDIT_ACTION_CONFIG[log.action] ?? UNKNOWN_STATUS_CONFIG;
                  return (
                    <TableRow key={log.id}>
                      <TableCell className="font-mono text-xs">{log.actor}</TableCell>
                      <TableCell>
                        <StatusBadge {...config} />
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {log.entityType} #{log.entityId}
                      </TableCell>
                      <TableCell className="font-mono text-xs">{new Date(log.occurredAt).toLocaleString()}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{log.detail ?? "—"}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
          <div className="mt-3 flex shrink-0 items-center justify-between text-sm text-muted-foreground">
            <span className="font-mono text-xs">
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
