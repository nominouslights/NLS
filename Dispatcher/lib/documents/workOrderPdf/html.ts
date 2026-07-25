// Small HTML helpers shared by the NL-WO-01 section builders. Pure string
// functions — no framework, no state.

export function esc(v: unknown): string {
  if (v === null || v === undefined) return "";
  return String(v).replace(/[&<>"]/g, (c) =>
    c === "&" ? "&amp;" : c === "<" ? "&lt;" : c === ">" ? "&gt;" : "&quot;",
  );
}

/** A labelled field cell: small uppercase label above a value (or blank line). */
export function field(label: string, value?: unknown, opts: { mono?: boolean; wide?: boolean } = {}): string {
  const cls = ["fld", opts.wide ? "wide" : "", opts.mono ? "mono" : ""].filter(Boolean).join(" ");
  return `<div class="${cls}"><div class="lbl">${esc(label)}</div><div class="val">${esc(value) || "&nbsp;"}</div></div>`;
}

/** A bare checkbox glyph (no label) — for table cells and inline runs. */
export function box(checked: boolean): string {
  return checked ? "☒" : "☐";
}

/** A checkbox glyph + label; filled when checked. */
export function check(checked: boolean, label: string): string {
  return `<span class="chk">${checked ? "☒" : "☐"} ${esc(label)}</span>`;
}

/** A navy section header bar. */
export function sectionBar(title: string): string {
  return `<div class="sec">${esc(title)}</div>`;
}

/** Wrap cells in a bordered field row/grid with N equal columns. */
export function grid(cells: string[], cols?: number): string {
  const style = cols ? ` style="grid-template-columns:repeat(${cols},1fr)"` : "";
  return `<div class="grid"${style}>${cells.join("")}</div>`;
}
