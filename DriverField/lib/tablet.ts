// ---------------------------------------------------------------------------
// Tablet geometry. App-local — NOT on any copy manifest, and never copied anywhere else.
//
// THE RULE THIS FILE EXISTS TO ENFORCE:
//   lib/theme.ts owns colour and type FAMILY.  lib/tablet.ts owns SIZE and SPACING.
//   No hex literal ever enters this file. No number in theme.ts is ever changed to suit
//   this app.
//
// Why: theme.ts and 21 other files are byte-identical copies of Dispatcher's design system,
// checked by a diff (see DriverField/CLAUDE.md). This app renders at roughly twice the
// physical size of a desktop console — a 10-inch landscape tablet, mounted, operated with
// gloves, often in daylight. Every one of those size changes would be an edit to a protected
// copy. Putting the sizes here instead is what lets both facts be true at once.
//
// ANTI-PATTERN, named so nobody reinvents it: never wrap a copied component in
// `transform: scale()` or a CSS override to make it bigger. It breaks text rendering, and it
// hides the signal you actually want — that the desktop primitive does not fit here, and a
// ui-tablet counterpart is owed.
//
// Form factor is fixed by architecture non-negotiable #5: company-issued Android tablet,
// landscape-only, 10-inch class, never BYOD. There is no phone breakpoint and no @media query
// anywhere in this app.
// ---------------------------------------------------------------------------

/** Minimum hit targets. `min` is the floor for anything tappable; `primary` for real actions. */
export const touch = {
  /** Absolute floor for any interactive element, including icon-only controls. */
  min: 44,
  /** Buttons, list rows a driver taps while the vehicle is running, form inputs. */
  primary: 56,
  /** Navigation rail rows. */
  rail: 64,
} as const;

/** Top bar. Taller than the consoles' 56px — it carries the duty chip and the sync pill. */
export const bar = {
  height: 72,
} as const;

/** Navigation rail. Wider tiles and labels than NavRail's 236/72 desktop geometry. */
export const rail = {
  expanded: 200,
  collapsed: 88,
  /** The two-letter code tile. NavRail's is 26px — unreadable at arm's length. */
  tile: 44,
} as const;

/** Type scale, in px. Families and colours come from theme.ts. */
export const type = {
  /** Rail group headings. */
  group: 12,
  /** Field labels, secondary row text. */
  label: 16,
  /** Primary row text, form values. */
  value: 22,
  /** The big number on a metric tile. */
  metric: 34,
} as const;

export const gap = {
  tight: 8,
  row: 14,
  section: 20,
  page: 26,
} as const;

export const radius = {
  control: 8,
  panel: 12,
} as const;

/**
 * The design viewport. Not a breakpoint — the target device. Screens are laid out to fit this
 * without horizontal scroll; verify at exactly this size in the device toolbar.
 */
export const viewport = {
  w: 1280,
  h: 800,
} as const;
