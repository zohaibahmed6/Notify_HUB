import { useAuth } from "@/context/AuthContext";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

/// Thin/read-only — no concrete General-settings field list was specified; this avoids
/// inventing unrequested schema.
export function GeneralTab() {
  const { user } = useAuth();

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">General</CardTitle>
        <CardDescription>Basic account and application info.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-muted-foreground">Signed in as</span>
          <span>{user?.username} ({user?.role})</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Application</span>
          <span>NotifyHub</span>
        </div>
      </CardContent>
    </Card>
  );
}
