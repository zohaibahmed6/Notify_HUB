import { useAuth } from "@/context/AuthContext";
import { formatUserLabel } from "@/lib/userDisplay";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { SettingsSearch } from "@/components/settings/settings-search";
import type { SettingsTabValue } from "@/components/settings/settings-search-index";

interface GeneralTabProps {
  onNavigateToSetting: (tab: SettingsTabValue, sectionId: string) => void;
}

/// Thin/read-only — no concrete General-settings field list was specified; this avoids
/// inventing unrequested schema.
export function GeneralTab({ onNavigateToSetting }: GeneralTabProps) {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Find a setting</CardTitle>
          <CardDescription>Search across every Settings tab and jump straight to it.</CardDescription>
        </CardHeader>
        <CardContent>
          <SettingsSearch isAdmin={isAdmin} onNavigate={onNavigateToSetting} />
        </CardContent>
      </Card>

      <Card id="general-info">
        <CardHeader>
          <CardTitle className="text-base">General</CardTitle>
          <CardDescription>Basic account and application info.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Signed in as</span>
            <span>{user && formatUserLabel(user)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Application</span>
            <span>NotifyHub</span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
