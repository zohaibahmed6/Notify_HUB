import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { ArrowLeft, Eye, FileText, Plus, Sparkles } from "lucide-react";

import { useCreateTemplateMutation, useTemplates, useUpdateTemplateMutation } from "@/hooks/useTemplates";
import { errorMessage } from "@/lib/errorMessage";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { StatusBadge } from "@/components/v2/status-badge";
import { TRIGGER_TYPE_CONFIG } from "@/components/v2/status-config";
import { MergeFieldText } from "@/components/v2/merge-field-text";
import { EmptyState } from "@/components/v2/empty-state";
import { EMPTY_TEMPLATE_FORM, TemplateForm, type TemplateFormValues } from "@/components/v2/template-form";
import { Skeleton } from "@/components/ui/skeleton";
import type { TemplateDto } from "@/types/templates";

type Selection = number | "new" | null;

// P9-01e: OffsetHours is no longer user-editable — this satisfies the backend's still-
// required CreateTemplateRequest.OffsetHours field without a breaking migration.
const LEGACY_OFFSET_HOURS_PLACEHOLDER = 24;

export default function TemplatesPageV2() {
  const [activeFilter, setActiveFilter] = useState<"all" | "Active" | "Inactive">("all");
  const { data: templates, isLoading } = useTemplates(
    activeFilter === "all" ? undefined : activeFilter === "Active",
  );
  const createTemplate = useCreateTemplateMutation();
  const updateTemplate = useUpdateTemplateMutation();

  const [searchParams, setSearchParams] = useSearchParams();
  const [selected, setSelected] = useState<Selection>(() => {
    const fromUrl = searchParams.get("template");
    return fromUrl ? Number(fromUrl) : null;
  });
  const [isEditing, setIsEditing] = useState(false);
  const [previewMode, setPreviewMode] = useState<"preview" | "raw">("preview");

  useEffect(() => {
    const fromUrl = searchParams.get("template");
    const next = fromUrl ? Number(fromUrl) : null;
    if (next !== null && next !== selected) {
      setSelected(next);
      setIsEditing(false);
    }
  }, [searchParams]);

  const select = (id: number) => {
    setSelected(id);
    setIsEditing(false);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("template", String(id));
      return next;
    });
  };

  const startNew = () => {
    setSelected("new");
    setIsEditing(true);
  };

  // Mobile: single-pane with back-navigation instead of squeezing list+detail side by
  // side (P9-00), same pattern as InboxPageV2's ThreadList/ConversationPanelV2 split.
  const handleBack = () => {
    setSelected(null);
    setIsEditing(false);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("template");
      return next;
    });
  };

  const selectedTemplate: TemplateDto | undefined =
    typeof selected === "number" ? templates?.find((t) => t.id === selected) : undefined;

  const handleCreate = async (values: TemplateFormValues) => {
    try {
      const created = await createTemplate.mutateAsync({
        name: values.name,
        body: values.body,
        triggerType: values.triggerType,
        // OffsetHours is no longer a UI-editable field (P9-01e — dead once P9-08's
        // Reminder SMS engine ships) but the backend column/request field is still
        // required (avoids a breaking migration), so a fixed placeholder is sent.
        offsetHours: LEGACY_OFFSET_HOURS_PLACEHOLDER,
      });
      if (!values.isActive) {
        await updateTemplate.mutateAsync({ id: created.id, isActive: false });
      }
      toast.success("Template created");
      select(created.id);
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
        isActive: values.isActive,
      });
      toast.success("Template updated");
      setIsEditing(false);
    } catch (error) {
      toast.error(errorMessage(error, "Template update failed"));
    }
  };

  return (
    <div className="flex h-full">
      <aside
        className={cn(
          "flex h-full w-full shrink-0 flex-col border-r md:w-80",
          selected !== null && "hidden md:flex",
        )}
      >
        <div className="flex shrink-0 items-center justify-between border-b p-3">
          <h1 className="text-sm font-semibold">Templates</h1>
          <Button size="sm" className="h-7 gap-1 px-2 text-xs" onClick={startNew}>
            <Plus className="size-3.5" />
            New
          </Button>
        </div>

        <div className="shrink-0 border-b p-2">
          <Select value={activeFilter} onValueChange={(v) => setActiveFilter(v as typeof activeFilter)}>
            <SelectTrigger className="h-8 text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All templates</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Inactive">Inactive</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto">
          {isLoading ? (
            <div className="space-y-3 p-3">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="space-y-2">
                  <Skeleton className="h-4 w-2/3" />
                  <Skeleton className="h-3 w-1/3" />
                </div>
              ))}
            </div>
          ) : !templates || templates.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No templates yet"
              description="Create one to start sending reminders and alerts."
            />
          ) : (
            <ul>
              {templates.map((template) => {
                const trigger = TRIGGER_TYPE_CONFIG[template.triggerType];
                const isSelected = selected === template.id;
                return (
                  <li key={template.id}>
                    <button
                      type="button"
                      onClick={() => select(template.id)}
                      className={cn(
                        "flex w-full flex-col items-start gap-1 border-b px-3 py-2.5 text-left transition-colors hover:bg-accent",
                        isSelected && "bg-accent",
                      )}
                    >
                      <span className={cn("truncate text-sm font-medium", !template.isActive && "text-muted-foreground")}>
                        {template.name}
                      </span>
                      <span className="flex items-center gap-1.5">
                        <StatusBadge {...trigger} size="xs" />
                        {!template.isActive && (
                          <span className="rounded-full border border-dashed px-1.5 text-2xs text-muted-foreground">Inactive</span>
                        )}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </aside>

      <section
        className={cn(
          "min-w-0 flex-1 overflow-y-auto p-6",
          selected === null && "hidden md:block",
        )}
      >
        {selected !== null && (
          <Button variant="ghost" size="sm" className="mb-3 -ml-2 gap-1.5 md:hidden" onClick={handleBack}>
            <ArrowLeft className="size-3.5" />
            Back to templates
          </Button>
        )}
        {selected === "new" ? (
          <div className="mx-auto max-w-lg">
            <h2 className="mb-4 text-lg font-semibold">New template</h2>
            <TemplateForm
              initial={EMPTY_TEMPLATE_FORM}
              submitLabel="Create template"
              isSubmitting={createTemplate.isPending}
              onSubmit={handleCreate}
              onCancel={() => setSelected(null)}
            />
          </div>
        ) : !selectedTemplate ? (
          <EmptyState icon={FileText} title="Select a template" description="Pick one from the list to preview or edit it." />
        ) : isEditing ? (
          <div className="mx-auto max-w-lg">
            <h2 className="mb-4 text-lg font-semibold">Edit template</h2>
            <TemplateForm
              initial={{
                name: selectedTemplate.name,
                body: selectedTemplate.body,
                triggerType: selectedTemplate.triggerType,
                isActive: selectedTemplate.isActive,
              }}
              submitLabel="Save"
              isSubmitting={updateTemplate.isPending}
              onSubmit={(values) => handleUpdate(selectedTemplate.id, values)}
              onCancel={() => setIsEditing(false)}
            />
          </div>
        ) : (
          <div className="mx-auto max-w-lg">
            <div className="mb-4 flex items-start justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold">{selectedTemplate.name}</h2>
                <div className="mt-1 flex items-center gap-2">
                  <StatusBadge {...TRIGGER_TYPE_CONFIG[selectedTemplate.triggerType]} />
                  {!selectedTemplate.isActive && (
                    <span className="rounded-full border border-dashed px-2 py-0.5 text-2xs text-muted-foreground">Inactive</span>
                  )}
                </div>
              </div>
              <Button variant="outline" size="sm" onClick={() => setIsEditing(true)}>
                Edit
              </Button>
            </div>

            <div className="mb-2 flex items-center gap-1">
              <Button
                variant={previewMode === "preview" ? "secondary" : "ghost"}
                size="sm"
                className="h-7 gap-1.5 text-xs"
                onClick={() => setPreviewMode("preview")}
              >
                <Sparkles className="size-3.5" />
                Sample preview
              </Button>
              <Button
                variant={previewMode === "raw" ? "secondary" : "ghost"}
                size="sm"
                className="h-7 gap-1.5 text-xs"
                onClick={() => setPreviewMode("raw")}
              >
                <Eye className="size-3.5" />
                Raw source
              </Button>
            </div>

            <div className="rounded-lg border bg-muted/40 p-4">
              <div className="max-w-[85%] rounded-lg bg-primary px-3 py-2 text-sm text-primary-foreground">
                <MergeFieldText body={selectedTemplate.body} mode={previewMode} />
              </div>
            </div>
            {previewMode === "preview" && (
              <p className="mt-2 text-xs text-muted-foreground">
                Sample values shown for illustration — actual sends resolve{" "}
                <code className="font-mono">{"{{patient_name}}"}</code> and{" "}
                <code className="font-mono">{"{{appointment_time}}"}</code> from real patient/appointment data.
              </p>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
