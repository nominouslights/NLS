// APP-LOCAL FOR NOW — NOT YET A COPY. Dispatcher/lib/company.ts carries the carrier letterhead
// (name, address, one phone number) but no `dispatchPhone` field; the HOS plan adds one there
// and this file then becomes a header-copied file on the drift manifest. Until frontend-dev
// makes that change, the number below is Dispatcher's `COMPANY.phone`, read once by hand on
// 2026-10-10 — never edit it here, change Dispatcher first. Only the alert modal and the
// out-of-hours banner read it.

export interface CompanyInfo {
  name: string;
  /** The number a driver calls from the roadside. Display form, as the console prints it. */
  dispatchPhone: string;
}

export const COMPANY: CompanyInfo = {
  name: "Northern Link Shuttle & Cargo",
  dispatchPhone: "(204) 441-7724",
};

/** A `tel:` href from the display number — digits only, with Canada's country code. */
export function telHref(displayPhone: string): string {
  const digits = displayPhone.replace(/\D/g, "");
  return `tel:+${digits.length === 10 ? `1${digits}` : digits}`;
}
