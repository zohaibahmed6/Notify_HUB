import { cn } from "@/lib/utils";

// Plain-SVG mini bar chart for lightweight inline data-viz (audit activity-by-day,
// task status distribution) — deliberately not a charting library: no tooltips/zoom,
// just a glanceable shape. Values are pre-aggregated by the caller from data already
// fetched via existing hooks; this component does no fetching or aggregation itself.
export function Sparkline({
  values,
  className,
  barClassName,
}: {
  values: number[];
  className?: string;
  barClassName?: string;
}) {
  const max = Math.max(1, ...values);
  return (
    <div className={cn("flex h-8 items-end gap-0.5", className)}>
      {values.map((v, i) => (
        <div
          key={i}
          className={cn("min-w-[3px] flex-1 rounded-sm bg-primary/70", barClassName)}
          style={{ height: `${Math.max(4, (v / max) * 100)}%` }}
        />
      ))}
    </div>
  );
}

// Segmented distribution bar (e.g. task counts by status) — one flex-basis segment
// per entry, proportional to its share of the total.
export function DistributionBar({
  segments,
}: {
  segments: { label: string; value: number; className: string }[];
}) {
  const total = Math.max(
    1,
    segments.reduce((sum, s) => sum + s.value, 0),
  );
  return (
    <div className="flex h-2 w-full overflow-hidden rounded-full bg-muted" title={segments.map((s) => `${s.label}: ${s.value}`).join(" · ")}>
      {segments
        .filter((s) => s.value > 0)
        .map((s) => (
          <div
            key={s.label}
            className={s.className}
            style={{ width: `${(s.value / total) * 100}%` }}
          />
        ))}
    </div>
  );
}
