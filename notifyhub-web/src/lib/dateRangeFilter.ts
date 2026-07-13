// Shared "day-granularity date range -> instant range" helper. Previously duplicated
// verbatim in AuditLogPage.tsx and AuditLogPageV2.tsx; extracted here so a third copy
// (the Task filter bar) doesn't get pasted in too.

/** <input type="date"> always takes/returns yyyy-mm-dd regardless of locale. */
export function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** `daysBack` days before today, as a yyyy-mm-dd string (e.g. 7 -> "last 7 days"). */
export function defaultFromDaysAgo(daysBack: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - daysBack);
  return toDateInputValue(d);
}

/**
 * Converts day-granularity `from`/`to` <input type="date"> values into instant ISO
 * strings for a server query: `from` is UTC midnight of that day, `to` is forced to
 * `T23:59:59.999Z` of that day (not midnight) — otherwise a same-day `from === to` range
 * collapses to a single instant and matches nothing against a server-side `<= to` filter.
 */
export function toInstantRange(from: string, to: string): { from?: string; to?: string } {
  return {
    from: from ? new Date(from).toISOString() : undefined,
    to: to ? new Date(`${to}T23:59:59.999Z`).toISOString() : undefined,
  };
}
