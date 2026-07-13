import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateTemplateMutation } from "@/hooks/useTemplates";
import { errorMessage } from "@/lib/errorMessage";
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
import type { TemplateTriggerType } from "@/types/templates";

const TRIGGER_TYPES: TemplateTriggerType[] = ["AppointmentReminder", "MedicationAlert", "PrescriptionAlert"];

// Create-only counterpart to the legacy TemplatesPage's inline TemplateForm (which isn't
// exported and stays untouched) — used by the command palette's "New template" quick
// action so it can be triggered from anywhere, not just the Templates screen.
export function QuickCreateTemplateForm({ onDone }: { onDone: () => void }) {
  const [name, setName] = useState("");
  const [body, setBody] = useState("");
  const [triggerType, setTriggerType] = useState<TemplateTriggerType>("AppointmentReminder");
  const [offsetHours, setOffsetHours] = useState("48");

  const createTemplate = useCreateTemplateMutation();

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!name.trim() || !body.trim()) {
      toast.error("Name and body are required");
      return;
    }
    if (Number(offsetHours) <= 0) {
      toast.error("Offset hours must be greater than 0");
      return;
    }

    try {
      await createTemplate.mutateAsync({ name, body, triggerType, offsetHours: Number(offsetHours) });
      toast.success("Template created");
      onDone();
    } catch (error) {
      toast.error(errorMessage(error, "Template creation failed"));
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor="qc-template-name">Name</Label>
        <Input id="qc-template-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="qc-template-body">Body</Label>
        <Textarea id="qc-template-body" value={body} maxLength={1000} onChange={(e) => setBody(e.target.value)} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="qc-template-trigger">Trigger type</Label>
          <Select value={triggerType} onValueChange={(v) => setTriggerType(v as TemplateTriggerType)}>
            <SelectTrigger id="qc-template-trigger">
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
        <div className="space-y-1.5">
          <Label htmlFor="qc-template-offset">Offset hours</Label>
          <Input
            id="qc-template-offset"
            type="number"
            min={1}
            value={offsetHours}
            onChange={(e) => setOffsetHours(e.target.value)}
          />
        </div>
      </div>
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="ghost" size="sm" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" size="sm" disabled={createTemplate.isPending}>
          {createTemplate.isPending ? "Creating..." : "Create template"}
        </Button>
      </div>
    </form>
  );
}
