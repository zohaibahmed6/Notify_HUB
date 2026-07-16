// API timestamps are UTC (see NotifyHubDbContext's UtcDateTimeConverter), but a string with no
// timezone designator is parsed by `new Date(...)` as local time, silently breaking the
// UTC->local conversion. Treat any designator-less string as UTC before parsing.
const hasTimezoneDesignator = (value: string) => /Z$|[+-]\d{2}:\d{2}$/.test(value);

export function parseUtc(value: string | null | undefined): Date | null {
  if (!value) return null;
  return new Date(hasTimezoneDesignator(value) ? value : `${value}Z`);
}

export function formatUtc(value: string | null | undefined, fallback = "—"): string {
  const date = parseUtc(value);
  return date ? date.toLocaleString() : fallback;
}

const pad = (n: number) => String(n).padStart(2, "0");

// Fixed "yyyy-MM-dd HH:mm:ss" format in local time, unlike formatUtc's locale-dependent output.
export function formatUtcDateTime(value: string | null | undefined, fallback = "—"): string {
  const date = parseUtc(value);
  if (!date) return fallback;
  const datePart = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  const timePart = `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  return `${datePart} ${timePart}`;
}
