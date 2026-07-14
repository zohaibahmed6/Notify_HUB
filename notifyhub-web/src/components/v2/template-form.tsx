import { useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { BookmarkPlus } from "lucide-react";

import { useBookmarks } from "@/hooks/useBookmarks";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { TemplateTriggerType } from "@/types/templates";

const TRIGGER_TYPES: TemplateTriggerType[] = ["AppointmentReminder", "MedicationAlert", "PrescriptionAlert"];

export interface TemplateFormValues {
  name: string;
  body: string;
  triggerType: TemplateTriggerType;
  isActive: boolean;
}

export const EMPTY_TEMPLATE_FORM: TemplateFormValues = {
  name: "",
  body: "",
  triggerType: "AppointmentReminder",
  isActive: true,
};

// Same validation rules as legacy TemplatesPage.tsx's inline TemplateForm — presentation
// only, mutation call owned by the caller (page decides create vs. update).
export function TemplateForm({
  initial,
  submitLabel,
  isSubmitting,
  onSubmit,
  onCancel,
}: {
  initial: TemplateFormValues;
  submitLabel: string;
  isSubmitting: boolean;
  onSubmit: (values: TemplateFormValues) => void;
  onCancel: () => void;
}) {
  const [values, setValues] = useState(initial);
  const bodyRef = useRef<HTMLTextAreaElement>(null);
  const { data: bookmarks } = useBookmarks();

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!values.name.trim() || !values.body.trim()) {
      toast.error("Name and body are required");
      return;
    }
    onSubmit(values);
  };

  // §5: inserts a bookmark's InsertText at the current cursor position (falls back to
  // appending if the textarea hasn't been focused yet).
  const insertBookmark = (insertText: string) => {
    const el = bodyRef.current;
    const start = el?.selectionStart ?? values.body.length;
    const end = el?.selectionEnd ?? values.body.length;
    const nextBody = values.body.slice(0, start) + insertText + values.body.slice(end);
    setValues({ ...values, body: nextBody });
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(start + insertText.length, start + insertText.length);
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor="tpl-name">Name</Label>
        <Input id="tpl-name" value={values.name} onChange={(e) => setValues({ ...values, name: e.target.value })} autoFocus />
      </div>
      <div className="space-y-1.5">
        <div className="flex items-center justify-between">
          <Label htmlFor="tpl-body">Body</Label>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button type="button" variant="outline" size="sm" className="h-7 gap-1.5 text-xs">
                <BookmarkPlus className="size-3.5" />
                Insert bookmark
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-64">
              {!bookmarks || bookmarks.length === 0 ? (
                <DropdownMenuItem disabled>No bookmarks yet</DropdownMenuItem>
              ) : (
                bookmarks.map((b) => (
                  <DropdownMenuItem key={b.id} onSelect={() => insertBookmark(b.insertText)} className="flex-col items-start gap-0.5">
                    <span className="font-medium">{b.label}</span>
                    <span className="text-xs text-muted-foreground">{b.description}</span>
                  </DropdownMenuItem>
                ))
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
        <Textarea
          id="tpl-body"
          ref={bodyRef}
          value={values.body}
          maxLength={1000}
          rows={5}
          onChange={(e) => setValues({ ...values, body: e.target.value })}
        />
        <p className="text-xs text-muted-foreground">
          Use <code className="font-mono">{"{{patient_name}}"}</code> or{" "}
          <code className="font-mono">{"{{appointment_time}}"}</code> — the only fields resolved at send time.
        </p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="tpl-trigger">Trigger type</Label>
        <Select value={values.triggerType} onValueChange={(v) => setValues({ ...values, triggerType: v as TemplateTriggerType })}>
          <SelectTrigger id="tpl-trigger">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TRIGGER_TYPES.map((t) => (
              <SelectItem key={t} value={t}>
                {t}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <label htmlFor="tpl-active" className="flex items-center gap-2 text-sm">
        <input
          id="tpl-active"
          type="checkbox"
          checked={values.isActive}
          onChange={(e) => setValues({ ...values, isActive: e.target.checked })}
          className="size-4 rounded border-input"
        />
        Active
      </label>
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" size="sm" disabled={isSubmitting}>
          {isSubmitting ? "Saving..." : submitLabel}
        </Button>
      </div>
    </form>
  );
}
