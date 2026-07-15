// Cosmetic only: stops the URL from showing a plain sequential thread id
// (e.g. `?thread=1000`) as a friendly decimal number. Not an access-control
// measure — trivially reversible by anyone reading this file.
const MASK = 0x5bd1e995;

export function encodeThreadId(id: number): string {
  // eslint-disable-next-line no-bitwise
  return ((id ^ MASK) >>> 0).toString(36);
}

export function decodeThreadId(token: string | null): number | null {
  // Reject anything that isn't a full base36 string outright — parseInt would
  // otherwise silently parse a leading valid prefix (e.g. "not-a-token" -> "not")
  // instead of failing, turning a malformed URL into a bogus-but-valid-looking id.
  if (!token || !/^[0-9a-z]+$/.test(token)) return null;
  const masked = parseInt(token, 36);
  if (!Number.isFinite(masked) || masked < 0) return null;
  // eslint-disable-next-line no-bitwise
  return (masked ^ MASK) >>> 0;
}
