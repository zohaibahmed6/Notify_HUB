import { useState } from "react";
import { MessageSquareText, ShieldAlert } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useSmsHistory } from "@/hooks/useMessages";
import { toDateInputValue, defaultFromDaysAgo, toInstantRange } from "@/lib/dateRangeFilter";
import { formatUtc } from "@/lib/dateUtc";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { StatusBadge } from "@/components/v2/status-badge";
import { DELIVERY_STATUS_CONFIG, UNKNOWN_STATUS_CONFIG } from "@/components/v2/status-config";
import { EmptyState } from "@/components/v2/empty-state";
import { TableRowSkeleton } from "@/components/v2/skeletons";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { FilterBar, FilterField } from "@/components/v2/filter-bar";

const STATUSES = ["All", "Queued", "Sending", "Sent", "Delivered", "Failed", "Expired"];
const PAGE_SIZE = 25;

/// P9-06: SMS History report — Admin-only (matches AuditController's access pattern, not
/// the shared-inbox default-authenticated model). Unversioned, no legacy variant needed
/// (entirely new screen, same "no legacy equivalent" precedent as DashboardPage/
/// SettingsPage). Skeleton per the build-order note in STEP9_PLAN.md: Scheduled Time is
/// wired now (OutboundMessage.ScheduledAt already existed); Expiry Time/PDU Count render
/// as "—" until P9-07/P9-09 add the underlying columns.
export default function SmsHistoryPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  const [patientName, setPatientName] = useState("");
  const [username, setUsername] = useState("");
  const [phone, setPhone] = useState("");
  const [text, setText] = useState("");
  const [status, setStatus] = useState("All");
  const [from, setFrom] = useState(() => defaultFromDaysAgo(7));
  const [to, setTo] = useState(() => toDateInputValue(new Date()));
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSmsHistory({
    patientName: patientName || undefined,
    username: username || undefined,
    phone: phone || undefined,
    text: text || undefined,
    status: status === "All" ? undefined : status,
    ...toInstantRange(from, to),
    page,
    pageSize: PAGE_SIZE,
  });

  const rows = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  const resetPage = () => setPage(1);

  const resetFilters = () => {
    setPatientName("");
    setUsername("");
    setPhone("");
    setText("");
    setStatus("All");
    setFrom(defaultFromDaysAgo(7));
    setTo(toDateInputValue(new Date()));
    setPage(1);
  };

  if (!isAdmin) {
    return (
      <div className="flex h-full flex-col p-4">
        <EmptyState
          icon={ShieldAlert}
          title="Admins only"
          description="SMS History is only available to Admin accounts."
        />
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <h1 className="mb-4 text-lg font-semibold">SMS History</h1>

      <FilterBar className="mb-3">
        <FilterField label="Patient" htmlFor="sms-patient">
          <Input
            id="sms-patient"
            placeholder="Any patient"
            value={patientName}
            onChange={(e) => {
              setPatientName(e.target.value);
              resetPage();
            }}
            className="h-8"
          />
        </FilterField>
        <FilterField label="Sender" htmlFor="sms-username">
          <Input
            id="sms-username"
            placeholder="Any sender"
            value={username}
            onChange={(e) => {
              setUsername(e.target.value);
              resetPage();
            }}
            className="h-8"
          />
        </FilterField>
        <FilterField label="Phone" htmlFor="sms-phone">
          <Input
            id="sms-phone"
            placeholder="Any phone"
            value={phone}
            onChange={(e) => {
              setPhone(e.target.value);
              resetPage();
            }}
            className="h-8"
          />
        </FilterField>
        <FilterField label="Text" htmlFor="sms-text">
          <Input
            id="sms-text"
            placeholder="Search message text"
            value={text}
            onChange={(e) => {
              setText(e.target.value);
              resetPage();
            }}
            className="h-8"
          />
        </FilterField>
        <FilterField label="Status" htmlFor="sms-status">
          <Select
            value={status}
            onValueChange={(v) => {
              setStatus(v);
              resetPage();
            }}
          >
            <SelectTrigger id="sms-status" className="h-8 w-full text-sm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {STATUSES.map((s) => (
                <SelectItem key={s} value={s}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterField>
        <FilterField label="From" htmlFor="sms-from">
          <DateTimePicker
            id="sms-from"
            mode="date"
            value={from}
            onChange={(v) => {
              setFrom(v);
              resetPage();
            }}
            variant="compact"
          />
        </FilterField>
        <FilterField label="To" htmlFor="sms-to">
          <DateTimePicker
            id="sms-to"
            mode="date"
            value={to}
            onChange={(v) => {
              setTo(v);
              resetPage();
            }}
            variant="compact"
          />
        </FilterField>
      </FilterBar>

      <div className="mb-4 flex items-center justify-end">
        <Button variant="outline" size="sm" className="h-8" onClick={resetFilters}>
          Reset
        </Button>
      </div>

      {isLoading ? (
        <div className="divide-y rounded-lg border">
          {Array.from({ length: 8 }).map((_, i) => (
            <TableRowSkeleton key={i} columns={8} />
          ))}
        </div>
      ) : rows.length === 0 ? (
        <EmptyState icon={MessageSquareText} title="No messages found" description="Try widening your filters." />
      ) : (
        <>
          {/* Mobile: stacked card rows instead of a horizontally-scrolling table — every
              column stays visible, just arranged vertically per entry (P9-00). */}
          <div className="min-h-0 flex-1 space-y-2 overflow-y-auto md:hidden">
            {rows.map((row) => {
              const config = DELIVERY_STATUS_CONFIG[row.status] ?? UNKNOWN_STATUS_CONFIG;
              return (
                <div key={row.id} className="rounded-lg border p-3 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">{row.patientName}</span>
                    <StatusBadge {...config} />
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{row.text ?? "—"}</p>
                  <div className="mt-2 grid grid-cols-[auto_1fr] gap-x-2 gap-y-1 text-xs">
                    <span className="text-muted-foreground">Sender</span>
                    <span>{row.senderUsername}</span>
                    <span className="text-muted-foreground">Phone</span>
                    <span className="font-mono">{row.phone}</span>
                    <span className="text-muted-foreground">Scheduled</span>
                    <span>{formatUtc(row.scheduledTime)}</span>
                    <span className="text-muted-foreground">Expiry</span>
                    <span>{formatUtc(row.expiryTime)}</span>
                    <span className="text-muted-foreground">PDU</span>
                    <span>{row.pduCount ?? "—"}</span>
                  </div>
                </div>
              );
            })}
          </div>

          <div className="hidden min-h-0 flex-1 overflow-auto rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="sticky top-0 z-10 bg-background">Patient</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Sender</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Phone</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Text</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Status</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Scheduled</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">Expiry</TableHead>
                  <TableHead className="sticky top-0 z-10 bg-background">PDU</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const config = DELIVERY_STATUS_CONFIG[row.status] ?? UNKNOWN_STATUS_CONFIG;
                  return (
                    <TableRow key={row.id}>
                      <TableCell className="text-sm">{row.patientName}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{row.senderUsername}</TableCell>
                      <TableCell className="font-mono text-xs">{row.phone}</TableCell>
                      <TableCell className="max-w-xs whitespace-normal break-words text-xs text-muted-foreground">{row.text ?? "—"}</TableCell>
                      <TableCell>
                        <StatusBadge {...config} size="xs" />
                      </TableCell>
                      <TableCell className="font-mono text-xs">
                        {formatUtc(row.scheduledTime)}
                      </TableCell>
                      <TableCell className="font-mono text-xs">
                        {formatUtc(row.expiryTime)}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">{row.pduCount ?? "—"}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
          <div className="mt-3 flex shrink-0 items-center justify-between text-sm text-muted-foreground">
            <span className="font-mono text-xs">
              Page {data?.page} of {totalPages} ({data?.totalCount} total/{data?.totalPduCount} pdu(segments))
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
