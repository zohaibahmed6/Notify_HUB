import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Clock as ClockIcon } from "lucide-react";

import { useTemplates } from "@/hooks/useTemplates";
import { useCreateReminderMutation } from "@/hooks/useThreads";
import { useSettings } from "@/hooks/useSettings";
import { apiClient } from "@/lib/apiClient";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DateTimePicker } from "@/components/v2/date-time-picker";

/// P9-08: "Reminder SMS" action — same discoverability tier as "Insert template" in the
/// composer, opens a modal rather than an inline composer change.
///
/// **Rule 31 reversal**: the body used to be a locked read-only preview, TemplateId-linked
/// and rendered fresh at dispatch. It's now a freely-editable Textarea, same UX as the
/// composer's "Insert template" (selecting a template replaces the box with its resolved
/// text via handleTemplateChange, editable afterward — mirrors conversation-panel.tsx's
/// handleInsertTemplate/setDraft) — and the edited text is committed as RenderedBody at
/// creation (see ThreadsController.CreateReminder/MessageDispatcher.DispatchOneAsync).
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
  const [body, setBody] = useState("");
  const [bodyLoading, setBodyLoading] = useState(false);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (!open) {
      setTemplateId("");
      setEventTime("");
      setBody("");
    }
  }, [open]);

  // Selecting a template replaces the box with its resolved text (patient_name etc.
  // already substituted) — same behavior as the composer's "Insert template", not a
  // locked preview. Editable afterward: this is a plain useState set, not tied back to
  // templateId reactively, so further typing in the Textarea below is never overwritten.
  const handleTemplateChange = async (value: string) => {
    setTemplateId(value);
    const template = templates?.find((t) => String(t.id) === value);
    if (!template) return;
    setBodyLoading(true);
    try {
      const res = await apiClient.get<{ renderedBody: string }>(`/api/threads/${threadId}/templates/${value}/preview`);
      setBody(res.renderedBody);
    } catch (error) {
      toast.error(errorMessage(error, "Couldn't resolve template fields, inserting raw text"));
      setBody(template.body);
    } finally {
      setBodyLoading(false);
    }
  };

  // Inserts text at the Textarea's current caret position, replacing any active
  // selection, then restores focus with the caret placed immediately after the inserted
  // text — same pattern as TemplateForm's insertBookmark. Falls back to appending at the
  // end if the Textarea hasn't been focused yet (selectionStart/End undefined).
  const insertAtCursor = (insertText: string) => {
    const el = bodyRef.current;
    const start = el?.selectionStart ?? body.length;
    const end = el?.selectionEnd ?? body.length;
    const nextBody = body.slice(0, start) + insertText + body.slice(end);
    setBody(nextBody);
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(start + insertText.length, start + insertText.length);
    });
  };

  // DateTimePicker.onChange fires continuously while the user drags the clock hand (once
  // per hour/minute tick) — inserting on every tick would litter the message with dozens
  // of partial values. onCommit instead fires once, when the picker's popover closes
  // (Done / outside click / Escape) — "the user is done selecting" — which is what
  // actually drives insertion here.
  const handleEventTimeCommit = (value: string) => {
    insertAtCursor(new Date(value).toLocaleString());
  };

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

    if (!body.trim()) {
      toast.error("Message text can't be empty");
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
      await createReminder.mutateAsync({ templateId: Number(templateId), eventTime: eventTimeDate.toISOString(), body });
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
            <Select value={templateId} onValueChange={handleTemplateChange}>
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

          <div className="space-y-1.5">
            <Label htmlFor="reminder-body">Message</Label>
            <Textarea
              id="reminder-body"
              ref={bodyRef}
              value={body}
              maxLength={1000}
              rows={5}
              placeholder={bodyLoading ? "Loading..." : "Select a template above, or type a message..."}
              onChange={(e) => setBody(e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="reminder-event-time" className="flex items-center gap-1.5">
              <ClockIcon className="size-3.5" />
              Event Time
            </Label>
            <DateTimePicker
              id="reminder-event-time"
              value={eventTime}
              onChange={setEventTime}
              onCommit={handleEventTimeCommit}
              minDate={minEventTime}
            />
            <p className="text-xs text-muted-foreground">
              Inserted into the message above at your cursor once you finish picking a date/time.
            </p>
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
