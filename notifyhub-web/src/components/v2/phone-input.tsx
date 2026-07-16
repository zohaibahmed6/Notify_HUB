import { useEffect, useMemo, useState } from "react";
import { Check, ChevronsUpDown } from "lucide-react";
import { AsYouType } from "libphonenumber-js/min";

import { COUNTRIES, isValidForCountry, type CountryCode } from "@/lib/phone";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { cn } from "@/lib/utils";

// Dynamically imported (not a top-level `import "..."`) so its ~250-country SVG sprite
// sheet becomes its own lazy-loaded chunk instead of bloating the app's main CSS bundle
// for every page — this field is only ever shown inside the New Conversation dialog.
// Emoji flags (🇺🇸) were tried first but don't reliably render as actual flag glyphs on
// every platform (confirmed falling back to plain "US" text on Windows/Chromium during
// manual verification), hence bundled SVGs instead of a zero-cost Unicode approach.
let flagIconsCssLoaded: Promise<unknown> | null = null;
function ensureFlagIconsCss() {
  flagIconsCssLoaded ??= import("flag-icons/css/flag-icons.min.css");
  return flagIconsCssLoaded;
}

// Flag/calling-code picker (searchable Popover+Command combobox, same pattern as any
// other shadcn app) + a national-number Input, live-formatted and validated per selected
// country via libphonenumber-js — no country-code typing required, red border/text on an
// invalid number (same convention as LoginPageV2's field errors).
export function PhoneInput({
  id,
  country,
  nationalNumber,
  onCountryChange,
  onNationalNumberChange,
  onValidityChange,
}: {
  id?: string;
  country: CountryCode;
  nationalNumber: string;
  onCountryChange: (country: CountryCode) => void;
  onNationalNumberChange: (value: string) => void;
  onValidityChange?: (isValid: boolean) => void;
}) {
  useEffect(() => {
    void ensureFlagIconsCss();
  }, []);

  const [open, setOpen] = useState(false);
  const selected = COUNTRIES.find((c) => c.code === country) ?? COUNTRIES[0];

  const isValid = useMemo(() => isValidForCountry(nationalNumber, country), [nationalNumber, country]);
  const showError = nationalNumber.trim().length > 0 && !isValid;

  const handleNumberChange = (raw: string) => {
    const formatted = new AsYouType(country).input(raw);
    onNationalNumberChange(formatted);
    onValidityChange?.(isValidForCountry(formatted, country));
  };

  const handleCountryChange = (next: CountryCode) => {
    onCountryChange(next);
    setOpen(false);
    onValidityChange?.(isValidForCountry(nationalNumber, next));
  };

  return (
    <div>
      <div className="flex gap-1.5">
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger asChild>
            <Button
              type="button"
              variant="outline"
              role="combobox"
              aria-expanded={open}
              className={cn("h-9 shrink-0 gap-1 px-2 text-sm", showError && "border-destructive")}
            >
              <span className={`fi fi-${selected.flagClass}`} />
              <span className="text-muted-foreground">+{selected.callingCode}</span>
              <ChevronsUpDown className="size-3.5 text-muted-foreground" />
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-64 p-0" align="start">
            <Command>
              <CommandInput placeholder="Search country..." />
              <CommandList>
                <CommandEmpty>No country found.</CommandEmpty>
                <CommandGroup>
                  {COUNTRIES.map((c) => (
                    <CommandItem
                      key={c.code}
                      value={`${c.name} +${c.callingCode}`}
                      onSelect={() => handleCountryChange(c.code)}
                    >
                      <Check className={cn("size-4", c.code === country ? "opacity-100" : "opacity-0")} />
                      <span className={`fi fi-${c.flagClass}`} />
                      <span className="flex-1 truncate">{c.name}</span>
                      <span className="text-muted-foreground">+{c.callingCode}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>

        <Input
          id={id}
          type="tel"
          inputMode="tel"
          value={nationalNumber}
          onChange={(e) => handleNumberChange(e.target.value)}
          placeholder="(555) 010-0001"
          className={cn("flex-1", showError && "border-destructive focus-visible:ring-destructive")}
        />
      </div>
      {showError && (
        <p className="mt-1 text-sm text-destructive">Enter a valid phone number for {selected.name}.</p>
      )}
    </div>
  );
}
