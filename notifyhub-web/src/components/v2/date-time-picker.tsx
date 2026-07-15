import { useEffect, useMemo, useRef, useState } from "react";
import { CalendarIcon, Clock as ClockIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

// P9-03: one shared date/time picker (Material-style date card + clock-face time
// picker, per Zohaib's reference image) swapped into every date/datetime input in the
// app — not per-screen variants. Drop-in replacement for native `<input type="date">`/
// `<input type="datetime-local">`: `value`/`onChange` use exactly the same string shapes
// ("yyyy-MM-dd" / "yyyy-MM-ddTHH:mm", local time, no timezone) so call sites that already
// do `new Date(value).toISOString()` don't need to change that part at all.

function pad2(n: number) {
  return String(n).padStart(2, "0");
}

function parseValue(value: string): { date: Date | undefined; hour24: number | null; minute: number | null } {
  if (!value) return { date: undefined, hour24: null, minute: null };
  const [datePart, timePart] = value.split("T");
  const [y, m, d] = datePart.split("-").map(Number);
  if (!y || !m || !d) return { date: undefined, hour24: null, minute: null };
  const date = new Date(y, m - 1, d);
  if (!timePart) return { date, hour24: null, minute: null };
  const [hh, mm] = timePart.split(":").map(Number);
  return { date, hour24: hh, minute: mm };
}

function formatDatePart(date: Date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
}

function formatDisplay(value: string, mode: "date" | "datetime") {
  const { date, hour24, minute } = parseValue(value);
  if (!date) return null;
  const dateLabel = date.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric", year: "numeric" });
  if (mode === "date" || hour24 === null) return dateLabel;
  const period = hour24 >= 12 ? "PM" : "AM";
  const hour12 = hour24 % 12 === 0 ? 12 : hour24 % 12;
  return `${dateLabel}, ${hour12}:${pad2(minute ?? 0)} ${period}`;
}

/** Angle (degrees, 0 = 12 o'clock, clockwise) from a pointer position relative to the
 * clock face's center — shared by both the hour and minute rings. */
function angleFromPointer(clientX: number, clientY: number, rect: DOMRect) {
  const cx = rect.left + rect.width / 2;
  const cy = rect.top + rect.height / 2;
  const dx = clientX - cx;
  const dy = clientY - cy;
  let deg = (Math.atan2(dx, -dy) * 180) / Math.PI;
  if (deg < 0) deg += 360;
  return deg;
}

function ClockFace({
  hour24,
  minute,
  phase,
  onChangeHour,
  onChangeMinute,
  onPhaseSettled,
  onSelectPhase,
}: {
  hour24: number;
  minute: number;
  phase: "hour" | "minute";
  onChangeHour: (hour24: number) => void;
  onChangeMinute: (minute: number) => void;
  onPhaseSettled: () => void;
  onSelectPhase: (phase: "hour" | "minute") => void;
}) {
  const faceRef = useRef<HTMLDivElement>(null);
  const draggingRef = useRef(false);

  const isPM = hour24 >= 12;
  const hour12 = hour24 % 12 === 0 ? 12 : hour24 % 12;

  const applyFromPointer = (clientX: number, clientY: number) => {
    const el = faceRef.current;
    if (!el) return;
    const deg = angleFromPointer(clientX, clientY, el.getBoundingClientRect());
    if (phase === "hour") {
      let h12 = Math.round(deg / 30) % 12;
      if (h12 === 0) h12 = 12;
      const nextHour24 = isPM ? (h12 === 12 ? 12 : h12 + 12) : h12 === 12 ? 0 : h12;
      onChangeHour(nextHour24);
    } else {
      const m = Math.round(deg / 6) % 60;
      onChangeMinute(m);
    }
  };

  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId);
    draggingRef.current = true;
    applyFromPointer(event.clientX, event.clientY);
  };
  const handlePointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!draggingRef.current) return;
    applyFromPointer(event.clientX, event.clientY);
  };
  const handlePointerUp = () => {
    if (!draggingRef.current) return;
    draggingRef.current = false;
    onPhaseSettled();
  };

  const ticks = useMemo(() => {
    const count = 12;
    return Array.from({ length: count }, (_, i) => {
      const value = phase === "hour" ? (i === 0 ? 12 : i) : i * 5;
      const angleDeg = i * 30;
      return { value, angleDeg };
    });
  }, [phase]);

  const selectedAngle = phase === "hour" ? (hour12 % 12) * 30 : minute * 6;

  return (
    <div className="flex flex-col items-center gap-3">
      <div className="flex items-center gap-2 text-3xl font-medium tabular-nums">
        <button
          type="button"
          onClick={() => onSelectPhase("hour")}
          className={cn("rounded-md px-2 py-0.5", phase === "hour" && "bg-primary/10 text-primary")}
        >
          {pad2(hour12)}
        </button>
        <span>:</span>
        <button
          type="button"
          onClick={() => onSelectPhase("minute")}
          className={cn("rounded-md px-2 py-0.5", phase === "minute" && "bg-primary/10 text-primary")}
        >
          {pad2(minute)}
        </button>
        <div className="ml-2 flex flex-col text-xs font-medium text-muted-foreground">
          <button
            type="button"
            onClick={() => onChangeHour(hour12 === 12 ? 0 : hour12)}
            className={cn("rounded px-1.5", !isPM && "bg-primary text-primary-foreground")}
          >
            AM
          </button>
          <button
            type="button"
            onClick={() => onChangeHour(hour12 === 12 ? 12 : hour12 + 12)}
            className={cn("rounded px-1.5", isPM && "bg-primary text-primary-foreground")}
          >
            PM
          </button>
        </div>
      </div>

      <div
        ref={faceRef}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        className="relative size-56 shrink-0 touch-none select-none rounded-full bg-muted"
      >
        {/* Hand: rotated line from center to the selected tick, Material-clock style. */}
        <div
          className="absolute left-1/2 top-1/2 h-[calc(50%-1.75rem)] w-0.5 origin-top -translate-x-1/2 rounded-full bg-primary"
          style={{ transform: `rotate(${selectedAngle}deg)` }}
        />
        <div className="absolute left-1/2 top-1/2 size-2 -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary" />
        <div
          className="absolute size-8 -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary"
          style={{
            left: `${50 + 38 * Math.sin((selectedAngle * Math.PI) / 180)}%`,
            top: `${50 - 38 * Math.cos((selectedAngle * Math.PI) / 180)}%`,
          }}
        />

        {ticks.map(({ value, angleDeg }) => {
          const active = phase === "hour" ? value % 12 === hour12 % 12 : value === minute || (value === 0 && minute >= 58);
          const x = 50 + 38 * Math.sin((angleDeg * Math.PI) / 180);
          const y = 50 - 38 * Math.cos((angleDeg * Math.PI) / 180);
          return (
            <span
              key={value}
              className={cn(
                "pointer-events-none absolute flex size-8 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full text-sm tabular-nums",
                active ? "text-primary-foreground" : "text-foreground",
              )}
              style={{ left: `${x}%`, top: `${y}%` }}
            >
              {pad2(value)}
            </span>
          );
        })}
      </div>
    </div>
  );
}

