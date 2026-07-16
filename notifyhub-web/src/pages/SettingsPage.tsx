import { useEffect, useState } from "react";

import { useAuth } from "@/context/AuthContext";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { GeneralTab } from "@/components/settings/general-tab";
import { SmsTab } from "@/components/settings/sms-tab";
import { TaskTab } from "@/components/settings/task-tab";
import { TemplateTab } from "@/components/settings/template-tab";
import { NotificationTab } from "@/components/settings/notification-tab";
import { UserManagementTab } from "@/components/settings/user-management-tab";
import { SystemTab } from "@/components/settings/system-tab";
import type { SettingsTabValue } from "@/components/settings/settings-search-index";

/// §8: dedicated Settings area — General/SMS/Task/Template/Notification/User Management/
/// System tabs. User Management is Admin-only (server already enforces this on every
/// underlying endpoint; hidden here too so Staff aren't shown a tab that would just 403).
export default function SettingsPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  const [activeTab, setActiveTab] = useState<SettingsTabValue>("general");
  const [highlight, setHighlight] = useState<{ id: string; token: number } | null>(null);

  const navigateToSetting = (tab: SettingsTabValue, sectionId: string) => {
    setActiveTab(tab);
    setHighlight({ id: sectionId, token: Date.now() });
  };

  // Runs after the tab switch commits so the target Card exists in the DOM by the time
  // this fires — a search result can land on a tab that wasn't previously mounted.
  useEffect(() => {
    if (!highlight) return;
    let timeoutId: number | undefined;
    const frameId = requestAnimationFrame(() => {
      const el = document.getElementById(highlight.id);
      if (!el) return;
      el.scrollIntoView({ behavior: "smooth", block: "center" });
      el.classList.add("ring-2", "ring-primary", "ring-offset-2");
      timeoutId = window.setTimeout(() => {
        el.classList.remove("ring-2", "ring-primary", "ring-offset-2");
      }, 1500);
    });
    return () => {
      cancelAnimationFrame(frameId);
      if (timeoutId) window.clearTimeout(timeoutId);
    };
  }, [highlight]);

  return (
    <div className="mx-auto h-full max-w-3xl overflow-y-auto p-6">
      <h1 className="mb-1 text-lg font-semibold">Settings</h1>
      <p className="mb-6 text-sm text-muted-foreground">Configure how NotifyHub looks and behaves.</p>

      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as SettingsTabValue)}>
        <TabsList className="mb-4 h-auto flex-wrap justify-start gap-1">
          <TabsTrigger value="general">General</TabsTrigger>
          <TabsTrigger value="sms">SMS</TabsTrigger>
          <TabsTrigger value="task">Task</TabsTrigger>
          <TabsTrigger value="template">Template</TabsTrigger>
          <TabsTrigger value="notification">Notification</TabsTrigger>
          {isAdmin && <TabsTrigger value="users">User Management</TabsTrigger>}
          <TabsTrigger value="system">System</TabsTrigger>
        </TabsList>

        <TabsContent value="general">
          <GeneralTab onNavigateToSetting={navigateToSetting} />
        </TabsContent>
        <TabsContent value="sms">
          <SmsTab />
        </TabsContent>
        <TabsContent value="task">
          <TaskTab />
        </TabsContent>
        <TabsContent value="template">
          <TemplateTab />
        </TabsContent>
        <TabsContent value="notification">
          <NotificationTab />
        </TabsContent>
        {isAdmin && (
          <TabsContent value="users">
            <UserManagementTab />
          </TabsContent>
        )}
        <TabsContent value="system">
          <SystemTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}
