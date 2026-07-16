import { calculateSmsSegmentInfo } from "@/lib/smsSegmentCalculator";

// Live, non-blocking feedback under an SMS composer textarea: a red warning when the text
// contains a character outside the GSM-7 alphabet (which silently switches the whole
// message to UCS-2 and drops the per-segment limit from 160/153 to 70/67, multiplying the
// billed segment count), plus an always-visible segment count. Purely derived from `text`
// so it appears/disappears live as a character is added/removed — no local state needed.
export function SmsSegmentHint({ text }: { text: string }) {
  if (!text) return null;

  const { isGsm7, segmentCount } = calculateSmsSegmentInfo(text);

  return (
    <div className="flex items-center justify-between gap-2 px-1 text-[11px]">
      {!isGsm7 && (
        <span className="text-destructive">
          Contains special characters — sent as Unicode SMS, which costs more per segment
          than a standard SMS.
        </span>
      )}
      <span className="ml-auto shrink-0 text-muted-foreground">
        {segmentCount} segment{segmentCount === 1 ? "" : "s"} · {isGsm7 ? "GSM-7" : "Unicode"}
      </span>
    </div>
  );
}
