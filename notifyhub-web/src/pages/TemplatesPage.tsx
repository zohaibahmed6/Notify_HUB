import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateTemplateMutation, useTemplates, useUpdateTemplateMutation } from "@/hooks/useTemplates";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import type { TemplateDto, TemplateTriggerType } from "@/types/templates";

const TRIGGER_TYPES: TemplateTriggerType[] = ["AppointmentReminder", "MedicationAlert", "PrescriptionAlert"];

interface TemplateFormValues {
  name: string;
  body: string;
  triggerType: TemplateTriggerType;
  offsetHours: string;
}

const EMPTY_FORM: TemplateFormValues = { name: "", body: "", triggerType: "AppointmentReminder", offsetHours: "48" };

export default function TemplatesPage() {
  const { data: templates, isLoading } = useTemplates();
  const createTemplate = useCreateTemplateMutation();
  const updateTemplate = useUpdateTemplateMutation();

  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);

  const handleCreate = async (values: TemplateFormValues) => {
    try {
      await createTemplate.mutateAsync({
        name: values.name,
        body: values.body,
        triggerType: values.triggerType,
        offsetHours: Number(values.offsetHours),
      });
      toast.success("Template created");
      setShowCreateForm(false);
    } catch (error) {
      toast.error(errorMessage(error, "Template creation failed"));
    }
  };

  const handleUpdate = async (id: number, values: TemplateFormValues) => {
    try {
      await updateTemplate.mutateAsync({
        id,
        name: values.name,
        body: values.body,
        triggerType: values.triggerType,
        offsetHours: Number(values.offsetHours),
      });
      toast.success("Template updated");
      setEditingId(null);
    } catch (error) {
      toast.error(errorMessage(error, "Template update failed"));
    }
  };

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-semibold">Templates &amp; reminder rules</h1>
        <Button size="sm" onClick={() => setShowCreateForm((v) => !v)}>
          New template
        </Button>
      </div>

      {showCreateForm && (
        <div className="mb-4">
          <TemplateForm
            initial={EMPTY_FORM}
            submitLabel="Create"
            isSubmitting={createTemplate.isPending}
            onSubmit={handleCreate}
            onCancel={() => setShowCreateForm(false)}
          />
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">Loading...</p>
      ) : !templates || templates.length === 0 ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-1 text-center">
          <p className="font-medium">No templates yet</p>
          <p className="text-sm text-muted-foreground">Create one to start sending reminders and alerts.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {templates.map((template) => (
            <div key={template.id} className="overflow-hidden rounded-lg border">
              <div className="flex items-center justify-between gap-4 p-3">
                <div className="min-w-0">
                  <div className="text-sm font-medium">{template.name}</div>
                  <div className="text-xs text-muted-foreground">
                    {template.triggerType} · offset {template.offsetHours}h
                  </div>
                  <div className="mt-1 truncate text-xs text-muted-foreground">{template.body}</div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setEditingId(editingId === template.id ? null : template.id)}
                >
                  {editingId === template.id ? "Cancel" : "Edit"}
                </Button>
              </div>
              {editingId === template.id && (
                <div className="border-t bg-muted/40 p-3">
                  <TemplateForm
                    initial={toFormValues(template)}
                    submitLabel="Save"
                    isSubmitting={updateTemplate.isPending}
                    onSubmit={(values) => handleUpdate(template.id, values)}
                    onCancel={() => setEditingId(null)}
                  />
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function toFormValues(template: TemplateDto): TemplateFormValues {
  return {
    name: template.name,
    body: template.body,
    triggerType: template.triggerType,
    offsetHours: String(template.offsetHours),
  };
}

function TemplateForm({
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

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!values.name.trim() || !values.body.trim()) {
      toast.error("Name and body are required");
      return;
    }
    if (Number(values.offsetHours) <= 0) {
      toast.error("Offset hours must be greater than 0");
      return;
    }
    onSubmit(values);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3 rounded-md border bg-muted/40 p-4">
      <div className="space-y-1.5">
        <Label htmlFor="template-name">Name</Label>
        <Input
          id="template-name"
          value={values.name}
          onChange={(event) => setValues({ ...values, name: event.target.value })}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="template-body">Body</Label>
        <Textarea
          id="template-body"
          value={values.body}
          maxLength={1000}
          onChange={(event) => setValues({ ...values, body: event.target.value })}
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="template-trigger">Trigger type</Label>
          <select
            id="template-trigger"
            value={values.triggerType}
            onChange={(event) => setValues({ ...values, triggerType: event.target.value as TemplateTriggerType })}
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            {TRIGGER_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="template-offset">Offset hours</Label>
          <Input
            id="template-offset"
            type="number"
            min={1}
            value={values.offsetHours}
            onChange={(event) => setValues({ ...values, offsetHours: event.target.value })}
          />
        </div>
      </div>
      <div className="flex justify-end gap-2">
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
