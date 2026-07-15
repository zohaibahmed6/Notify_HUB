// Shared "day-granularity date range -> instant range" helper. Previously duplicated
// verbatim in AuditLogPage.tsx and AuditLogPageV2.tsx; extracted here so a third copy
// (the Task filter bar) doesn't get pasted in too.

/** <input type="date"> always takes/returns yyyy-mm-dd regardless of locale. Built from
 * local getters (not toISOString/UTC) so it reflects the viewer's actual local date. */
export function toDateInputValue(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

/** `daysBack` days before today (the viewer's local today), as a yyyy-mm-dd string
 * (e.g. 7 -> "last 7 days"). */
export function defaultFromDaysAgo(daysBack: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysBack);
  return toDateInputValue(d);
}

function localDayBoundary(value: string, endOfDay: boolean): string {
  const [y, m, d] = value.split("-").map(Number);
  return endOfDay ? new Date(y, m - 1, d, 23, 59, 59, 999).toISOString() : new Date(y, m - 1, d).toISOString();
}

/**
 * Converts day-granularity `from`/`to` <input type="date"> values into instant ISO
 * strings for a server query: `from` is local midnight of that day, `to` is local
 * `23:59:59.999` of that day (not midnight) — otherwise a same-day `from === to` range
 * collapses to a single instant and matches nothing against a server-side `<= to` filter.
 * Anchored to the viewer's local day, not UTC, so the query window actually matches the
 * calendar day shown in the date picker.
 */
export function toInstantRange(from: string, to: string): { from?: string; to?: string } {
  return {
    from: from ? localDayBoundary(from, false) : undefined,
    to: to ? localDayBoundary(to, true) : undefined,
  };
}
