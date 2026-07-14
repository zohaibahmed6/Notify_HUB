import { useEffect, useState } from "react";
import { toast } from "sonner";

import { useTemplates } from "@/hooks/useTemplates";
import { useCreateReminderMutation } from "@/hooks/useThreads";
import { useSettings } from "@/hooks/useSettings";
import { apiClient } from "@/lib/apiClient";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DateTimePicker } from "@/components/v2/date-time-picker";

/// P9-08: "Reminder SMS" action — same discoverability tier as "Insert template" in the
/// composer, opens a modal rather than an inline composer change. Template/message
/// selection reuses the same picker infra as P9-04's composer, but the preview here is
/// read-only (rule 31: UI may display calculated/resolved values for preview only) — the
/// reminder stays TemplateId-linked and is rendered fresh at dispatch, same as any other
/// template-linked message, not committed as edited ad-hoc text at creation.
export function ReminderSmsDialog({
  threadId,
  open,
  onOpenChange,
}: {
  threadId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { data: templates } = useTemplates(true);
  const { data: settings } = useSettings();
  const createReminder = useCreateReminderMutation(threadId);

  const [templateId, setTemplateId] = useState("");
  const [eventTime, setEventTime] = useState("");
  const [preview, setPreview] = useState<string | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      setTemplateId("");
      setEventTime("");
      setPreview(null);
    }
  }, [open]);

  useEffect(() => {
    if (!templateId) {
      setPreview(null);
      return;
    }
    setPreviewLoading(true);
    apiClient
      .get<{ renderedBody: string }>(`/api/threads/${threadId}/templates/${templateId}/preview`)
      .then((res) => setPreview(res.renderedBody))
      .catch(() => setPreview(null))
      .finally(() => setPreviewLoading(false));
  }, [templateId, threadId]);

  // Rule 9/10: minimum selectable Event Time = Current Time + Reminder Offset. Enforced
  // for real server-side (rule 8/31 — this is a UX aid, not the source of truth); the
  // DateTimePicker's minDate only disables whole days before this instant (P9-03's
  // documented day-granularity simplification), so the submit-time check below is what
  // actually blocks a same-day-but-too-early pick.
  const reminderOffsetMinutes = settings?.reminderOffsetMinutes ?? 1440;
  const minEventTime = new Date(Date.now() + reminderOffsetMinutes * 60_000);

  const handleSubmit = async () => {
    if (!templateId) {
      toast.error("Pick a template");
      return;
    }
    if (!eventTime) {
      toast.error("Pick an Event Time");
      return;
    }

    const eventTimeDate = new Date(eventTime);
    if (eventTimeDate < minEventTime) {
      toast.error(
        `Event Time must be at least ${Math.round(reminderOffsetMinutes / 60)}h from now (the current Reminder Offset).`,
      );
      return;
    }

    try {
      await createReminder.mutateAsync({ templateId: Number(templateId), eventTime: eventTimeDate.toISOString() });
      toast.success("Reminder SMS scheduled");
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error, "Could not schedule reminder"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reminder SMS</DialogTitle>
          <DialogDescription>
            Event-based — the send time is calculated automatically from the Event Time and the
            configured Reminder Offset.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="reminder-template">Template</Label>
            <Select value={templateId} onValueChange={setTemplateId}>
              <SelectTrigger id="reminder-template">
                <SelectValue placeholder="Select a template..." />
              </SelectTrigger>
              <SelectContent>
                {(templates ?? []).map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {templateId && (
            <div className="space-y-1.5">
              <Label>Preview (read-only)</Label>
              <p className="rounded-md border bg-muted/40 p-2 text-sm text-muted-foreground">
                {previewLoading ? "Loading..." : (preview ?? "—")}
              </p>
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="reminder-event-time">Event Time</Label>
            <DateTimePicker id="reminder-event-time" value={eventTime} onChange={setEventTime} minDate={minEventTime} />
          </div>

          {eventTime && (
            <p className="text-xs text-muted-foreground">
              Will send at{" "}
              {new Date(new Date(eventTime).getTime() - reminderOffsetMinutes * 60_000).toLocaleString()} (read-only,
              server-calculated).
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createReminder.isPending}>
            {createReminder.isPending ? "Scheduling..." : "Schedule reminder"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
