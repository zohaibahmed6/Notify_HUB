import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

const DEFAULT_DUE_DATES = [
  { priority: "Urgent", offset: "+4 hours" },
  { priority: "High", offset: "+1 day" },
  { priority: "Medium", offset: "+3 days" },
  { priority: "Low", offset: "+7 days" },
];

/// Read-only — TaskDueDateDefaults are hardcoded Domain constants (FR-008), not
/// SystemSetting-backed; this tab surfaces them for visibility rather than making them
/// editable, which would be new scope beyond what was requested.
export function TaskTab() {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Task defaults</CardTitle>
        <CardDescription>Due dates auto-suggested by priority when creating a task (not editable here).</CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="space-y-1.5 text-sm">
          {DEFAULT_DUE_DATES.map((d) => (
            <li key={d.priority} className="flex justify-between">
              <span className="text-muted-foreground">{d.priority}</span>
              <span>{d.offset}</span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
