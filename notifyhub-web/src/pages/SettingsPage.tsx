import { useAuth } from "@/context/AuthContext";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { GeneralTab } from "@/components/settings/general-tab";
import { SmsTab } from "@/components/settings/sms-tab";
import { TaskTab } from "@/components/settings/task-tab";
import { TemplateTab } from "@/components/settings/template-tab";
import { NotificationTab } from "@/components/settings/notification-tab";
import { UserManagementTab } from "@/components/settings/user-management-tab";
import { SystemTab } from "@/components/settings/system-tab";

/// §8: dedicated Settings area — General/SMS/Task/Template/Notification/User Management/
/// System tabs. User Management is Admin-only (server already enforces this on every
/// underlying endpoint; hidden here too so Staff aren't shown a tab that would just 403).
export default function SettingsPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  return (
    <div className="mx-auto h-full max-w-3xl overflow-y-auto p-6">
      <h1 className="mb-1 text-lg font-semibold">Settings</h1>
      <p className="mb-6 text-sm text-muted-foreground">Configure how NotifyHub looks and behaves.</p>

      <Tabs defaultValue="general">
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
          <GeneralTab />
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
