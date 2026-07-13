import { CheckCircle2, XCircle } from "lucide-react";

import { useSystemInfo } from "@/hooks/useSettings";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

/// Read-only diagnostics — not SystemSetting-backed, reflects live config/DB state via
/// GET /api/settings/system-info.
export function SystemTab() {
  const { data: info, isLoading } = useSystemInfo();

  if (isLoading || !info) {
    return <Skeleton className="h-40 w-full" />;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">System</CardTitle>
        <CardDescription>Live diagnostics — not editable.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Database</span>
          <span className="flex items-center gap-1.5">
            {info.databaseConnected ? (
              <CheckCircle2 className="size-4 text-emerald-500" />
            ) : (
              <XCircle className="size-4 text-red-500" />
            )}
            {info.databaseConnected ? "Connected" : "Disconnected"}
          </span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Dispatcher poll interval</span>
          <span>{info.dispatcherPollIntervalSeconds}s</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Escalation poll interval</span>
          <span>{info.escalationPollIntervalSeconds}s</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Reminder poll interval</span>
          <span>{info.reminderPollIntervalSeconds}s</span>
        </div>
      </CardContent>
    </Card>
  );
}
