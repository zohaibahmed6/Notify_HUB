import { Fragment } from "react";

// Mirrors NotifyHub.Domain/Messaging/TemplateRenderer.cs's actual behavior: only
// `patient_name` (always) and `appointment_time` (AppointmentReminder sends only, when
// the trigger reference parses) are ever resolved server-side — everything else is left
// as the literal `{{field}}` token (TemplateRenderer.Render's documented degrade path).
// The "Preview" mode below uses illustrative sample values for exactly those two known
// fields and nothing else, so it can't imply the app resolves fields it doesn't.
const KNOWN_MERGE_FIELDS: Record<string, string> = {
  patient_name: "Jordan Lee",
  appointment_time: "Jul 22, 2026, 2:30 PM",
};

const MERGE_FIELD_PATTERN = /(\{\{\s*\w+\s*\}\})/g;
const MERGE_FIELD_CAPTURE = /^\{\{\s*(\w+)\s*\}\}$/;

export function MergeFieldText({ body, mode }: { body: string; mode: "raw" | "preview" }) {
  const parts = body.split(MERGE_FIELD_PATTERN);

  return (
    <>
      {parts.map((part, i) => {
        const match = part.match(MERGE_FIELD_CAPTURE);
        if (!match) return <Fragment key={i}>{part}</Fragment>;

        const field = match[1];
        const sample = KNOWN_MERGE_FIELDS[field];

        if (mode === "preview" && sample) {
          // text-current + a translucent light overlay (not the tinted-primary combo used
          // elsewhere) — this renders inside a solid bg-primary message bubble, where
          // primary-colored text on a primary-tinted background is nearly invisible.
          return (
            <span key={i} className="rounded bg-white/20 px-1 font-semibold underline decoration-dotted underline-offset-2">
              {sample}
            </span>
          );
        }

        return (
          <span
            key={i}
            className="rounded border border-dashed border-amber-400 bg-amber-50 px-1 font-mono text-[0.8em] text-amber-700 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-300"
            title={mode === "preview" ? "No sample value — sent as-is if unresolved at send time" : "Merge field"}
          >
            {part}
          </span>
        );
      })}
    </>
  );
}
