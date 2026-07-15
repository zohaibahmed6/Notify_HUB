import { Link } from "react-router-dom";
import { AlertTriangle, ArrowUp, Clock, Inbox as InboxIcon, MailWarning } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useDashboardSummary } from "@/hooks/useDashboard";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/v2/status-badge";
import { AUDIT_ACTION_CONFIG, UNKNOWN_STATUS_CONFIG } from "@/components/v2/status-config";
import { EmptyState } from "@/components/v2/empty-state";

function StatCard({ label, value, icon: Icon, tone }: { label: string; value: number; icon: typeof Clock; tone: "neutral" | "danger" | "info" }) {
  const toneClass = tone === "danger" ? "text-destructive" : tone === "info" ? "text-blue-600 dark:text-blue-400" : "text-foreground";

  return (
    <Card>
      <CardContent className="flex items-center justify-between p-4">
        <div>
          <div className="text-xs text-muted-foreground">{label}</div>
          <div className={`text-2xl font-semibold ${toneClass}`}>{value}</div>
        </div>
        <Icon className={`size-6 ${toneClass}`} />
      </CardContent>
    </Card>
  );
}

/// Post-login landing page — pure summary over existing data (GET /api/dashboard/summary),
/// no new business logic. v2-only (no legacy variant needed since this screen is entirely new).
export default function DashboardPage() {
  const { user } = useAuth();
  const { data: summary, isLoading } = useDashboardSummary();

  return (
    <div className="h-full overflow-y-auto p-6">
      <h1 className="mb-1 text-lg font-semibold">Welcome back, {user?.username}</h1>
      <p className="mb-6 text-sm text-muted-foreground">Here's what's happening in NotifyHub.</p>

      {isLoading || !summary ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      ) : (
        <>
          <div className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-4">
            <StatCard label="My open tasks" value={summary.myTasks.open + summary.myTasks.inProgress} icon={Clock} tone="neutral" />
            <StatCard label="My escalated" value={summary.myTasks.escalated} icon={ArrowUp} tone="danger" />
            <StatCard label="My overdue" value={summary.myTasks.overdue} icon={AlertTriangle} tone="danger" />
            <StatCard label="Unread threads" value={summary.unreadThreadCount} icon={MailWarning} tone="info" />
          </div>

          {summary.orgTasks && (
            <Card className="mb-6">
              <CardHeader>
                <CardTitle className="text-base">Org-wide tasks</CardTitle>
                <CardDescription>Visible to Admins only.</CardDescription>
              </CardHeader>
              <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-4 text-sm">
                <div>
                  <div className="text-muted-foreground">Open</div>
                  <div className="text-lg font-medium">{summary.orgTasks.open}</div>
                </div>
                <div>
                  <div className="text-muted-foreground">In progress</div>
                  <div className="text-lg font-medium">{summary.orgTasks.inProgress}</div>
                </div>
                <div>
                  <div className="text-muted-foreground">Escalated</div>
                  <div className="text-lg font-medium text-destructive">{summary.orgTasks.escalated}</div>
                </div>
                <div>
                  <div className="text-muted-foreground">Overdue</div>
                  <div className="text-lg font-medium text-destructive">{summary.orgTasks.overdue}</div>
                </div>
              </CardContent>
            </Card>
          )}

          <div className="mb-6 flex gap-2">
            <Button asChild size="sm" variant="outline" className="gap-1.5">
              <Link to="/inbox">
                <InboxIcon className="size-4" />
                Go to Inbox
              </Link>
            </Button>
            <Button asChild size="sm" variant="outline">
              <Link to="/tasks">Go to Task board</Link>
            </Button>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Recent activity</CardTitle>
            </CardHeader>
            <CardContent>
              {summary.recentActivity.length === 0 ? (
                <EmptyState icon={Clock} title="No recent activity" description="Actions will show up here as they happen." />
              ) : (
                <ul className="divide-y">
                  {summary.recentActivity.map((entry) => (
                    <li key={entry.id} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-sm">
                      <div className="flex min-w-0 items-center gap-2">
                        <StatusBadge {...(AUDIT_ACTION_CONFIG[entry.action] ?? UNKNOWN_STATUS_CONFIG)} size="xs" />
                        <span className="font-medium">{entry.actor}</span>
                        <span className="truncate text-muted-foreground">{entry.detail ?? "—"}</span>
                      </div>
                      <span className="shrink-0 text-xs text-muted-foreground">{new Date(entry.occurredAt).toLocaleString()}</span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
