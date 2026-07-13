import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

/// Client-only — browser notification permission, no backend. No concrete
/// notification-preference field list was specified beyond this.
export function NotificationTab() {
  const [permission, setPermission] = useState<NotificationPermission>(
    typeof Notification !== "undefined" ? Notification.permission : "denied",
  );

  const requestPermission = async () => {
    if (typeof Notification === "undefined") return;
    const result = await Notification.requestPermission();
    setPermission(result);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Browser notifications</CardTitle>
        <CardDescription>Get a desktop notification for real-time inbox/task events.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="text-sm">
          Status: <span className="font-medium">{permission}</span>
        </div>
        {permission !== "granted" && (
          <Button size="sm" variant="outline" onClick={requestPermission}>
            Enable notifications
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