export function DateTimePicker({
  value,
  onChange,
  onCommit,
  mode = "datetime",
  timeRequired = true,
  minDate,
  placeholder = "Select date",
  disabled,
  id,
  className,
  variant = "default",
}: {
  value: string;
  onChange: (value: string) => void;
  /** Fired once with the current value when the popover closes (Done button, outside
   * click, Escape) while a value is set — unlike onChange, which fires continuously
   * during interaction (once per clock-drag tick), this fires once per "the user is
   * done picking." Optional; callers that don't need a discrete commit point (most)
   * can ignore it. */
  onCommit?: (value: string) => void;
  mode?: "date" | "datetime";
  timeRequired?: boolean;
  minDate?: Date;
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  className?: string;
  /** "compact": plain bordered trigger matching Input's height/border with a trailing
   * 14px Calendar icon, used by the dense filter-bar grids (Task board/SMS
   * History/Audit log). "default" (unchanged): the existing outlined-button trigger with
   * a leading icon, used everywhere else (forms/dialogs). */
  variant?: "default" | "compact";
}) {
  const [open, setOpen] = useState(false);
  const [step, setStep] = useState<"date" | "time">("date");
  const [clockPhase, setClockPhase] = useState<"hour" | "minute">("hour");

  const parsed = parseValue(value);

  useEffect(() => {
    if (open) {
      setStep("date");
      setClockPhase("hour");
    }
  }, [open]);

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next && value) onCommit?.(value);
  };

  const commitDate = (date: Date | undefined) => {
    if (!date) return;
    const datePart = formatDatePart(date);
    const timePart = parsed.hour24 !== null ? `${pad2(parsed.hour24)}:${pad2(parsed.minute ?? 0)}` : timeRequired ? "" : "00:00";
    const next = mode === "date" || !timePart ? datePart : `${datePart}T${timePart}`;
    onChange(next);
    if (mode === "datetime") setStep("time");
  };

  const commitTime = (hour24: number, minute: number) => {
    const datePart = parsed.date ? formatDatePart(parsed.date) : formatDatePart(new Date());
    onChange(`${datePart}T${pad2(hour24)}:${pad2(minute)}`);
  };

  const currentHour24 = parsed.hour24 ?? 9;
  const currentMinute = parsed.minute ?? 0;

  const display = formatDisplay(value, mode);

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        {variant === "compact" ? (
          <button
            id={id}
            type="button"
            disabled={disabled}
            className={cn(
              "flex h-8 w-full items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
              !display && "text-muted-foreground",
              className,
            )}
          >
            <span className="truncate">{display ?? placeholder}</span>
            <CalendarIcon className="size-3.5 shrink-0 text-muted-foreground" />
          </button>
        ) : (
          <Button
            id={id}
            type="button"
            variant="outline"
            disabled={disabled}
            className={cn("w-full justify-start gap-2 font-normal", !display && "text-muted-foreground", className)}
          >
            {mode === "date" ? <CalendarIcon className="size-4" /> : <ClockIcon className="size-4" />}
            {display ?? placeholder}
          </Button>
        )}
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        {mode === "datetime" && (
          <div className="flex items-center justify-between border-b bg-primary px-4 py-3 text-primary-foreground">
            <div className="text-xs font-medium uppercase tracking-wide opacity-80">
              {step === "date" ? "Select date" : "Select time"}
            </div>
            <div className="flex gap-1">
              <button
                type="button"
                onClick={() => setStep("date")}
                className={cn("rounded px-2 py-0.5 text-sm", step === "date" && "bg-primary-foreground/20")}
              >
                {parsed.date ? parsed.date.toLocaleDateString(undefined, { month: "short", day: "numeric" }) : "Date"}
              </button>
              <button
                type="button"
                onClick={() => parsed.date && setStep("time")}
                disabled={!parsed.date}
                className={cn("rounded px-2 py-0.5 text-sm disabled:opacity-50", step === "time" && "bg-primary-foreground/20")}
              >
                {parsed.hour24 !== null ? `${pad2(currentHour24 % 12 === 0 ? 12 : currentHour24 % 12)}:${pad2(currentMinute)}` : "Time"}
              </button>
            </div>
          </div>
        )}

        {step === "date" ? (
          <Calendar
            mode="single"
            selected={parsed.date}
            onSelect={commitDate}
            disabled={minDate ? { before: minDate } : undefined}
            autoFocus
          />
        ) : (
          <div className="p-4">
            <ClockFace
              hour24={currentHour24}
              minute={currentMinute}
              phase={clockPhase}
              onChangeHour={(h) => commitTime(h, currentMinute)}
              onChangeMinute={(m) => commitTime(currentHour24, m)}
              onSelectPhase={setClockPhase}
              onPhaseSettled={() => {
                if (clockPhase === "hour") {
                  setClockPhase("minute");
                } else {
                  setOpen(false);
                }
              }}
            />
          </div>
        )}

        <div className="flex items-center justify-end gap-2 border-t p-2">
          <Button type="button" size="sm" variant="ghost" onClick={() => setOpen(false)}>
            Done
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
