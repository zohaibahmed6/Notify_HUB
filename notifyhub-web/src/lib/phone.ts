import { getCountries, getCountryCallingCode, isValidPhoneNumber, parsePhoneNumberFromString } from "libphonenumber-js/min";
import type { CountryCode } from "libphonenumber-js/min";

export type { CountryCode };

export const DEFAULT_COUNTRY: CountryCode = "US";

const regionNames = new Intl.DisplayNames(["en"], { type: "region" });

export interface CountryOption {
  code: CountryCode;
  name: string;
  callingCode: string;
  /** Lowercase ISO 3166-1 alpha-2 code for the `flag-icons` CSS class (`fi fi-{flagClass}`). */
  flagClass: string;
}

export const COUNTRIES: CountryOption[] = getCountries()
  .map((code) => ({
    code,
    name: regionNames.of(code) ?? code,
    callingCode: getCountryCallingCode(code),
    flagClass: code.toLowerCase(),
  }))
  .sort((a, b) => a.name.localeCompare(b.name));

export function isValidForCountry(nationalNumber: string, country: CountryCode): boolean {
  if (!nationalNumber.trim()) return false;
  return isValidPhoneNumber(nationalNumber, country);
}

// Returns null if the number isn't valid for the given country — callers should only call
// this once isValidForCountry has already confirmed validity (e.g. at submit time).
export function toE164(nationalNumber: string, country: CountryCode): string | null {
  return parsePhoneNumberFromString(nationalNumber, country)?.number ?? null;
}
