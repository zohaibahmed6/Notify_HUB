import { useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { BookmarkPlus, X } from "lucide-react";

import { useBookmarks } from "@/hooks/useBookmarks";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
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
import { SmsSegmentHint } from "@/components/v2/sms-segment-hint";
import type { CommunicationMode } from "@/types/templates";

const COMMUNICATION_MODES: { value: CommunicationMode; label: string }[] = [
  { value: "Sms", label: "SMS" },
  { value: "Email", label: "Email" },
  { value: "Letter", label: "Letter" },
];

export interface TemplateFormValues {
  name: string;
  body: string;
  isActive: boolean;
  communicationMode: CommunicationMode;
  bookmarkIds: number[];
}

export const EMPTY_TEMPLATE_FORM: TemplateFormValues = {
  name: "",
  body: "",
  isActive: true,
  communicationMode: "Sms",
  bookmarkIds: [],
};

function removeFirstOccurrence(text: string, needle: string): string {
  if (!needle) return text;
  const index = text.indexOf(needle);
  if (index === -1) return text;
  return text.slice(0, index) + text.slice(index + needle.length);
}

// Presentation only — mutation call owned by the caller (page decides create vs. update).
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
  // appending if the textarea hasn't been focused yet) and records it in the "Included
  // bookmarks" manifest (deduped).
  const insertBookmark = (bookmarkId: number, insertText: string) => {
    const el = bodyRef.current;
    const start = el?.selectionStart ?? values.body.length;
    const end = el?.selectionEnd ?? values.body.length;
    const nextBody = values.body.slice(0, start) + insertText + values.body.slice(end);
    setValues((v) => ({
      ...v,
      body: nextBody,
      bookmarkIds: v.bookmarkIds.includes(bookmarkId) ? v.bookmarkIds : [...v.bookmarkIds, bookmarkId],
    }));
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(start + insertText.length, start + insertText.length);
    });
  };

  // Removing a chip also strips the bookmark's text from the body — best-effort match
  // against the bookmark's *current* insertText, first occurrence only. If the body was
  // hand-edited since insertion, or the same bookmark was inserted twice, this can no-op
  // or leave a duplicate behind; the chip/id is removed from bookmarkIds regardless.
  const removeIncludedBookmark = (bookmarkId: number) => {
    const bookmark = (bookmarks ?? []).find((b) => b.id === bookmarkId);
    setValues((v) => ({
      ...v,
      bookmarkIds: v.bookmarkIds.filter((id) => id !== bookmarkId),
      body: bookmark ? removeFirstOccurrence(v.body, bookmark.insertText) : v.body,
    }));
  };

  const includedBookmarks = (bookmarks ?? []).filter((b) => values.bookmarkIds.includes(b.id));

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Details</CardTitle>
          <CardDescription>Name, channel, and when this template fires.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="tpl-name">Name</Label>
            <Input id="tpl-name" value={values.name} onChange={(e) => setValues({ ...values, name: e.target.value })} autoFocus />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="tpl-mode">Communication mode</Label>
            <Select
              value={values.communicationMode}
              onValueChange={(v) => setValues({ ...values, communicationMode: v as CommunicationMode })}
            >
              <SelectTrigger id="tpl-mode">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {COMMUNICATION_MODES.map((m) => (
                  <SelectItem key={m.value} value={m.value}>
                    {m.label}
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
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-base">Message</CardTitle>
              <CardDescription>The text that gets sent — merge fields resolve at send time.</CardDescription>
            </div>
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
                    <DropdownMenuItem
                      key={b.id}
                      onSelect={() => insertBookmark(b.id, b.insertText)}
                      className="flex-col items-start gap-0.5"
                    >
                      <span className="font-medium">{b.label}</span>
                      <span className="text-xs text-muted-foreground">{b.description}</span>
                    </DropdownMenuItem>
                  ))
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <div>
            {includedBookmarks.length === 0 ? (
              <p className="text-xs text-muted-foreground">No bookmarks included yet — use "Insert bookmark" above.</p>
            ) : (
              <div className="flex flex-wrap gap-1.5">
                {includedBookmarks.map((b) => (
                  <Badge key={b.id} variant="secondary" className="gap-1 pr-1">
                    {b.label}
                    <button
                      type="button"
                      onClick={() => removeIncludedBookmark(b.id)}
                      className="rounded-full p-0.5 hover:bg-background/60"
                      aria-label={`Remove ${b.label}`}
                    >
                      <X className="size-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}
          </div>
          <div className="space-y-1.5">
            <Textarea
              id="tpl-body"
              ref={bodyRef}
              value={values.body}
              maxLength={1000}
              rows={5}
              onChange={(e) => setValues({ ...values, body: e.target.value })}
            />
            {values.communicationMode === "Sms" && <SmsSegmentHint text={values.body} />}
            <p className="text-xs text-muted-foreground">
              Use <code className="font-mono">{"{{patient_name}}"}</code> or{" "}
              <code className="font-mono">{"{{appointment_time}}"}</code> — the only fields resolved at send time.
            </p>
          </div>
        </CardContent>
      </Card>

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
