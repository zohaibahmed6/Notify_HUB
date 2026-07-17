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

export const pad = (n: number) => String(n).padStart(2, "0");

// Quiet Hours has no date component — combine the bare UTC "HH:mm" with *today's* date to get
// the correct current UTC offset (DST-correct for right now), then read back the local HH:mm.
export function utcTimeToLocal(value: string): string {
  const [hours, minutes] = value.split(":").map(Number);
  const reference = new Date();
  reference.setUTCHours(hours, minutes, 0, 0);
  return `${pad(reference.getHours())}:${pad(reference.getMinutes())}`;
}

// Inverse of utcTimeToLocal — converts a local "HH:mm" back to UTC before sending to the API.
export function localTimeToUtc(value: string): string {
  const [hours, minutes] = value.split(":").map(Number);
  const reference = new Date();
  reference.setHours(hours, minutes, 0, 0);
  return `${pad(reference.getUTCHours())}:${pad(reference.getUTCMinutes())}`;
}

// Fixed "yyyy-MM-dd HH:mm:ss" format in local time, unlike formatUtc's locale-dependent output.
export function formatUtcDateTime(value: string | null | undefined, fallback = "—"): string {
  const date = parseUtc(value);
  if (!date) return fallback;
  const datePart = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  const timePart = `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  return `${datePart} ${timePart}`;
}

// Fixed "yyyy-MM-dd h:mm am/pm" format in local time — e.g. "2026-07-16 6:00 pm".
export function formatFriendly(value: string | null | undefined, fallback = "—"): string {
  const date = parseUtc(value);
  if (!date) return fallback;
  const datePart = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  const hours24 = date.getHours();
  const hours12 = hours24 % 12 || 12;
  const ampm = hours24 < 12 ? "am" : "pm";
  return `${datePart} ${hours12}:${pad(date.getMinutes())} ${ampm}`;
}

// Backend audit-detail strings embed raw ISO round-trip timestamps (C#'s `:o` format, e.g.
// "overdue since 2026-07-16T18:00:00.0000000Z") inside otherwise free-text messages. Find and
// reformat any such embedded timestamp in place rather than the whole string.
const ISO_TIMESTAMP = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z/g;

export function formatEmbeddedDates(text: string | null | undefined, fallback = "—"): string {
  if (!text) return fallback;
  return text.replace(ISO_TIMESTAMP, (match) => formatFriendly(match));
}
