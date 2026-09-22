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
// landscape-only, 10-inch class, never BYOD. There is no phone breakpoint and no width-based
// @media query anywhere in this app. (app/globals.css carries a prefers-reduced-motion block,
// inherited from the Dispatcher copy — that is an accessibility preference, not a breakpoint,
// and it stays.)
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
 * One-question-per-screen geometry, for the DVIR wizard.
 *
 * The wizard is the only surface in this app that must fit 1280×800 with NO SCROLL AT ALL — a
 * driver answering a legal attestation must never be able to leave part of the question off
 * screen. (screens/shared.tsx's `Screen` gives its body `overflowY: "auto"`, which is exactly
 * what the wizard must not have; only the review step scrolls.)
 *
 *   answerH    168  3× the 56px touch.primary floor. A gloved thumb in a moving vehicle gets a
 *                   target it cannot miss, and three tiles still leave room for the question
 *                   and the footer inside 800.
 *   answerMinW 300  Usable width is 1280 − 200 (expanded rail) − 52 (2× gap.page) = 1028.
 *                   Three at 300 plus two gap.row = 928 — fits expanded AND collapsed. The
 *                   defect step offers only two (NL-PTI-01 has no third box), which is the
 *                   same geometry with more air, not a different one.
 *   question    40  The type scale tops out at `metric: 34`, which is a NUMBER size. The
 *                   question is prose and must out-rank every other string on screen; 40 keeps
 *                   most NL-PTI-01 row labels on one line and the longest on two.
 *   footerH     88  touch.primary 56 + 2×16 air. Same family as bar.height 72 without
 *                   equalling it, so the two rows never read as one control strip.
 *   barH         6  Thinner reads as decoration at 1280; thicker competes with the question.
 *
 * Height budget, worst case, with NL-PTI-01's longest rows: 800 − bar.height 72 = 728 for the
 * frame; header ~90 + barH 6 + body padding 20 + footerH 88 leaves ~524 for a step. A check
 * step spends at most ~467 of it (area line 19 + a two-line question 88 + a two-line Check For
 * 64 + tiles 168 + note 72 + four gap.row 56) and the defect step ~479. WizardFrame has NO
 * scroll container, so a step that does not fit is CLIPPED, not scrolled — anything added here
 * has to come out of that slack, not out of the question's size.
 */
export const wizard = {
  answerH: 168,
  answerMinW: 300,
  question: 40,
  footerH: 88,
  barH: 6,
} as const;

/**
 * The design viewport. Not a breakpoint — the target device. Screens are laid out to fit this
 * without horizontal scroll; verify at exactly this size in the device toolbar.
 */
export const viewport = {
  w: 1280,
  h: 800,
} as const;
