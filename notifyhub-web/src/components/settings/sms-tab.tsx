import { useEffect, useState } from "react";
import { toast } from "sonner";

import { useSettings, useUpdateSettingsMutation } from "@/hooks/useSettings";
import { useTemplates } from "@/hooks/useTemplates";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/// §6/§8: Quiet Hours and per-patient rate limiting — both default disabled server-side,
/// so this tab is where an Admin opts them in.
export function SmsTab() {
  const { data: settings, isLoading } = useSettings();
  const { data: templates } = useTemplates(true);
  const updateSettings = useUpdateSettingsMutation();

  const [quietHoursEnabled, setQuietHoursEnabled] = useState(false);
  const [quietHoursStart, setQuietHoursStart] = useState("21:00");
  const [quietHoursEnd, setQuietHoursEnd] = useState("08:00");
  const [rateLimitEnabled, setRateLimitEnabled] = useState(false);
  const [rateLimitMaxMessages, setRateLimitMaxMessages] = useState("20");
  const [rateLimitWindowHours, setRateLimitWindowHours] = useState("24");
  const [reminderOffsetMinutes, setReminderOffsetMinutes] = useState("1440");
  const [reminderExpiryOffsetMinutes, setReminderExpiryOffsetMinutes] = useState("15");
  const [defaultReminderTemplateId, setDefaultReminderTemplateId] = useState("none");

  useEffect(() => {
    if (!settings) return;
    setQuietHoursEnabled(settings.quietHoursEnabled);
    setQuietHoursStart(settings.quietHoursStart);
    setQuietHoursEnd(settings.quietHoursEnd);
    setRateLimitEnabled(settings.rateLimitEnabled);
    setRateLimitMaxMessages(String(settings.rateLimitMaxMessages));
    setRateLimitWindowHours(String(settings.rateLimitWindowHours));
    setReminderOffsetMinutes(String(settings.reminderOffsetMinutes));
    setReminderExpiryOffsetMinutes(String(settings.reminderExpiryOffsetMinutes));
    setDefaultReminderTemplateId(settings.defaultReminderTemplateId ? String(settings.defaultReminderTemplateId) : "none");
  }, [settings]);

  const handleSaveQuietHours = async () => {
    try {
      await updateSettings.mutateAsync({ quietHoursEnabled, quietHoursStart, quietHoursEnd });
      toast.success("Quiet hours saved");
    } catch (error) {
      toast.error(errorMessage(error, "Save failed"));
    }
  };

  const handleSaveRateLimit = async () => {
    try {
      await updateSettings.mutateAsync({
        rateLimitEnabled,
        rateLimitMaxMessages: Number(rateLimitMaxMessages),
        rateLimitWindowHours: Number(rateLimitWindowHours),
      });
      toast.success("Rate limit saved");
    } catch (error) {
      toast.error(errorMessage(error, "Save failed"));
    }
  };

  const handleSaveReminderDefaults = async () => {
    try {
      await updateSettings.mutateAsync({
        reminderOffsetMinutes: Number(reminderOffsetMinutes),
        reminderExpiryOffsetMinutes: Number(reminderExpiryOffsetMinutes),
        defaultReminderTemplateId: defaultReminderTemplateId === "none" ? 0 : Number(defaultReminderTemplateId),
      });
      toast.success("Reminder SMS defaults saved");
    } catch (error) {
      toast.error(errorMessage(error, "Save failed"));
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Card id="sms-quiet-hours">
        <CardHeader>
          <CardTitle className="text-base">Quiet Hours</CardTitle>
          <CardDescription>
            While enabled, the dispatcher pauses sending during this UTC window. Messages stay
            queued and go out once the window ends.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={quietHoursEnabled}
              onChange={(e) => setQuietHoursEnabled(e.target.checked)}
              className="size-4 rounded border-input"
            />
            Enabled
          </label>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="quiet-start">Start (UTC)</Label>
              <Input id="quiet-start" type="time" value={quietHoursStart} onChange={(e) => setQuietHoursStart(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="quiet-end">End (UTC)</Label>
              <Input id="quiet-end" type="time" value={quietHoursEnd} onChange={(e) => setQuietHoursEnd(e.target.value)} />
            </div>
          </div>
          <div className="flex justify-end">
            <Button size="sm" onClick={handleSaveQuietHours} disabled={updateSettings.isPending}>
              Save
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card id="sms-rate-limit">
        <CardHeader>
          <CardTitle className="text-base">Per-patient rate limiting</CardTitle>
          <CardDescription>Caps how many outbound messages a single patient can receive within the window.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={rateLimitEnabled}
              onChange={(e) => setRateLimitEnabled(e.target.checked)}
              className="size-4 rounded border-input"
            />
            Enabled
          </label>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="rate-max">Max messages</Label>
              <Input id="rate-max" type="number" min={1} value={rateLimitMaxMessages} onChange={(e) => setRateLimitMaxMessages(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="rate-window">Window (hours)</Label>
              <Input id="rate-window" type="number" min={1} value={rateLimitWindowHours} onChange={(e) => setRateLimitWindowHours(e.target.value)} />
            </div>
          </div>
          <div className="flex justify-end">
            <Button size="sm" onClick={handleSaveRateLimit} disabled={updateSettings.isPending}>
              Save
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card id="sms-reminder-defaults">
        <CardHeader>
          <CardTitle className="text-base">Reminder SMS defaults</CardTitle>
          <CardDescription>
            Applied to newly created Reminder SMS only — changing these never affects reminders
            already scheduled.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="reminder-default-template">Default reminder template</Label>
            <Select value={defaultReminderTemplateId} onValueChange={setDefaultReminderTemplateId}>
              <SelectTrigger id="reminder-default-template">
                <SelectValue placeholder="No default" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No default</SelectItem>
                {(templates ?? []).map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Preselected when opening the Reminder SMS dialog from a thread — staff can still pick a different template there.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="reminder-offset">Reminder offset (minutes before Event Time)</Label>
              <Input
                id="reminder-offset"
                type="number"
                min={1}
                value={reminderOffsetMinutes}
                onChange={(e) => setReminderOffsetMinutes(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="reminder-expiry-offset">Expiry offset (minutes before Event Time)</Label>
              <Input
                id="reminder-expiry-offset"
                type="number"
                min={1}
                value={reminderExpiryOffsetMinutes}
                onChange={(e) => setReminderExpiryOffsetMinutes(e.target.value)}
              />
            </div>
          </div>
          <div className="flex justify-end">
            <Button size="sm" onClick={handleSaveReminderDefaults} disabled={updateSettings.isPending}>
              Save
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
